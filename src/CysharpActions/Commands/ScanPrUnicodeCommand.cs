using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CysharpActions.Utils;

namespace CysharpActions.Commands;

public sealed class ScanPrUnicodeCommand(IPrChangeSource? changeSource = null)
{
    internal const long MaxFileBytes = 10 * 1024 * 1024;
    internal const long MaxTotalTextBytes = 100 * 1024 * 1024;
    private const int MaxAnnotations = 50;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IPrChangeSource changeSource = changeSource ?? new GitPrChangeSource();

    public async Task ValidateAsync(
        string eventPath,
        string repositoryPath = ".",
        CancellationToken cancellationToken = default)
    {
        var input = await ReadPullRequestAsync(eventPath, cancellationToken);
        var state = new ScanState(MaxAnnotations);
        ScanMetadata(input, state);

        var fileCount = await changeSource.VisitChangedFilesAsync(
            repositoryPath,
            input.BaseSha,
            input.HeadSha,
            (file, content, token) => ScanFileAsync(file, content, state, token),
            cancellationToken);

        foreach (var violation in state.Violations)
        {
            WriteAnnotation(violation);
        }
        if (state.ViolationCount > MaxAnnotations)
        {
            GitHubActions.WriteLog(
                $"Unicode scan found {state.ViolationCount} violations. Only the first {MaxAnnotations} were annotated.");
        }
        if (state.ViolationCount != 0)
        {
            throw new ActionCommandException(
                $"PR Unicode security scan found {state.ViolationCount} violation(s).");
        }

        GitHubActions.WriteLog(
            $"PR Unicode security scan passed. Scanned {fileCount} changed non-deleted file(s).");
    }

    public static IReadOnlyList<UnicodeViolation> Scan(
        PullRequestScanInput input,
        IReadOnlyList<PrChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var state = new ScanState(MaxAnnotations);
        ScanMetadata(input, state);

        foreach (var file in files)
        {
            var disposition = PrepareFile(file, state);
            if (disposition == FileScanDisposition.Stop)
                break;
            if (disposition == FileScanDisposition.ScanContent)
                ScanCSharpBytes(file.Path, file.Content, state);
        }

        return state.Violations;
    }

    private static void ScanMetadata(PullRequestScanInput input, ScanState state)
    {
        ScanText("PR title", input.Title, UnicodeScanOptions.Metadata, state);
        ScanText("PR body", input.Body, UnicodeScanOptions.Metadata, state);
    }

    private static FileScanDisposition PrepareFile(PrChangedFile file, ScanState state)
    {
        ScanText(file.Path, file.Path, UnicodeScanOptions.FileName, state);
        if (file.OldPath is not null)
        {
            ScanText(file.OldPath, file.OldPath, UnicodeScanOptions.FileName, state);
        }

        if (file.PreScanError is not null)
        {
            state.Add(UnicodeViolation.FileError(file.Path, file.PreScanError));
            return FileScanDisposition.Stop;
        }

        if (file.IsSymbolicLink)
        {
            if (IsCSharpSource(file.Path))
            {
                state.Add(UnicodeViolation.FileError(
                    file.Path,
                    "C# source file must not be a symbolic link."));
            }
            return FileScanDisposition.Skip;
        }

        if (file.IsGitLink || !IsCSharpSource(file.Path))
            return FileScanDisposition.Skip;

        var contentLength = file.DeclaredSize ?? file.Content.LongLength;
        if (contentLength > MaxFileBytes)
        {
            state.Add(UnicodeViolation.FileError(
                file.Path,
                $"C# source file exceeds the {MaxFileBytes / 1024 / 1024} MiB scan limit."));
            return FileScanDisposition.Skip;
        }

        // GitPrChangeSource checks this from tree metadata before loading a blob. Keep the same
        // guard here for tests and alternative IPrChangeSource implementations.
        if (state.TotalTextBytes > MaxTotalTextBytes - contentLength)
        {
            state.Add(UnicodeViolation.FileError(
                file.Path,
                $"Changed C# source exceeds the {MaxTotalTextBytes / 1024 / 1024} MiB total scan limit."));
            return FileScanDisposition.Stop;
        }
        state.TotalTextBytes += contentLength;
        return FileScanDisposition.ScanContent;
    }

    private static async Task<bool> ScanFileAsync(
        PrChangedFile file,
        Stream? content,
        ScanState state,
        CancellationToken cancellationToken)
    {
        var disposition = PrepareFile(file, state);
        if (disposition != FileScanDisposition.ScanContent)
            return disposition != FileScanDisposition.Stop;
        if (content is null)
            throw new ActionCommandException($"Changed C# source '{file.Path}' has no blob stream.");

        const int bufferSize = 64 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var scanner = new Utf8ContentScanner(file.Path, state);
            var valid = true;
            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken);
                if (read == 0)
                    break;
                if (!valid)
                    continue; // Drain git stdout so cat-file cannot block while exiting.
                if (buffer.AsSpan(0, read).Contains((byte)0))
                {
                    state.Add(UnicodeViolation.FileError(file.Path, "C# source file contains a NUL byte."));
                    valid = false;
                    continue;
                }
                try
                {
                    scanner.Append(buffer.AsSpan(0, read));
                }
                catch (DecoderFallbackException)
                {
                    state.Add(UnicodeViolation.FileError(file.Path, "C# source file is not valid UTF-8."));
                    valid = false;
                }
            }
            if (valid)
            {
                try
                {
                    scanner.Complete();
                }
                catch (DecoderFallbackException)
                {
                    state.Add(UnicodeViolation.FileError(file.Path, "C# source file is not valid UTF-8."));
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return true;
    }

    private static void ScanCSharpBytes(string source, ReadOnlySpan<byte> content, ScanState state)
    {
        if (content.Contains((byte)0))
        {
            state.Add(UnicodeViolation.FileError(source, "C# source file contains a NUL byte."));
            return;
        }
        try
        {
            var scanner = new Utf8ContentScanner(source, state);
            scanner.Append(content);
            scanner.Complete();
        }
        catch (DecoderFallbackException)
        {
            state.Add(UnicodeViolation.FileError(source, "C# source file is not valid UTF-8."));
        }
    }

    internal static IReadOnlyList<UnicodeViolation> ScanCSharpChunksForTest(
        params ReadOnlyMemory<byte>[] chunks)
    {
        var state = new ScanState(int.MaxValue);
        var scanner = new Utf8ContentScanner("Test.cs", state);
        foreach (var chunk in chunks)
            scanner.Append(chunk.Span);
        scanner.Complete();
        return state.Violations;
    }

    private enum FileScanDisposition
    {
        Skip,
        ScanContent,
        Stop,
    }

    private sealed class Utf8ContentScanner
    {
        private readonly Decoder decoder = StrictUtf8.GetDecoder();
        private readonly char[] characters = new char[4096];
        private readonly CSharpTextScanner scanner;

        public Utf8ContentScanner(string source, ScanState state) => scanner = new(source, state);

        public void Append(ReadOnlySpan<byte> bytes) => Convert(bytes, flush: false);

        public void Complete()
        {
            Convert([], flush: true);
            scanner.Complete();
        }

        private void Convert(ReadOnlySpan<byte> bytes, bool flush)
        {
            do
            {
                decoder.Convert(
                    bytes,
                    characters,
                    flush,
                    out var bytesUsed,
                    out var charactersUsed,
                    out var completed);
                scanner.Append(characters.AsSpan(0, charactersUsed));
                bytes = bytes[bytesUsed..];
                if (completed)
                    break;
            }
            while (!bytes.IsEmpty || flush);
        }
    }

    private sealed class CSharpTextScanner(string source, ScanState state)
    {
        private const int EscapeWindowSize = 10; // backslash + U + eight hexadecimal digits
        private readonly char[] escapeCharacters = new char[EscapeWindowSize];
        private readonly int[] escapeLines = new int[EscapeWindowSize];
        private readonly int[] escapeColumns = new int[EscapeWindowSize];
        private int escapeCount;
        private int escapeStart;
        private int line = 1;
        private int column = 1;
        private bool previousWasCarriageReturn;
        private bool isFirstScalar = true;
        private char? pendingHighSurrogate;
        private int pendingHighLine;
        private int pendingHighColumn;

        public void Append(ReadOnlySpan<char> characters)
        {
            foreach (var character in characters)
            {
                AddEscapeCharacter(character, line, column);
                ScanRawCharacter(character, line, column);
                AdvancePosition(character);
            }
        }

        public void Complete()
        {
            if (pendingHighSurrogate is not null)
                throw new DecoderFallbackException("UTF-8 decoder produced an incomplete surrogate pair.");
        }

        private void ScanRawCharacter(char character, int characterLine, int characterColumn)
        {
            if (pendingHighSurrogate is char high)
            {
                if (!char.IsLowSurrogate(character))
                    throw new DecoderFallbackException("UTF-8 decoder produced an invalid surrogate pair.");
                ScanRawRune(new Rune(high, character), pendingHighLine, pendingHighColumn);
                pendingHighSurrogate = null;
                return;
            }

            if (char.IsHighSurrogate(character))
            {
                pendingHighSurrogate = character;
                pendingHighLine = characterLine;
                pendingHighColumn = characterColumn;
                return;
            }
            if (char.IsLowSurrogate(character))
                throw new DecoderFallbackException("UTF-8 decoder produced an unexpected low surrogate.");

            ScanRawRune(new Rune(character), characterLine, characterColumn);
        }

        private void ScanRawRune(Rune rune, int runeLine, int runeColumn)
        {
            var value = rune.Value;
            string? reason = null;
            if (!(isFirstScalar && value == 0xFEFF))
            {
                if (IsFormat(value))
                    reason = "Unicode format character (Cf)";
                else if (IsDefaultIgnorable(value))
                    reason = "Unicode Default_Ignorable_Code_Point";
                else if (IsForbiddenControl(value))
                    reason = "forbidden control or line-separator character";
                else if (IsNonAsciiSpace(rune))
                    reason = "non-ASCII space in a C# source file";
            }
            isFirstScalar = false;

            if (reason is not null)
                state.Add(new UnicodeViolation(source, runeLine, runeColumn, value, "raw", reason));
        }

        private void AddEscapeCharacter(char character, int characterLine, int characterColumn)
        {
            if (escapeCount == EscapeWindowSize)
            {
                escapeStart = (escapeStart + 1) % EscapeWindowSize;
                escapeCount--;
            }
            var index = (escapeStart + escapeCount) % EscapeWindowSize;
            escapeCharacters[index] = character;
            escapeLines[index] = characterLine;
            escapeColumns[index] = characterColumn;
            escapeCount++;

            CheckEscape(4, 'u');
            CheckEscape(8, 'U');
        }

        // C# language specification section 6.4.3 permits Unicode_Escape_Sequence in identifiers.
        // Rejecting only decoded scalar values would therefore miss an identifier containing, for example,
        // backslash-u-200B. Keep the ten-character suffix across chunks and inspect escapes everywhere in
        // .cs/.csx source; this intentionally does not depend on whether the escape is in code, a string, or a
        // comment, because deceptive invisible text is forbidden throughout source files.
        // https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/lexical-structure#643-identifiers
        private void CheckEscape(int digitCount, char marker)
        {
            var length = digitCount + 2;
            if (escapeCount < length || EscapeAt(escapeCount - length) != '\\' ||
                EscapeAt(escapeCount - length + 1) != marker)
            {
                return;
            }

            var value = 0;
            for (var i = 0; i < digitCount; i++)
            {
                var digit = HexValue(EscapeAt(escapeCount - digitCount + i));
                if (digit < 0)
                    return;
                value = checked(value * 16 + digit);
            }
            if (!Rune.IsValid(value) || !(IsFormat(value) || IsDefaultIgnorable(value)))
                return;

            var start = (escapeStart + escapeCount - length) % EscapeWindowSize;
            state.Add(new UnicodeViolation(
                source,
                escapeLines[start],
                escapeColumns[start],
                value,
                "C# Unicode escape",
                "escape resolves to a forbidden identifier character"));
        }

        private char EscapeAt(int relativeIndex) =>
            escapeCharacters[(escapeStart + relativeIndex) % EscapeWindowSize];

        private void AdvancePosition(char character)
        {
            if (character == '\r')
            {
                line++;
                column = 1;
                previousWasCarriageReturn = true;
            }
            else if (character == '\n')
            {
                if (!previousWasCarriageReturn)
                    line++;
                column = 1;
                previousWasCarriageReturn = false;
            }
            else if (character is '\u0085' or '\u2028' or '\u2029')
            {
                line++;
                column = 1;
                previousWasCarriageReturn = false;
            }
            else
            {
                column++;
                previousWasCarriageReturn = false;
            }
        }
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
        ScanState state)
    {
        var lineStarts = BuildLineStarts(text);
        var offset = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            string? reason = null;
            if (IsFormat(value))
                reason = "Unicode format character (Cf)";
            else if (IsDefaultIgnorable(value))
                reason = "Unicode Default_Ignorable_Code_Point";
            else if (IsForbiddenControl(value))
                reason = "forbidden control or line-separator character";
            else if (options.RejectNonAsciiSpace && IsNonAsciiSpace(rune))
                reason = "non-ASCII space in a C# source file";

            if (reason is not null)
            {
                var (line, column) = GetLineColumn(lineStarts, offset);
                state.Add(new UnicodeViolation(source, line, column, value, "raw", reason));
            }
            offset += rune.Utf16SequenceLength;
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

    private sealed class ScanState(int maxRetainedViolations)
    {
        public List<UnicodeViolation> Violations { get; } = [];

        public int ViolationCount { get; private set; }

        public long TotalTextBytes { get; set; }

        public void Add(UnicodeViolation violation)
        {
            ViolationCount++;
            if (Violations.Count < maxRetainedViolations)
            {
                Violations.Add(violation);
            }
        }
    }

    private readonly record struct UnicodeScanOptions(bool RejectNonAsciiSpace)
    {
        public static UnicodeScanOptions Metadata => new(false);
        public static UnicodeScanOptions FileName => new(true);
    }
}

public readonly record struct PullRequestScanInput(string BaseSha, string HeadSha, string Title, string Body);

public sealed record PrChangedFile(
    string Path,
    string? OldPath,
    byte[] Content,
    bool IsGitLink = false,
    bool IsSymbolicLink = false,
    long? DeclaredSize = null,
    string? PreScanError = null);

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
    Task<int> VisitChangedFilesAsync(
        string repositoryPath,
        string baseSha,
        string headSha,
        Func<PrChangedFile, Stream?, CancellationToken, Task<bool>> visitor,
        CancellationToken cancellationToken = default);
}

internal sealed class GitPrChangeSource(
    long maxTotalTextBytes = ScanPrUnicodeCommand.MaxTotalTextBytes) : IPrChangeSource
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<int> VisitChangedFilesAsync(
        string repositoryPath,
        string baseSha,
        string headSha,
        Func<PrChangedFile, Stream?, CancellationToken, Task<bool>> visitor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateSha(baseSha);
        ValidateSha(headSha);
        await RunGitAsync(repositoryPath, ["cat-file", "-e", $"{baseSha}^{{commit}}"], cancellationToken);
        await RunGitAsync(repositoryPath, ["cat-file", "-e", $"{headSha}^{{commit}}"], cancellationToken);

        var diff = await RunGitAsync(
            repositoryPath,
            ["diff", "--name-status", "-z", "--find-renames", baseSha, headSha, "--"],
            cancellationToken);
        var fields = SplitNullTerminated(diff);
        long totalTextBytes = 0;
        var fileCount = 0;
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

            fileCount++;

            // Non-C# contents are outside policy. Report the names without spawning ls-tree/cat-file.
            if (!ScanPrUnicodeCommand.IsCSharpSource(path))
            {
                if (!await visitor(new PrChangedFile(path, oldPath, []), null, cancellationToken))
                    return fileCount;
                continue;
            }

            var tree = await ReadTreeEntryAsync(repositoryPath, headSha, path, cancellationToken);
            if (tree.Mode == "120000")
            {
                // A Git symbolic link is stored as a blob whose content is only the target path. Scanning
                // that blob would inspect "payload.txt", while a Linux build opening Link.cs would read the
                // target file. Never follow an untrusted link.
                if (!await visitor(
                    new PrChangedFile(path, oldPath, [], IsSymbolicLink: true),
                    null,
                    cancellationToken))
                {
                    return fileCount;
                }
                continue;
            }
            if (tree.Type == "commit")
            {
                if (!await visitor(
                    new PrChangedFile(path, oldPath, [], IsGitLink: true),
                    null,
                    cancellationToken))
                {
                    return fileCount;
                }
                continue;
            }
            if (tree.Type != "blob")
                throw new ActionCommandException($"Changed path '{path}' is not a Git blob or gitlink.");

            if (tree.Size > ScanPrUnicodeCommand.MaxFileBytes)
            {
                if (!await visitor(
                    new PrChangedFile(path, oldPath, [], DeclaredSize: tree.Size),
                    null,
                    cancellationToken))
                {
                    return fileCount;
                }
                continue;
            }

            if (totalTextBytes > maxTotalTextBytes - tree.Size)
            {
                await visitor(
                    new PrChangedFile(
                        path,
                        oldPath,
                        [],
                        DeclaredSize: tree.Size,
                        PreScanError:
                            $"Changed C# source exceeds the {maxTotalTextBytes / 1024 / 1024} MiB total scan limit."),
                    null,
                    cancellationToken);
                return fileCount;
            }
            totalTextBytes += tree.Size;

            var file = new PrChangedFile(path, oldPath, [], DeclaredSize: tree.Size);
            if (!await VisitGitBlobAsync(
                repositoryPath,
                tree.ObjectId,
                file,
                visitor,
                cancellationToken))
            {
                return fileCount;
            }
        }

        return fileCount;
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
        return new GitTreeEntry(header[0], header[1], header[2], size);
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

    private static async Task<bool> VisitGitBlobAsync(
        string repositoryPath,
        string objectId,
        PrChangedFile file,
        Func<PrChangedFile, Stream?, CancellationToken, Task<bool>> visitor,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path.GetFullPath(repositoryPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("cat-file");
        startInfo.ArgumentList.Add("blob");
        startInfo.ArgumentList.Add(objectId);

        using var process = Process.Start(startInfo) ?? throw new ActionCommandException("Failed to start git.");
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            var shouldContinue = await visitor(
                file,
                process.StandardOutput.BaseStream,
                cancellationToken);
            // A visitor is expected to consume the stream, but draining here also makes the process lifecycle
            // safe for alternative IPrChangeSource consumers which intentionally stop inspecting early.
            await process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new ActionCommandException(
                    $"git cat-file failed with exit code {process.ExitCode}: {(await errorTask).Trim()}");
            }
            return shouldContinue;
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            throw;
        }
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

    private readonly record struct GitTreeEntry(string Mode, string Type, string ObjectId, long Size);
}
