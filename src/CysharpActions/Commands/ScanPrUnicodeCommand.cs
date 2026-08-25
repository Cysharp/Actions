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
    internal const int MaxChangedFiles = 3000;
    internal const int MaxDiffBytes = 16 * 1024 * 1024;
    internal const int MaxTrackedCSharpListBytes = 16 * 1024 * 1024;
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
            GitHubActions.WriteLog($"Unicode scan found {state.ViolationCount} violations. Only the first {MaxAnnotations} were annotated.");
        }
        if (state.ViolationCount != 0)
        {
            throw new ActionCommandException($"PR Unicode security scan found {state.ViolationCount} violation(s).");
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
                state.Add(UnicodeViolation.FileError(file.Path, "C# source file must not be a symbolic link."));
            }
            return FileScanDisposition.Skip;
        }

        if (file.IsGitLink || !IsCSharpSource(file.Path))
            return FileScanDisposition.Skip;

        var contentLength = file.DeclaredSize ?? file.Content.LongLength;
        if (contentLength > MaxFileBytes)
        {
            state.Add(UnicodeViolation.FileError(file.Path, $"C# source file exceeds the {MaxFileBytes / 1024 / 1024} MiB scan limit."));
            return FileScanDisposition.Skip;
        }

        // GitPrChangeSource checks this from tree metadata before loading a blob. Keep the same
        // guard here for tests and alternative IPrChangeSource implementations.
        if (state.TotalTextBytes > MaxTotalTextBytes - contentLength)
        {
            state.Add(UnicodeViolation.FileError(file.Path, $"Changed C# source exceeds the {MaxTotalTextBytes / 1024 / 1024} MiB total scan limit."));
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
                if (buffer.AsSpan(0, read).Contains((byte)0))
                {
                    state.Add(UnicodeViolation.FileError(file.Path, "C# source file contains a NUL byte."));
                    valid = false;
                    break;
                }
                try
                {
                    scanner.Append(buffer.AsSpan(0, read));
                }
                catch (DecoderFallbackException)
                {
                    state.Add(UnicodeViolation.FileError(file.Path, "C# source file is not valid UTF-8."));
                    valid = false;
                    break;
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
                decoder.Convert(bytes, characters, flush, out var bytesUsed, out var charactersUsed, out var completed);
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

            uint value = 0;
            for (var i = 0; i < digitCount; i++)
            {
                var digit = HexValue(EscapeAt(escapeCount - digitCount + i));
                if (digit < 0)
                    return;
                // Eight hexadecimal digits fit exactly in UInt32, including values such as FFFFFFFF.
                // Check the Unicode scalar range before converting to Int32 so hostile escapes cannot
                // overflow the scanner itself.
                value = value * 16 + (uint)digit;
            }
            if (value > 0x10FFFF)
                return;
            var scalarValue = (int)value;
            if (!Rune.IsValid(scalarValue) || !(IsFormat(scalarValue) || IsDefaultIgnorable(scalarValue)))
                return;

            var start = (escapeStart + escapeCount - length) % EscapeWindowSize;
            state.Add(new UnicodeViolation(source, escapeLines[start], escapeColumns[start], scalarValue, "C# Unicode escape", "escape resolves to a forbidden identifier character"));
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
        Console.WriteLine(FormatAnnotation(violation));
    }

    internal static string FormatAnnotation(UnicodeViolation violation)
    {
        var source = VisualizeUnsafeAnnotationCharacters(violation.Source);
        var message = VisualizeUnsafeAnnotationCharacters(
            $"{violation.Rule}: U+{violation.CodePoint:X4}; {violation.Reason}");
        if (violation.Line > 0)
        {
            return $"::error file={EscapeProperty(source)},line={violation.Line},col={violation.Column},title=Forbidden Unicode::{EscapeData(message)}";
        }

        var failure = VisualizeUnsafeAnnotationCharacters($"{violation.Source}: {violation.Reason}");
        return $"::error title=Unicode scan failed::{EscapeData(failure)}";
    }

    // Human-readable visualization and GitHub workflow-command escaping solve different problems.
    // First make attacker-controlled controls and invisible Unicode explicit in logs, then let
    // EscapeProperty/EscapeData protect the workflow-command syntax itself.
    private static string VisualizeUnsafeAnnotationCharacters(string value)
    {
        StringBuilder? builder = null;
        var copiedLength = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var codePoint = rune.Value;
            var mustVisualize = codePoint is >= 0x0000 and <= 0x001F or >= 0x007F and <= 0x009F ||
                IsForbiddenControl(codePoint) || IsFormat(codePoint) || IsDefaultIgnorable(codePoint);
            if (!mustVisualize)
            {
                if (builder is not null)
                    builder.Append(rune.ToString());
                copiedLength += rune.Utf16SequenceLength;
                continue;
            }

            builder ??= new StringBuilder(value.Length + 16).Append(value.AsSpan(0, copiedLength));
            builder.Append(codePoint <= 0xFFFF ? $"\\u{codePoint:X4}" : $"\\U{codePoint:X8}");
            copiedLength += rune.Utf16SequenceLength;
        }

        return builder?.ToString() ?? value;
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
    long maxTotalTextBytes = ScanPrUnicodeCommand.MaxTotalTextBytes,
    int maxChangedFiles = ScanPrUnicodeCommand.MaxChangedFiles,
    int maxDiffBytes = ScanPrUnicodeCommand.MaxDiffBytes) : IPrChangeSource
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

        var trackedSymlink = await FindTrackedCSharpSymlinkAsync(repositoryPath, cancellationToken);
        if (trackedSymlink is not null)
        {
            await visitor(
                new PrChangedFile(trackedSymlink, null, [], IsSymbolicLink: true),
                null,
                cancellationToken);
            return 0;
        }

        var diff = await RunGitAsync(
            repositoryPath,
            ["diff", "--raw", "-z", "--find-renames", baseSha, headSha, "--"],
            maxDiffBytes,
            "Git diff output",
            cancellationToken);
        // A rename/copy uses three NUL-terminated fields (header, old path, new path).
        // Bound field allocation separately from raw byte size so many empty/short paths cannot amplify memory.
        var fields = SplitNullTerminated(diff, ScanPrUnicodeCommand.MaxChangedFiles * 3);
        var repositoryRoot = Path.GetFullPath(repositoryPath);
        long totalTextBytes = 0;
        var fileCount = 0;
        var changedFileCount = 0;
        for (var i = 0; i < fields.Count;)
        {
            var header = ParseRawDiffHeader(DecodeUtf8(fields[i++], "git diff raw header"));

            string? oldPath = null;
            string path;
            if (header.Status is 'R' or 'C')
            {
                oldPath = DecodeUtf8(RequiredField(fields, ref i, header.Status), "old Git path");
                path = DecodeUtf8(RequiredField(fields, ref i, header.Status), "new Git path");
            }
            else
            {
                path = DecodeUtf8(RequiredField(fields, ref i, header.Status), "Git path");
            }

            changedFileCount++;
            if (changedFileCount > maxChangedFiles)
            {
                throw new ActionCommandException(
                    $"Pull request changes more than the {maxChangedFiles} file scan limit.");
            }

            if (header.Status == 'D')
                continue;

            fileCount++;

            // Non-C# contents are outside policy. Report their names without touching the working-tree file.
            if (!ScanPrUnicodeCommand.IsCSharpSource(path))
            {
                if (!await visitor(new PrChangedFile(path, oldPath, []), null, cancellationToken))
                    return fileCount;
                continue;
            }

            // Read mode from the single raw diff rather than following an untrusted working-tree link.
            // This also works on checkout configurations which materialize a Git symlink as a regular file.
            if (header.NewMode == "120000")
            {
                if (!await visitor(
                    new PrChangedFile(path, oldPath, [], IsSymbolicLink: true),
                    null,
                    cancellationToken))
                {
                    return fileCount;
                }
                continue;
            }
            if (header.NewMode == "160000")
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

            var fullPath = ResolveWorkingTreePath(repositoryRoot, path);
            if (ContainsSymbolicLink(repositoryRoot, fullPath))
            {
                if (!await visitor(
                    new PrChangedFile(path, oldPath, [], IsSymbolicLink: true),
                    null,
                    cancellationToken))
                {
                    return fileCount;
                }
                continue;
            }

            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                await visitor(
                    new PrChangedFile(
                        path,
                        oldPath,
                        [],
                        PreScanError: "Changed C# source is missing from the checked-out working tree."),
                    null,
                    cancellationToken);
                return fileCount;
            }

            var fileSize = fileInfo.Length;
            if (fileSize > ScanPrUnicodeCommand.MaxFileBytes)
            {
                if (!await visitor(
                    new PrChangedFile(path, oldPath, [], DeclaredSize: fileSize),
                    null,
                    cancellationToken))
                {
                    return fileCount;
                }
                continue;
            }

            if (totalTextBytes > maxTotalTextBytes - fileSize)
            {
                await visitor(
                    new PrChangedFile(
                        path,
                        oldPath,
                        [],
                        DeclaredSize: fileSize,
                        PreScanError:
                            $"Changed C# source exceeds the {maxTotalTextBytes / 1024 / 1024} MiB total scan limit."),
                    null,
                    cancellationToken);
                return fileCount;
            }
            totalTextBytes += fileSize;

            var file = new PrChangedFile(path, oldPath, [], DeclaredSize: fileSize);
            await using var content = new FileStream(
                fullPath,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                    BufferSize = 1,
                });
            if (!await visitor(file, content, cancellationToken))
            {
                return fileCount;
            }
        }

        return fileCount;
    }

    private static async Task<string?> FindTrackedCSharpSymlinkAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        // Validate the complete index, not only changed paths. Otherwise an existing Link.cs -> Payload.txt
        // could consume a changed Payload.txt while that non-C# file remains outside the content policy.
        var output = await RunGitAsync(
            repositoryPath,
            ["ls-files", "--stage", "-z", "--", ":(icase)*.cs", ":(icase)*.csx"],
            ScanPrUnicodeCommand.MaxTrackedCSharpListBytes,
            "Git tracked C# file list",
            cancellationToken);

        var start = 0;
        for (var i = 0; i < output.Length; i++)
        {
            if (output[i] != 0)
                continue;

            var entry = output.AsSpan(start, i - start);
            var tab = entry.IndexOf((byte)'\t');
            if (tab < 7 || entry[6] != (byte)' ')
                throw new ActionCommandException("Invalid git ls-files --stage output.");
            if (entry[..6].SequenceEqual("120000"u8))
            {
                try
                {
                    return StrictUtf8.GetString(entry[(tab + 1)..]);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new ActionCommandException("Tracked C# symbolic-link path is not valid UTF-8.", exception);
                }
            }
            start = i + 1;
        }
        if (start != output.Length)
            throw new ActionCommandException("Git returned non-NUL-terminated tracked C# file output.");
        return null;
    }

    private static RawDiffHeader ParseRawDiffHeader(string value)
    {
        var fields = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5 || fields[0].Length != 7 || fields[0][0] != ':' ||
            fields[1].Length != 6 || fields[4].Length == 0)
        {
            throw new ActionCommandException("Invalid git diff --raw header.");
        }
        return new RawDiffHeader(fields[1], fields[4][0]);
    }

    private static string ResolveWorkingTreePath(string repositoryRoot, string gitPath)
    {
        var relativePath = gitPath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
        var rootPrefix = Path.TrimEndingDirectorySeparator(repositoryRoot) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(rootPrefix, comparison))
            throw new ActionCommandException($"Changed path '{gitPath}' escapes the repository working tree.");
        return fullPath;
    }

    private static bool ContainsSymbolicLink(string repositoryRoot, string fullPath)
    {
        var components = Path.GetRelativePath(repositoryRoot, fullPath)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var current = repositoryRoot;
        for (var i = 0; i < components.Length; i++)
        {
            current = Path.Combine(current, components[i]);
            FileSystemInfo entry = i == components.Length - 1
                ? new FileInfo(current)
                : new DirectoryInfo(current);
            if (entry.LinkTarget is not null)
                return true;
        }
        return false;
    }

    private static byte[] RequiredField(IReadOnlyList<byte[]> fields, ref int index, char status)
    {
        if (index >= fields.Count)
            throw new ActionCommandException($"Invalid git diff --raw output after status '{status}'.");
        return fields[index++];
    }

    private static List<byte[]> SplitNullTerminated(byte[] value, int maxFields)
    {
        var result = new List<byte[]>();
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != 0)
                continue;
            if (result.Count >= maxFields)
                throw new ActionCommandException("Git diff contains too many path fields to scan safely.");
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
        int maxOutputBytes,
        string outputDescription,
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
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            var output = await ReadBoundedOutputAsync(
                process.StandardOutput.BaseStream,
                maxOutputBytes,
                outputDescription,
                cancellationToken);
            await Task.WhenAll(errorTask, process.WaitForExitAsync(cancellationToken));
            if (process.ExitCode != 0)
            {
                var stderr = await errorTask;
                throw new ActionCommandException($"git {arguments[0]} failed with exit code {process.ExitCode}: {stderr.Trim()}");
            }
            return output;
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

    private static async Task<byte[]> ReadBoundedOutputAsync(
        Stream stream,
        int maxOutputBytes,
        string outputDescription,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 64 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            await using var output = new MemoryStream();
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken);
                if (read == 0)
                    return output.ToArray();
                if (output.Length > maxOutputBytes - read)
                {
                    throw new ActionCommandException(
                        $"{outputDescription} exceeds the {maxOutputBytes / 1024 / 1024} MiB scan limit.");
                }
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private readonly record struct RawDiffHeader(string NewMode, char Status);
}
