using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CysharpActions.Tests;

public class ScanPrUnicodeCommandTest
{
    private static readonly PullRequestScanInput Input = new(
        new string('b', 40),
        "Clean title",
        "Clean body");

    [Fact]
    public void CleanChangedFilePassesTest()
    {
        var violations = Scan(new PrChangedFile("src/Program.cs", null, Utf8("class Program { }")));

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(0x200B)] // ZERO WIDTH SPACE (Cf)
    [InlineData(0x202E)] // RIGHT-TO-LEFT OVERRIDE (Cf)
    [InlineData(0x3164)] // HANGUL FILLER (Lo, Default Ignorable)
    [InlineData(0xFE0F)] // VARIATION SELECTOR-16 (Mn, Default Ignorable)
    public void RawForbiddenCodePointFailsAnywhereInTextTest(int codePoint)
    {
        var text = $"prefix{char.ConvertFromUtf32(codePoint)}suffix";

        var violation = Assert.Single(Scan(new PrChangedFile("src/Test.cs", null, Utf8(text))));

        Assert.Equal(codePoint, violation.CodePoint);
        Assert.Equal("raw", violation.Rule);
    }

    public static TheoryData<string, int> ForbiddenCSharpEscapes => new()
    {
        { "\\" + "u200B", 0x200B },
        { "\\" + "U00003164", 0x3164 },
        { "var value = \"" + "\\" + "uFE0F\";", 0xFE0F },
        { "// example: " + "\\" + "u202E", 0x202E },
    };

    [Theory]
    [MemberData(nameof(ForbiddenCSharpEscapes))]
    public void ForbiddenCSharpUnicodeEscapeFailsRegardlessOfSyntaxContextTest(string text, int codePoint)
    {
        var violation = Assert.Single(Scan(new PrChangedFile("src/Test.cs", null, Utf8(text))));

        Assert.Equal(codePoint, violation.CodePoint);
        Assert.Equal("C# Unicode escape", violation.Rule);
    }

    [Fact]
    public void UnicodeEscapeIsAllowedInNonCSharpDocumentationTest()
    {
        var violations = Scan(new PrChangedFile("README.md", null, Utf8("Use " + "\\" + "u200B in this example.")));

        Assert.Empty(violations);
    }

    [Fact]
    public void RawForbiddenCodePointIsAllowedInNonCSharpFileTest()
    {
        var text = "example" + char.ConvertFromUtf32(0x200B);

        Assert.Empty(Scan(new PrChangedFile("README.md", null, Utf8(text))));
    }

    [Fact]
    public void VisibleCSharpUnicodeEscapeIsAllowedTest()
    {
        var violations = Scan(new PrChangedFile("src/Test.cs", null, Utf8("int " + "\\" + "u0061 = 1;")));

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("FFFFFFFF")]
    [InlineData("80000000")]
    [InlineData("00110000")]
    [InlineData("0000D800")]
    public void InvalidCSharpUnicodeEscapeDoesNotOverflowScannerTest(string digits)
    {
        var text = "\\" + "U" + digits;

        Assert.Empty(Scan(new PrChangedFile("src/Test.cs", null, Utf8(text))));
        Assert.Empty(ScanPrUnicodeCommand.ScanCSharpChunksForTest(
            Utf8(text).Select(value => new ReadOnlyMemory<byte>([value])).ToArray()));
    }

    [Fact]
    public void LeadingUtf8BomIsAllowedButEmbeddedBomFailsTest()
    {
        var bom = char.ConvertFromUtf32(0xFEFF);

        Assert.Empty(Scan(new PrChangedFile("src/Clean.cs", null, Utf8(bom + "class C {}"))));
        var violation = Assert.Single(Scan(new PrChangedFile("src/Bad.cs", null, Utf8("class" + bom + " C {}"))));
        Assert.Equal(0xFEFF, violation.CodePoint);
    }

    [Fact]
    public void NonAsciiSpaceFailsInCSharpButIsAllowedInDocumentationTest()
    {
        var ideographicSpace = char.ConvertFromUtf32(0x3000);

        var violation = Assert.Single(Scan(new PrChangedFile("src/Test.cs", null, Utf8("// 日本語" + ideographicSpace + "コメント"))));
        Assert.Equal(0x3000, violation.CodePoint);
        Assert.Equal("non-ASCII space in a C# source file", violation.Reason);
        Assert.Empty(Scan(new PrChangedFile("README.md", null, Utf8("日本語" + ideographicSpace + "文章"))));
    }

    [Fact]
    public void NonAsciiSpaceInFileNameHasFileNameReasonAndAccuratePositionTest()
    {
        var path = "dir\rname" + char.ConvertFromUtf32(0x3000) + ".cs";

        var violation = Assert.Single(Scan(new PrChangedFile(path, null, Utf8("class C {}"))));

        Assert.Equal((2, 5, 0x3000), (violation.Line, violation.Column, violation.CodePoint));
        Assert.Equal("non-ASCII space in a file name", violation.Reason);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void MetadataLineSeparatorsProduceAccurateViolationPositionTest(string separator)
    {
        var title = "first" + separator + char.ConvertFromUtf32(0x200B);

        var violation = Assert.Single(
            Scan(new PullRequestScanInput(Input.HeadSha, title, "Clean body")),
            x => x.CodePoint == 0x200B);

        Assert.Equal((2, 1), (violation.Line, violation.Column));
    }

    [Fact]
    public void PullRequestBodyAllowsDependabotMentionSuppressionTest()
    {
        var zeroWidthSpace = char.ConvertFromUtf32(0x200B);
        var body = "by <a href=\"https://github.com/octocat\"><code>@" + zeroWidthSpace + "octocat</code></a>\n@" + zeroWidthSpace + "hubot";

        Assert.Empty(Scan(new PullRequestScanInput(Input.HeadSha, "Bump actions", body)));
    }

    [Theory]
    [InlineData("a{0}b", 0x200B, 2)]    // ZERO WIDTH SPACE not after '@'
    [InlineData("{0}b", 0x200B, 1)]     // ZERO WIDTH SPACE at the start
    [InlineData("@{0}{0}b", 0x200B, 3)] // only the first ZERO WIDTH SPACE after '@' is allowed
    [InlineData("@{0}b", 0x200C, 2)]    // ZERO WIDTH NON-JOINER after '@'
    [InlineData("@{0}b", 0x202E, 2)]    // RIGHT-TO-LEFT OVERRIDE after '@'
    public void PullRequestBodyRejectsOtherFormatCharactersTest(string template, int codePoint, int column)
    {
        var body = string.Format(template, char.ConvertFromUtf32(codePoint));
        var violation = Assert.Single(Scan(new PullRequestScanInput(Input.HeadSha, "Clean title", body)));

        Assert.Equal(("PR body", 1, column, codePoint), (violation.Source, violation.Line, violation.Column, violation.CodePoint));
    }

    [Fact]
    public void ZeroWidthSpaceAfterAtIsRejectedOutsidePullRequestBodyTest()
    {
        var text = "@" + char.ConvertFromUtf32(0x200B) + "octocat";

        var titleViolation = Assert.Single(Scan(new PullRequestScanInput(Input.HeadSha, text, "Clean body")));
        Assert.Equal(("PR title", 2, 0x200B), (titleViolation.Source, titleViolation.Column, titleViolation.CodePoint));

        var pathViolation = Assert.Single(Scan(new PrChangedFile("docs/" + text + ".md", null, Utf8("text"))));
        Assert.Equal(0x200B, pathViolation.CodePoint);

        var contentViolation = Assert.Single(Scan(new PrChangedFile("src/Test.cs", null, Utf8("// " + text))));
        Assert.Equal(0x200B, contentViolation.CodePoint);
    }

    [Fact]
    public void CSharpMustBeValidUtf8ButNonCSharpFileIsSkippedTest()
    {
        var invalidUtf8 = new byte[] { 0xFF, 0xFE, 0xFD };

        var violation = Assert.Single(Scan(new PrChangedFile("src/Test.cs", null, invalidUtf8)));
        Assert.Contains("not valid UTF-8", violation.Reason, StringComparison.Ordinal);
        Assert.Empty(Scan(new PrChangedFile("image.bin", null, invalidUtf8)));
    }

    [Fact]
    public void ChunkBoundariesDoNotChangeUnicodeResultsTest()
    {
        var text = char.ConvertFromUtf32(0xFEFF) +
            "first\r\n" +
            char.ConvertFromUtf32(0x00A0) +
            char.ConvertFromUtf32(0x200B) +
            char.ConvertFromUtf32(0x1D173) +
            "\\" + "u200B" +
            "\\" + "U00003164" +
            "\nend";
        var bytes = Utf8(text);
        var expected = ScanPrUnicodeCommand.ScanCSharpChunksForTest(bytes);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var actual = ScanPrUnicodeCommand.ScanCSharpChunksForTest(
                bytes.AsMemory(0, split),
                bytes.AsMemory(split));
            Assert.Equal(expected, actual);
        }

        var oneByteChunks = bytes.Select(value => new ReadOnlyMemory<byte>([value])).ToArray();
        Assert.Equal(expected, ScanPrUnicodeCommand.ScanCSharpChunksForTest(oneByteChunks));
        Assert.Collection(
            expected,
            violation => Assert.Equal((2, 1, 0x00A0), (violation.Line, violation.Column, violation.CodePoint)),
            violation => Assert.Equal((2, 2, 0x200B), (violation.Line, violation.Column, violation.CodePoint)),
            violation => Assert.Equal((2, 3, 0x1D173), (violation.Line, violation.Column, violation.CodePoint)),
            violation => Assert.Equal((2, 5, 0x200B), (violation.Line, violation.Column, violation.CodePoint)),
            violation => Assert.Equal((2, 11, 0x3164), (violation.Line, violation.Column, violation.CodePoint)));
    }

    [Theory]
    [MemberData(nameof(InvalidUtf8Chunks))]
    public void InvalidUtf8IsRejectedAcrossChunkBoundariesTest(byte[] bytes)
    {
        for (var split = 0; split <= bytes.Length; split++)
        {
            Assert.Throws<DecoderFallbackException>(() =>
                ScanPrUnicodeCommand.ScanCSharpChunksForTest(
                    bytes.AsMemory(0, split),
                    bytes.AsMemory(split)));
        }
    }

    public static TheoryData<byte[]> InvalidUtf8Chunks => new()
    {
        new byte[] { (byte)'a', 0xE2, 0x82 },       // Truncated three-byte sequence.
        new byte[] { (byte)'a', 0xE2, 0x28, 0xA1 }, // Invalid continuation byte.
        new byte[] { (byte)'a', 0xF0, 0x80, 0x80, 0x80 }, // Overlong encoding.
    };

    [Fact]
    public void OversizedCSharpFailsWithoutLoadingBlobTest()
    {
        const long oversized = 10L * 1024 * 1024 + 1;

        var violation = Assert.Single(Scan(new PrChangedFile("src/Test.cs", null, [], DeclaredSize: oversized)));
        Assert.Contains("exceeds", violation.Reason, StringComparison.Ordinal);
        Assert.Empty(Scan(new PrChangedFile("image.bin", null, [], DeclaredSize: oversized)));
    }

    [Fact]
    public void ScanRetainsOnlyAnnotationLimitViolationsTest()
    {
        var zeroWidthSpaces = string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x200B), 100));

        var violations = Scan(new PrChangedFile("src/Test.cs", null, Utf8(zeroWidthSpaces)));

        Assert.Equal(50, violations.Count);
    }

    [Fact]
    public void AnnotationVisualizesAttackerControlledControlsAndInvisibleUnicodeTest()
    {
        var source = "src/" +
            char.ConvertFromUtf32(0x001B) + // ESC (C0 control)
            char.ConvertFromUtf32(0x0009) + // TAB (C0 control allowed in C# source text)
            char.ConvertFromUtf32(0x0085) + // NEXT LINE (C1 control)
            char.ConvertFromUtf32(0x202E) + // RIGHT-TO-LEFT OVERRIDE (Cf)
            char.ConvertFromUtf32(0xE0001) + // LANGUAGE TAG (supplementary-plane Default Ignorable)
            "%,:Test.cs";
        var violation = new UnicodeViolation(source, 3, 4, 0x202E, "raw", "Forbidden format character.");

        var annotation = ScanPrUnicodeCommand.FormatAnnotation(violation);
        const string slash = "\\";

        Assert.Equal(
            "::error file=src/" + slash + "u001B" + slash + "u0009" + slash + "u0085" +
            slash + "u202E" + slash + "U000E0001%25%2C%3ATest.cs,line=3,col=4,title=Forbidden Unicode::src/" +
            slash + "u001B" + slash + "u0009" + slash + "u0085" + slash + "u202E" +
            slash + "U000E0001%25,:Test.cs:3:4: raw: U+202E; Forbidden format character.",
            annotation);
        Assert.DoesNotContain(char.ConvertFromUtf32(0x001B), annotation, StringComparison.Ordinal);
        Assert.DoesNotContain(char.ConvertFromUtf32(0x202E), annotation, StringComparison.Ordinal);
        Assert.DoesNotContain(char.ConvertFromUtf32(0xE0001), annotation, StringComparison.Ordinal);
    }

    [Fact]
    public void AnnotationVisualizationPreservesWorkflowCommandEscapingTest()
    {
        var violation = UnicodeViolation.FileError("bad\r\n%,name.cs", "failed\u2028next");

        var annotation = ScanPrUnicodeCommand.FormatAnnotation(violation);

        Assert.Equal(
            "::error title=Unicode scan failed::bad\\u000D\\u000A%25,name.cs: failed\\u2028next",
            annotation);
        Assert.DoesNotContain('\r', annotation);
        Assert.DoesNotContain('\n', annotation);
        Assert.DoesNotContain('\u2028', annotation);
    }

    [Fact]
    public void AnnotationMessageIncludesSourceLineAndColumnTest()
    {
        var violation = new UnicodeViolation(
            "src/Test.cs",
            12,
            34,
            0x202E,
            "C# Unicode escape",
            "escape resolves to a forbidden identifier character");

        var annotation = ScanPrUnicodeCommand.FormatAnnotation(violation);

        Assert.Equal(
            "::error file=src/Test.cs,line=12,col=34,title=Forbidden Unicode::src/Test.cs:12:34: C# Unicode escape: U+202E; escape resolves to a forbidden identifier character",
            annotation);
    }

    [Fact]
    public async Task ValidateCountsAllViolationsWhileRetainingOnlyAnnotationsTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(ValidateCountsAllViolationsWhileRetainingOnlyAnnotationsTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            var eventPath = Path.Combine(directory, "event.json");
            CreateFile(eventPath, JsonSerializer.Serialize(new
            {
                pull_request = new
                {
                    title = "Clean title",
                    body = "Clean body",
                    head = new { sha = new string('b', 40) },
                },
            }));
            var zeroWidthSpaces = string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x200B), 100));
            var source = new TestPrChangeSource(
                new PrChangedFile("src/Test.cs", null, Utf8(zeroWidthSpaces)));

            var command = new ScanPrUnicodeCommand(source);
            var exception = await Assert.ThrowsAsync<UnicodeScanViolationException>(() =>
                command.ValidateAsync(eventPath, directory, TestContext.Current.CancellationToken));

            Assert.Contains("100 violation", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public void FileNamesAndPullRequestMetadataAreScannedTest()
    {
        var zeroWidthSpace = char.ConvertFromUtf32(0x200B);
        var input = Input with { Title = "title" + zeroWidthSpace, Body = "body" + zeroWidthSpace };

        var violations = Scan(
            input,
            new PrChangedFile("src/new" + zeroWidthSpace + ".cs", "src/old" + zeroWidthSpace + ".cs", Utf8("class C {}")));

        Assert.Equal(4, violations.Count(x => x.CodePoint == 0x200B));
    }

    [Fact]
    public void GitLinkContentIsNotScannedTest()
    {
        var violations = Scan(new PrChangedFile("vendor/submodule", null, Utf8(char.ConvertFromUtf32(0x200B)), IsGitLink: true));

        Assert.Empty(violations);
    }

    [Fact]
    public void CSharpSymbolicLinkFailsWithoutFollowingTargetTest()
    {
        var violation = Assert.Single(Scan(new PrChangedFile(
            "src/Link.cs",
            null,
            [],
            IsSymbolicLink: true)));

        Assert.Contains("symbolic link", violation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void NonCSharpSymbolicLinkIsOutsideContentPolicyTest()
    {
        Assert.Empty(Scan(new PrChangedFile(
            "docs/Link.txt",
            null,
            [],
            IsSymbolicLink: true)));
    }

    [Fact]
    public async Task ValidateReadsChangedFileFromCheckedOutWorkingTreeTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(ValidateReadsChangedFileFromCheckedOutWorkingTreeTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await InitRepositoryAsync(directory);

            CreateFile(Path.Combine(directory, "Test.cs"), "class C {}");
            await RunGitAsync(directory, "add", "--", "Test.cs");
            await RunGitAsync(directory, "commit", "-m", "base");

            var headSha = await CheckoutPullRequestMergeAsync(directory, async () =>
            {
                CreateFile(Path.Combine(directory, "Test.cs"), "class Changed {}");
                await RunGitAsync(directory, "add", "--", "Test.cs");
            });

            // The changed-file list comes from the merge commit's first parent, but content must come from the checked-out tree.
            var zeroWidthSpace = char.ConvertFromUtf32(0x200B);
            CreateFile(Path.Combine(directory, "Test.cs"), "class" + zeroWidthSpace + " CheckedOut {}");

            var eventPath = CreateEvent(directory, headSha);

            var command = new ScanPrUnicodeCommand();
            var exception = await Assert.ThrowsAsync<UnicodeScanViolationException>(() =>
                command.ValidateAsync(eventPath, directory, TestContext.Current.CancellationToken));

            Assert.Contains("1 violation", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ValidateRejectsCSharpSymbolicLinkFromGitModeTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(ValidateRejectsCSharpSymbolicLinkFromGitModeTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await InitRepositoryAsync(directory);

            CreateFile(Path.Combine(directory, "README.md"), "base");
            await RunGitAsync(directory, "add", "--", "README.md");
            await RunGitAsync(directory, "commit", "-m", "base");

            var headSha = await CheckoutPullRequestMergeAsync(directory, async () =>
            {
                // Construct mode 120000 in the Git index directly. This is independent of whether the test
                // host is allowed to create an operating-system symbolic link.
                CreateFile(Path.Combine(directory, "Link.cs"), "payload.txt");
                var linkBlob = await RunGitAsync(directory, "hash-object", "-w", "--", "Link.cs");
                await RunGitAsync(directory, "update-index", "--add", "--cacheinfo", $"120000,{linkBlob},Link.cs");
                // The regular file differs from the indexed link and would block switching back to the base commit.
                File.Delete(Path.Combine(directory, "Link.cs"));
            });

            var eventPath = CreateEvent(directory, headSha);

            var command = new ScanPrUnicodeCommand();
            var exception = await Assert.ThrowsAsync<UnicodeScanViolationException>(() =>
                command.ValidateAsync(eventPath, directory, TestContext.Current.CancellationToken));

            Assert.Contains("1 violation", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ValidateRejectsUnchangedCSharpSymbolicLinkWhenTargetChangesTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(ValidateRejectsUnchangedCSharpSymbolicLinkWhenTargetChangesTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await InitRepositoryAsync(directory);

            CreateFile(Path.Combine(directory, "Payload.txt"), "class Clean {}");
            CreateFile(Path.Combine(directory, "Link.cs"), "Payload.txt");
            await RunGitAsync(directory, "add", "--", "Payload.txt");
            var linkBlob = await RunGitAsync(directory, "hash-object", "-w", "--", "Link.cs");
            await RunGitAsync(directory, "update-index", "--add", "--cacheinfo", $"120000,{linkBlob},Link.cs");
            await RunGitAsync(directory, "commit", "-m", "base");

            var headSha = await CheckoutPullRequestMergeAsync(directory, async () =>
            {
                CreateFile(
                    Path.Combine(directory, "Payload.txt"),
                    "class" + char.ConvertFromUtf32(0x200B) + " Changed {}");
                await RunGitAsync(directory, "add", "--", "Payload.txt");
            });

            var eventPath = CreateEvent(directory, headSha);

            var command = new ScanPrUnicodeCommand();
            var exception = await Assert.ThrowsAsync<UnicodeScanViolationException>(() =>
                command.ValidateAsync(eventPath, directory, TestContext.Current.CancellationToken));

            Assert.Contains("1 violation", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task GitSourceDiffsAgainstAdvancedBaseInShallowCloneTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(GitSourceDiffsAgainstAdvancedBaseInShallowCloneTest)}");
        try
        {
            var (origin, mergeBaseSha, headSha) = await CreateAdvancedBasePullRequestOriginAsync(Path.Combine(directory, "origin"));
            var clone = Path.Combine(directory, "clone");
            await ShallowCheckoutPullRequestMergeAsync(origin, clone, depth: 2);

            // pull_request.base.sha is the merge base, which is outside a fetch-depth: 2 clone once the base branch advances.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RunGitAsync(clone, "cat-file", "-e", mergeBaseSha + "^{commit}"));

            var paths = new List<string>();
            var fileCount = await new GitPrChangeSource().VisitChangedFilesAsync(
                clone,
                headSha,
                (file, _, _) =>
                {
                    paths.Add(file.Path);
                    return Task.FromResult(true);
                },
                TestContext.Current.CancellationToken);

            // Base-only changes (Advanced.cs) are already on the base branch and must not be reported as PR changes.
            Assert.Equal(1, fileCount);
            Assert.Equal("Pr.cs", Assert.Single(paths));

            var eventPath = CreateEvent(directory, headSha);
            await new ScanPrUnicodeCommand().ValidateAsync(eventPath, clone, TestContext.Current.CancellationToken);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task GitSourceRejectsMergeWithoutFetchedParentsTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(GitSourceRejectsMergeWithoutFetchedParentsTest)}");
        try
        {
            var (origin, _, headSha) = await CreateAdvancedBasePullRequestOriginAsync(Path.Combine(directory, "origin"));
            var clone = Path.Combine(directory, "clone");
            await ShallowCheckoutPullRequestMergeAsync(origin, clone, depth: 1);

            var exception = await Assert.ThrowsAsync<ActionCommandException>(() =>
                new GitPrChangeSource().VisitChangedFilesAsync(
                    clone,
                    headSha,
                    (_, _, _) => Task.FromResult(true),
                    TestContext.Current.CancellationToken));

            Assert.Contains("not a pull request merge commit", exception.Message, StringComparison.Ordinal);
            Assert.Contains("fetch-depth 2", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task GitSourceRejectsNonMergeHeadTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(GitSourceRejectsNonMergeHeadTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await InitRepositoryAsync(directory);

            CreateFile(Path.Combine(directory, "README.md"), "base");
            await RunGitAsync(directory, "add", "--", "README.md");
            await RunGitAsync(directory, "commit", "-m", "base");

            CreateFile(Path.Combine(directory, "Changed.cs"), "class C {}");
            await RunGitAsync(directory, "add", "--", "Changed.cs");
            await RunGitAsync(directory, "commit", "-m", "head");
            var headSha = await RunGitAsync(directory, "rev-parse", "HEAD");

            var exception = await Assert.ThrowsAsync<ActionCommandException>(() =>
                new GitPrChangeSource().VisitChangedFilesAsync(
                    directory,
                    headSha,
                    (_, _, _) => Task.FromResult(true),
                    TestContext.Current.CancellationToken));

            Assert.Contains("not a pull request merge commit", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task GitSourceRejectsMergeOfDifferentHeadTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(GitSourceRejectsMergeOfDifferentHeadTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await InitRepositoryAsync(directory);

            CreateFile(Path.Combine(directory, "README.md"), "base");
            await RunGitAsync(directory, "add", "--", "README.md");
            await RunGitAsync(directory, "commit", "-m", "base");

            await CheckoutPullRequestMergeAsync(directory, async () =>
            {
                CreateFile(Path.Combine(directory, "Changed.cs"), "class C {}");
                await RunGitAsync(directory, "add", "--", "Changed.cs");
            });

            // A stale checkout or another ref merges a head other than the one in the event payload.
            var exception = await Assert.ThrowsAsync<ActionCommandException>(() =>
                new GitPrChangeSource().VisitChangedFilesAsync(
                    directory,
                    new string('c', 40),
                    (_, _, _) => Task.FromResult(true),
                    TestContext.Current.CancellationToken));

            Assert.Contains("not the pull request head", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task GitSourceChecksTotalSizeBeforeLoadingNextBlobTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(GitSourceChecksTotalSizeBeforeLoadingNextBlobTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await InitRepositoryAsync(directory);

            CreateFile(Path.Combine(directory, "README.md"), "base");
            await RunGitAsync(directory, "add", "--", "README.md");
            await RunGitAsync(directory, "commit", "-m", "base");

            var headSha = await CheckoutPullRequestMergeAsync(directory, async () =>
            {
                CreateFile(Path.Combine(directory, "A.cs"), "12345678");
                CreateFile(Path.Combine(directory, "B.cs"), "abcdefgh");
                await RunGitAsync(directory, "add", "--", "A.cs", "B.cs");
            });

            var files = new List<PrChangedFile>();
            var source = new GitPrChangeSource(maxTotalTextBytes: 10);
            await source.VisitChangedFilesAsync(
                directory,
                headSha,
                async (file, content, cancellationToken) =>
                {
                    if (content is null)
                    {
                        files.Add(file);
                    }
                    else
                    {
                        await using var copy = new MemoryStream();
                        await content.CopyToAsync(copy, cancellationToken);
                        files.Add(file with { Content = copy.ToArray() });
                    }
                    return true;
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(2, files.Count);
            Assert.Equal("A.cs", files[0].Path);
            Assert.Equal(8, files[0].Content.Length);
            Assert.Equal("B.cs", files[1].Path);
            Assert.Empty(files[1].Content);
            Assert.Contains("total scan limit", files[1].PreScanError, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task GitSourceRejectsTooManyChangedFilesTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(GitSourceRejectsTooManyChangedFilesTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await InitRepositoryAsync(directory);

            CreateFile(Path.Combine(directory, "README.md"), "base");
            await RunGitAsync(directory, "add", "--", "README.md");
            await RunGitAsync(directory, "commit", "-m", "base");

            var headSha = await CheckoutPullRequestMergeAsync(directory, async () =>
            {
                CreateFile(Path.Combine(directory, "A.txt"), "a");
                CreateFile(Path.Combine(directory, "B.txt"), "b");
                CreateFile(Path.Combine(directory, "C.txt"), "c");
                await RunGitAsync(directory, "add", "--", "A.txt", "B.txt", "C.txt");
            });

            var boundarySource = new GitPrChangeSource(maxChangedFiles: 3);
            var visited = 0;
            var fileCount = await boundarySource.VisitChangedFilesAsync(
                directory,
                headSha,
                (_, _, _) =>
                {
                    visited++;
                    return Task.FromResult(true);
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(3, fileCount);
            Assert.Equal(3, visited);

            var source = new GitPrChangeSource(maxChangedFiles: 2);
            var exception = await Assert.ThrowsAsync<ActionCommandException>(() =>
                source.VisitChangedFilesAsync(
                    directory,
                    headSha,
                    (_, _, _) => Task.FromResult(true),
                    TestContext.Current.CancellationToken));

            Assert.Contains("more than the 2 file scan limit", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task GitSourceRejectsOversizedDiffOutputTest()
    {
        var directory = Path.GetFullPath($".tests/{nameof(ScanPrUnicodeCommandTest)}/{nameof(GitSourceRejectsOversizedDiffOutputTest)}");
        try
        {
            Directory.CreateDirectory(directory);
            await InitRepositoryAsync(directory);

            CreateFile(Path.Combine(directory, "README.md"), "base");
            await RunGitAsync(directory, "add", "--", "README.md");
            await RunGitAsync(directory, "commit", "-m", "base");

            var headSha = await CheckoutPullRequestMergeAsync(directory, async () =>
            {
                CreateFile(Path.Combine(directory, "Changed.txt"), "changed");
                await RunGitAsync(directory, "add", "--", "Changed.txt");
            });

            var source = new GitPrChangeSource(maxDiffBytes: 1);
            var exception = await Assert.ThrowsAsync<ActionCommandException>(() =>
                source.VisitChangedFilesAsync(
                    directory,
                    headSha,
                    (_, _, _) => Task.FromResult(true),
                    TestContext.Current.CancellationToken));

            Assert.Contains("Git diff output exceeds", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            SafeDeleteDirectory(directory);
        }
    }

    private static IReadOnlyList<UnicodeViolation> Scan(params PrChangedFile[] files) =>
        Scan(Input, files);

    private static IReadOnlyList<UnicodeViolation> Scan(PullRequestScanInput input, params PrChangedFile[] files) =>
        ScanPrUnicodeCommand.Scan(input, files);

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static async Task<string> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {arguments[0]} failed: {await stderrTask}");
        return stdout.Trim();
    }

    private static async Task InitRepositoryAsync(string directory)
    {
        await RunGitAsync(directory, "init");
        await RunGitAsync(directory, "config", "user.email", "test@example.com");
        await RunGitAsync(directory, "config", "user.name", "Test User");
        await RunGitAsync(directory, "config", "commit.gpgSign", "false");
    }

    // Commits the changes staged by stageHead as the PR head on top of HEAD, then checks out a GitHub-style
    // refs/pull/<n>/merge commit whose first parent is the base branch tip and second parent is the PR head.
    private static async Task<string> CheckoutPullRequestMergeAsync(
        string directory,
        Func<Task> stageHead,
        Func<Task>? advanceBase = null)
    {
        var baseSha = await RunGitAsync(directory, "rev-parse", "HEAD");
        await RunGitAsync(directory, "checkout", "--quiet", "-b", "pull-request");
        await stageHead();
        await RunGitAsync(directory, "commit", "-m", "head");
        var headSha = await RunGitAsync(directory, "rev-parse", "HEAD");

        await RunGitAsync(directory, "checkout", "--quiet", "--detach", baseSha);
        if (advanceBase is not null)
            await advanceBase();
        await RunGitAsync(directory, "merge", "--no-ff", "-m", "merge", headSha);
        return headSha;
    }

    private static async Task<(string Origin, string MergeBaseSha, string HeadSha)> CreateAdvancedBasePullRequestOriginAsync(string origin)
    {
        Directory.CreateDirectory(origin);
        await InitRepositoryAsync(origin);

        CreateFile(Path.Combine(origin, "README.md"), "base");
        await RunGitAsync(origin, "add", "--", "README.md");
        await RunGitAsync(origin, "commit", "-m", "base");
        var mergeBaseSha = await RunGitAsync(origin, "rev-parse", "HEAD");

        var headSha = await CheckoutPullRequestMergeAsync(
            origin,
            async () =>
            {
                CreateFile(Path.Combine(origin, "Pr.cs"), "class Pr {}");
                await RunGitAsync(origin, "add", "--", "Pr.cs");
            },
            async () =>
            {
                // Unrelated commits land on the base branch after the PR branched off.
                CreateFile(Path.Combine(origin, "Advanced.cs"), "class Advanced {}");
                await RunGitAsync(origin, "add", "--", "Advanced.cs");
                await RunGitAsync(origin, "commit", "-m", "advance 1");
                CreateFile(Path.Combine(origin, "README.md"), "advanced");
                await RunGitAsync(origin, "add", "--", "README.md");
                await RunGitAsync(origin, "commit", "-m", "advance 2");
            });
        await RunGitAsync(origin, "update-ref", "refs/pull/1/merge", "HEAD");
        return (origin, mergeBaseSha, headSha);
    }

    // Mirrors actions/checkout for a pull_request event: fetch refs/pull/<n>/merge with fetch-depth and check it out.
    private static async Task ShallowCheckoutPullRequestMergeAsync(string origin, string clone, int depth)
    {
        Directory.CreateDirectory(clone);
        await RunGitAsync(clone, "init");
        // A file:// URL is required for a local fetch to honor --depth.
        await RunGitAsync(
            clone,
            "fetch", "--no-tags", $"--depth={depth}", new Uri(origin).AbsoluteUri,
            "+refs/pull/1/merge:refs/remotes/pull/1/merge");
        await RunGitAsync(clone, "checkout", "--quiet", "--detach", "refs/remotes/pull/1/merge");
    }

    private static string CreateEvent(string directory, string headSha)
    {
        var eventPath = Path.Combine(directory, "event.json");
        CreateFile(eventPath, JsonSerializer.Serialize(new
        {
            pull_request = new
            {
                title = "Clean title",
                body = "Clean body",
                head = new { sha = headSha },
            },
        }));
        return eventPath;
    }

    private sealed class TestPrChangeSource(params PrChangedFile[] files) : IPrChangeSource
    {
        public async Task<int> VisitChangedFilesAsync(
            string repositoryPath,
            string headSha,
            Func<PrChangedFile, Stream?, CancellationToken, Task<bool>> visitor,
            CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                count++;
                await using var content = new MemoryStream(file.Content, writable: false);
                if (!await visitor(
                    file,
                    ScanPrUnicodeCommand.IsCSharpSource(file.Path) ? content : null,
                    cancellationToken))
                {
                    break;
                }
            }
            return count;
        }
    }
}
