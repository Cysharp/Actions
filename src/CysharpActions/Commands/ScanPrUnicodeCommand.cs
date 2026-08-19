using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CysharpActions.Utils;

namespace CysharpActions.Commands;

public sealed class ScanPrUnicodeCommand(IPrChangeSource? changeSource = null)
{
    internal const long MaxFileBytes = 10 * 1024 * 1024;
    private const long MaxTotalTextBytes = 100 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IPrChangeSource changeSource = changeSource ?? new GitPrChangeSource();

    public async Task ValidateAsync(
        string eventPath,
        string repositoryPath = ".",
        CancellationToken cancellationToken = default)
    {
        var input = await ReadPullRequestAsync(eventPath, cancellationToken);
        var files = await changeSource.ReadChangedFilesAsync(
            repositoryPath,
            input.BaseSha,
            input.HeadSha,
            cancellationToken);
        var violations = Scan(input, files);

        foreach (var violation in violations.Take(50))
        {
            WriteAnnotation(violation);
        }
        if (violations.Count > 50)
        {
            GitHubActions.WriteLog($"Unicode scan found {violations.Count} violations. Only the first 50 were annotated.");
        }
        if (violations.Count != 0)
        {
            throw new ActionCommandException($"PR Unicode security scan found {violations.Count} violation(s).");
        }

        GitHubActions.WriteLog($"PR Unicode security scan passed. Scanned {files.Count} changed non-deleted file(s).");
    }

    public static IReadOnlyList<UnicodeViolation> Scan(
        PullRequestScanInput input,
        IReadOnlyList<PrChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var violations = new List<UnicodeViolation>();
        ScanText("PR title", input.Title, UnicodeScanOptions.Metadata, violations);
        ScanText("PR body", input.Body, UnicodeScanOptions.Metadata, violations);

        long totalTextBytes = 0;
        foreach (var file in files)
        {
            ScanText(file.Path, file.Path, UnicodeScanOptions.FileName, violations);
            if (file.OldPath is not null)
            {
                ScanText(file.OldPath, file.OldPath, UnicodeScanOptions.FileName, violations);
            }

            if (file.IsGitLink)
                continue;

            if (!IsCSharpSource(file.Path))
                continue;

            var contentLength = file.DeclaredSize ?? file.Content.LongLength;
            if (contentLength > MaxFileBytes)
            {
                violations.Add(UnicodeViolation.FileError(
                    file.Path,
                    $"C# source file exceeds the {MaxFileBytes / 1024 / 1024} MiB scan limit."));
                continue;
            }

            if (file.Content.Contains((byte)0))
            {
                violations.Add(UnicodeViolation.FileError(file.Path, "C# source file contains a NUL byte."));
                continue;
            }

            string text;
            try
            {
                text = StrictUtf8.GetString(file.Content);
            }
            catch (DecoderFallbackException)
            {
                violations.Add(UnicodeViolation.FileError(file.Path, "C# source file is not valid UTF-8."));
                continue;
            }

            totalTextBytes += file.Content.LongLength;
            if (totalTextBytes > MaxTotalTextBytes)
            {
                violations.Add(UnicodeViolation.FileError(
                    file.Path,
                    $"Changed text exceeds the {MaxTotalTextBytes / 1024 / 1024} MiB total scan limit."));
                break;
            }

            ScanText(
                file.Path,
                text,
                new UnicodeScanOptions(
                    AllowLeadingBom: true,
                    ScanCSharpEscapes: true,
                    RejectNonAsciiSpace: true),
                violations);
        }

        return violations;
    }

    private static async Task<PullRequestScanInput> ReadPullRequestAsync(
        string eventPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
            throw new ActionCommandException("GitHub event path is required.");

        await using var stream = File.OpenRead(eventPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("pull_request", out var pullRequest))
            throw new ActionCommandException("GitHub event payload does not contain a pull_request object.");

        return new PullRequestScanInput(
            GetRequiredString(pullRequest, "base", "sha"),
            GetRequiredString(pullRequest, "head", "sha"),
            GetOptionalString(pullRequest, "title"),
            GetOptionalString(pullRequest, "body"));
    }

    private static string GetRequiredString(JsonElement element, string objectName, string propertyName)
    {
        if (!element.TryGetProperty(objectName, out var child) ||
            !child.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ActionCommandException($"GitHub event payload is missing pull_request.{objectName}.{propertyName}.");
        }
        return value.GetString()!;
    }

    private static string GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static bool HasExtension(string path, string extension) =>
        Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);

    internal static bool IsCSharpSource(string path) =>
        HasExtension(path, ".cs") || HasExtension(path, ".csx");

    private static void ScanText(
        string source,
        string text,
        UnicodeScanOptions options,
        List<UnicodeViolation> violations)
    {
        var lineStarts = BuildLineStarts(text);
        var offset = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            string? reason = null;
            if (!(options.AllowLeadingBom && offset == 0 && value == 0xFEFF))
            {
                if (IsFormat(value))
                    reason = "Unicode format character (Cf)";
                else if (IsDefaultIgnorable(value))
                    reason = "Unicode Default_Ignorable_Code_Point";
                else if (IsForbiddenControl(value))
                    reason = "forbidden control or line-separator character";
                else if (options.RejectNonAsciiSpace && IsNonAsciiSpace(rune))
                    reason = "non-ASCII space in a C# source file";
            }

            if (reason is not null)
            {
                var (line, column) = GetLineColumn(lineStarts, offset);
                violations.Add(new UnicodeViolation(source, line, column, value, "raw", reason));
            }
            offset += rune.Utf16SequenceLength;
        }

        if (!options.ScanCSharpEscapes)
            return;

        // C# language specification section 6.4.3 permits Unicode_Escape_Sequence in identifiers.
        // Consequently, rejecting only raw Unicode scalar values would still allow an identifier such as
        // "user" + backslash-u-200B + "Name". This scanner deliberately checks escapes everywhere in a
        // .cs/.csx file rather than implementing a C# lexer. Tests that need these values must construct them
        // numerically, for example with char.ConvertFromUtf32(0x200B).
        // https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/lexical-structure#643-identifiers
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != '\\')
                continue;

            var digitCount = text[i + 1] switch
            {
                'u' => 4,
                'U' => 8,
                _ => 0,
            };
            if (digitCount == 0 || i + 2 + digitCount > text.Length)
                continue;

            var value = 0;
            var valid = true;
            for (var j = 0; j < digitCount; j++)
            {
                var digit = HexValue(text[i + 2 + j]);
                if (digit < 0)
                {
                    valid = false;
                    break;
                }
                value = checked(value * 16 + digit);
            }
            if (!valid || !Rune.IsValid(value) || !(IsFormat(value) || IsDefaultIgnorable(value)))
                continue;

            var (line, column) = GetLineColumn(lineStarts, i);
            violations.Add(new UnicodeViolation(
                source,
                line,
                column,
                value,
                "C# Unicode escape",
                "escape resolves to a forbidden identifier character"));
            i += 1 + digitCount;
        }
    }

    private static int HexValue(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        >= 'A' and <= 'F' => value - 'A' + 10,
        _ => -1,
    };

    private static int[] BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts.Add(i + 1);
        }
        return starts.ToArray();
    }

    private static (int Line, int Column) GetLineColumn(int[] lineStarts, int offset)
    {
        var index = Array.BinarySearch(lineStarts, offset);
        if (index < 0)
            index = ~index - 1;
        return (index + 1, offset - lineStarts[index] + 1);
    }

    private static bool IsForbiddenControl(int value) => value is
        >= 0x0000 and <= 0x0008 or // C0 controls: NULL..BACKSPACE
        >= 0x000B and <= 0x000C or // VERTICAL TABULATION..FORM FEED
        >= 0x000E and <= 0x001F or // C0 controls: SHIFT OUT..INFORMATION SEPARATOR ONE
        >= 0x007F and <= 0x009F or // DELETE and C1 controls, including NEXT LINE (U+0085)
        0x2028 or                  // LINE SEPARATOR; C# treats it as a source line terminator
        0x2029;                    // PARAGRAPH SEPARATOR; C# treats it as a source line terminator

    private static bool IsNonAsciiSpace(Rune rune) =>
        rune.Value != 0x20 && Rune.GetUnicodeCategory(rune) == UnicodeCategory.SpaceSeparator;

    // C# language specification section 6.4.3 permits General_Category=Format (Cf) as an
    // Identifier_Part_Character. More importantly, C# removes every Formatting_Character when comparing
    // identifiers. A format character can therefore be inserted into an identifier without changing the
    // symbol to which it binds, while still changing or hiding how the source is rendered to a reviewer.
    // We reject every Unicode 17.0 Cf value, not only the bidi controls or Default Ignorable subset.
    // The ranges are fixed here so the security policy does not change with the .NET runtime's Unicode version.
    //
    // C# specification:
    // https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/lexical-structure#643-identifiers
    // Unicode 17.0 data:
    // https://www.unicode.org/Public/17.0.0/ucd/extracted/DerivedGeneralCategory.txt
    private static bool IsFormat(int value) => value is
        0x00AD or                    // SOFT HYPHEN
        >= 0x0600 and <= 0x0605 or   // ARABIC NUMBER SIGN..ARABIC NUMBER MARK ABOVE
        0x061C or                    // ARABIC LETTER MARK
        0x06DD or                    // ARABIC END OF AYAH
        0x070F or                    // SYRIAC ABBREVIATION MARK
        >= 0x0890 and <= 0x0891 or   // ARABIC POUND MARK ABOVE..ARABIC PIASTRE MARK ABOVE
        0x08E2 or                    // ARABIC DISPUTED END OF AYAH
        0x180E or                    // MONGOLIAN VOWEL SEPARATOR
        >= 0x200B and <= 0x200F or   // ZERO WIDTH SPACE..RIGHT-TO-LEFT MARK
        >= 0x202A and <= 0x202E or   // LEFT-TO-RIGHT EMBEDDING..RIGHT-TO-LEFT OVERRIDE
        >= 0x2060 and <= 0x2064 or   // WORD JOINER..INVISIBLE PLUS
        >= 0x2066 and <= 0x206F or   // LEFT-TO-RIGHT ISOLATE..NOMINAL DIGIT SHAPES
        0xFEFF or                    // ZERO WIDTH NO-BREAK SPACE; also used as a byte order mark
        >= 0xFFF9 and <= 0xFFFB or   // INTERLINEAR ANNOTATION ANCHOR..INTERLINEAR ANNOTATION TERMINATOR
        0x110BD or                   // KAITHI NUMBER SIGN
        0x110CD or                   // KAITHI NUMBER SIGN ABOVE
        >= 0x13430 and <= 0x1343F or // EGYPTIAN HIEROGLYPH VERTICAL JOINER..END WALLED ENCLOSURE
        >= 0x1BCA0 and <= 0x1BCA3 or // SHORTHAND FORMAT LETTER OVERLAP..SHORTHAND FORMAT UP STEP
        >= 0x1D173 and <= 0x1D17A or // MUSICAL SYMBOL BEGIN BEAM..MUSICAL SYMBOL END PHRASE
        0xE0001 or                   // LANGUAGE TAG
        >= 0xE0020 and <= 0xE007F;   // TAG SPACE..CANCEL TAG

    // C# also permits Letter (including Lo) at the start of an identifier and Mn/Mc combining characters
    // after the first character. Default Ignorable characters in those categories are not removed during
    // C# identifier comparison, so values such as HANGUL FILLER (Lo) or VARIATION SELECTOR-16 (Mn) can create
    // a genuinely different identifier which appears empty or indistinguishable from another identifier.
    // Unicode UAX #31 therefore defines a Default-Ignorable Exclusion Profile for identifiers. We apply that
    // exclusion to source text as well as identifiers, and include reserved Default Ignorable values so a
    // future Unicode assignment cannot silently weaken the policy.
    //
    // Unicode 17.0 DerivedCoreProperties.txt:
    // https://www.unicode.org/Public/17.0.0/ucd/DerivedCoreProperties.txt
    // Unicode UAX #31, Default-Ignorable Exclusion Profile:
    // https://www.unicode.org/reports/tr31/#Default_Ignorable_Exclusion_Profile
    private static bool IsDefaultIgnorable(int value) => value is
        0x00AD or                    // SOFT HYPHEN (Cf)
        0x034F or                    // COMBINING GRAPHEME JOINER (Mn)
        0x061C or                    // ARABIC LETTER MARK (Cf)
        >= 0x115F and <= 0x1160 or   // HANGUL CHOSEONG FILLER..HANGUL JUNGSEONG FILLER (Lo)
        >= 0x17B4 and <= 0x17B5 or   // KHMER VOWEL INHERENT AQ..KHMER VOWEL INHERENT AA (Mn)
        >= 0x180B and <= 0x180D or   // MONGOLIAN FREE VARIATION SELECTOR ONE..THREE (Mn)
        0x180E or                    // MONGOLIAN VOWEL SEPARATOR (Cf)
        0x180F or                    // MONGOLIAN FREE VARIATION SELECTOR FOUR (Mn)
        >= 0x200B and <= 0x200F or   // ZERO WIDTH SPACE..RIGHT-TO-LEFT MARK (Cf)
        >= 0x202A and <= 0x202E or   // LEFT-TO-RIGHT EMBEDDING..RIGHT-TO-LEFT OVERRIDE (Cf)
        >= 0x2060 and <= 0x2064 or   // WORD JOINER..INVISIBLE PLUS (Cf)
        0x2065 or                    // Reserved Default Ignorable code point
        >= 0x2066 and <= 0x206F or   // LEFT-TO-RIGHT ISOLATE..NOMINAL DIGIT SHAPES (Cf)
        0x3164 or                    // HANGUL FILLER (Lo)
        >= 0xFE00 and <= 0xFE0F or   // VARIATION SELECTOR-1..VARIATION SELECTOR-16 (Mn)
        0xFEFF or                    // ZERO WIDTH NO-BREAK SPACE (Cf)
        0xFFA0 or                    // HALFWIDTH HANGUL FILLER (Lo)
        >= 0xFFF0 and <= 0xFFF8 or   // Reserved Default Ignorable code points
        >= 0x1BCA0 and <= 0x1BCA3 or // SHORTHAND FORMAT LETTER OVERLAP..SHORTHAND FORMAT UP STEP (Cf)
        >= 0x1D173 and <= 0x1D17A or // MUSICAL SYMBOL BEGIN BEAM..MUSICAL SYMBOL END PHRASE (Cf)
        0xE0000 or                   // Reserved Default Ignorable code point
        0xE0001 or                   // LANGUAGE TAG (Cf)
        >= 0xE0002 and <= 0xE001F or // Reserved tag-area Default Ignorable code points
        >= 0xE0020 and <= 0xE007F or // TAG SPACE..CANCEL TAG (Cf)
        >= 0xE0080 and <= 0xE00FF or // Reserved tag-area Default Ignorable code points
        >= 0xE0100 and <= 0xE01EF or // VARIATION SELECTOR-17..VARIATION SELECTOR-256 (Mn)
        >= 0xE01F0 and <= 0xE0FFF;   // Reserved variation-selector-area Default Ignorable code points

    private static void WriteAnnotation(UnicodeViolation violation)
    {
        var message = $"{violation.Rule}: U+{violation.CodePoint:X4}; {violation.Reason}";
        if (violation.Line > 0)
        {
            Console.WriteLine(
                $"::error file={EscapeProperty(violation.Source)},line={violation.Line},col={violation.Column},title=Forbidden Unicode::{EscapeData(message)}");
        }
        else
        {
            Console.WriteLine($"::error title=Unicode scan failed::{EscapeData($"{violation.Source}: {violation.Reason}")}");
        }
    }

    private static string EscapeProperty(string value) =>
        EscapeData(value).Replace(":", "%3A", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal);

    private static string EscapeData(string value) => value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("\r", "%0D", StringComparison.Ordinal)
        .Replace("\n", "%0A", StringComparison.Ordinal);

    private readonly record struct UnicodeScanOptions(
        bool AllowLeadingBom,
        bool ScanCSharpEscapes,
        bool RejectNonAsciiSpace)
    {
        public static UnicodeScanOptions Metadata => new(false, false, false);
        public static UnicodeScanOptions FileName => new(false, false, true);
    }
}

public readonly record struct PullRequestScanInput(string BaseSha, string HeadSha, string Title, string Body);

public sealed record PrChangedFile(
    string Path,
    string? OldPath,
    byte[] Content,
    bool IsGitLink = false,
    long? DeclaredSize = null);

public readonly record struct UnicodeViolation(
    string Source,
    int Line,
    int Column,
    int CodePoint,
    string Rule,
    string Reason)
{
    public static UnicodeViolation FileError(string source, string reason) => new(source, 0, 0, 0, "file", reason);
}

public interface IPrChangeSource
{
    Task<IReadOnlyList<PrChangedFile>> ReadChangedFilesAsync(
        string repositoryPath,
        string baseSha,
        string headSha,
        CancellationToken cancellationToken = default);
}

internal sealed class GitPrChangeSource : IPrChangeSource
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<IReadOnlyList<PrChangedFile>> ReadChangedFilesAsync(
        string repositoryPath,
        string baseSha,
        string headSha,
        CancellationToken cancellationToken = default)
    {
        ValidateSha(baseSha);
        ValidateSha(headSha);
        await RunGitAsync(repositoryPath, ["cat-file", "-e", $"{baseSha}^{{commit}}"], cancellationToken);
        await RunGitAsync(repositoryPath, ["cat-file", "-e", $"{headSha}^{{commit}}"], cancellationToken);

        var diff = await RunGitAsync(
            repositoryPath,
            ["diff", "--name-status", "-z", "--find-renames", baseSha, headSha, "--"],
            cancellationToken);
        var fields = SplitNullTerminated(diff);
        var files = new List<PrChangedFile>();
        for (var i = 0; i < fields.Count;)
        {
            var status = DecodeUtf8(fields[i++], "git diff status");
            if (status.Length == 0)
                continue;

            string? oldPath = null;
            string path;
            if (status[0] is 'R' or 'C')
            {
                oldPath = DecodeUtf8(RequiredField(fields, ref i, status), "old Git path");
                path = DecodeUtf8(RequiredField(fields, ref i, status), "new Git path");
            }
            else
            {
                path = DecodeUtf8(RequiredField(fields, ref i, status), "Git path");
            }

            if (status[0] == 'D')
                continue;

            var tree = await ReadTreeEntryAsync(repositoryPath, headSha, path, cancellationToken);
            if (tree.Type == "commit")
            {
                files.Add(new PrChangedFile(path, oldPath, [], IsGitLink: true));
                continue;
            }
            if (tree.Type != "blob")
                throw new ActionCommandException($"Changed path '{path}' is not a Git blob or gitlink.");

            // Filenames are scanned separately, but only C# source contents are in policy scope.
            // Do not load unrelated blobs merely to discover that Scan() will skip them.
            if (!ScanPrUnicodeCommand.IsCSharpSource(path))
            {
                files.Add(new PrChangedFile(path, oldPath, []));
                continue;
            }

            if (tree.Size > ScanPrUnicodeCommand.MaxFileBytes)
            {
                files.Add(new PrChangedFile(path, oldPath, [], DeclaredSize: tree.Size));
                continue;
            }

            var content = await RunGitAsync(repositoryPath, ["cat-file", "blob", tree.ObjectId], cancellationToken);
            files.Add(new PrChangedFile(path, oldPath, content, DeclaredSize: tree.Size));
        }
        return files;
    }

    private static async Task<GitTreeEntry> ReadTreeEntryAsync(
        string repositoryPath,
        string headSha,
        string path,
        CancellationToken cancellationToken)
    {
        var output = await RunGitAsync(
            repositoryPath,
            ["ls-tree", "-lz", headSha, "--", $":(literal){path}"],
            cancellationToken);
        var entries = SplitNullTerminated(output);
        if (entries.Count != 1)
            throw new ActionCommandException($"Could not resolve changed path '{path}' in head commit.");

        var headerAndPath = entries[0];
        var tab = Array.IndexOf(headerAndPath, (byte)'\t');
        if (tab < 0)
            throw new ActionCommandException($"Invalid git ls-tree output for '{path}'.");
        var header = StrictUtf8.GetString(headerAndPath, 0, tab).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (header.Length != 4)
            throw new ActionCommandException($"Invalid git ls-tree header for '{path}'.");
        var size = header[3] == "-"
            ? 0
            : long.TryParse(header[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSize)
                ? parsedSize
                : throw new ActionCommandException($"Invalid git blob size for '{path}'.");
        return new GitTreeEntry(header[1], header[2], size);
    }

    private static byte[] RequiredField(IReadOnlyList<byte[]> fields, ref int index, string status)
    {
        if (index >= fields.Count)
            throw new ActionCommandException($"Invalid git diff --name-status output after status '{status}'.");
        return fields[index++];
    }

    private static List<byte[]> SplitNullTerminated(byte[] value)
    {
        var result = new List<byte[]>();
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != 0)
                continue;
            result.Add(value[start..i]);
            start = i + 1;
        }
        if (start != value.Length)
            throw new ActionCommandException("Git returned non-NUL-terminated path output.");
        return result;
    }

    private static string DecodeUtf8(byte[] value, string description)
    {
        try
        {
            return StrictUtf8.GetString(value);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ActionCommandException($"{description} is not valid UTF-8.", exception);
        }
    }

    private static void ValidateSha(string sha)
    {
        if (sha.Length != 40 || sha.Any(x => !Uri.IsHexDigit(x)))
            throw new ActionCommandException("Pull request base/head SHA must be a 40-character hexadecimal Git object ID.");
    }

    private static async Task<byte[]> RunGitAsync(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path.GetFullPath(repositoryPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new ActionCommandException("Failed to start git.");
        await using var output = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cancellationToken));
        if (process.ExitCode != 0)
        {
            var stderr = await errorTask;
            throw new ActionCommandException($"git {arguments[0]} failed with exit code {process.ExitCode}: {stderr.Trim()}");
        }
        return output.ToArray();
    }

    private readonly record struct GitTreeEntry(string Type, string ObjectId, long Size);
}
