using System.Text;
using System.Text.RegularExpressions;

namespace CysharpActions.Utils;

/// <summary>Matches branch names with GitHub Actions branch filter patterns.</summary>
/// <remarks>
/// The syntax follows the GitHub filter pattern cheat sheet:
/// <c>*</c> matches zero or more characters except <c>/</c>, <c>**</c> also matches <c>/</c>,
/// <c>?</c> and <c>+</c> repeat the previous character or <c>[]</c> class zero-or-one and one-or-more times,
/// <c>[]</c> lists ASCII letters, digits and ranges, <c>\</c> escapes the next character,
/// and a leading <c>!</c> excludes names that earlier patterns matched. Later patterns win.
/// Any other character, including <c>.</c>, matches only itself.
/// </remarks>
public sealed class BranchFilter
{
    private readonly Rule[] rules;

    private BranchFilter(Rule[] rules) => this.rules = rules;

    /// <summary>Patterns in evaluation order, as written.</summary>
    public IReadOnlyList<string> Patterns => rules.Select(x => x.Pattern).ToArray();

    /// <summary>Parses patterns and rejects invalid syntax so that a typo cannot silently match nothing.</summary>
    public static BranchFilter Parse(IEnumerable<string> patterns)
        => new(patterns.Select(ParseRule).ToArray());

    /// <summary>Returns true when the last pattern that matches <paramref name="branch"/> is not a negation.</summary>
    public bool IsMatch(string branch)
    {
        var matched = false;
        foreach (var rule in rules)
        {
            if (rule.Regex.IsMatch(branch))
                matched = !rule.Negated;
        }
        return matched;
    }

    private static Rule ParseRule(string pattern)
    {
        var negated = pattern.StartsWith('!');
        var body = negated ? pattern[1..] : pattern;
        if (body.Length == 0)
            throw Invalid(pattern, "the pattern is empty");

        var regex = new StringBuilder(@"\A");
        // True when the previous token is one character or one [] class, which ? and + can repeat.
        var canRepeat = false;
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            switch (c)
            {
                case '*':
                    var last = i;
                    while (last + 1 < body.Length && body[last + 1] == '*')
                        last++;
                    regex.Append(last > i ? ".*" : "[^/]*");
                    i = last;
                    canRepeat = false;
                    break;
                case '?':
                case '+':
                    if (!canRepeat)
                        throw Invalid(pattern, $"'{c}' must follow a character or a [] class");
                    regex.Append(c);
                    canRepeat = false;
                    break;
                case '[':
                    var close = body.IndexOf(']', i + 1);
                    if (close < 0)
                        throw Invalid(pattern, "'[' has no matching ']'");
                    regex.Append('[').Append(ParseClass(pattern, body[(i + 1)..close])).Append(']');
                    i = close;
                    canRepeat = true;
                    break;
                case '\\':
                    if (i + 1 == body.Length)
                        throw Invalid(pattern, "'\\' must be followed by a character");
                    regex.Append(Regex.Escape(body[++i].ToString()));
                    canRepeat = true;
                    break;
                default:
                    regex.Append(Regex.Escape(c.ToString()));
                    canRepeat = true;
                    break;
            }
        }
        regex.Append(@"\z");
        // NonBacktracking keeps matching linear even for patterns such as *a*a*a*a.
        return new Rule(pattern, negated, new Regex(regex.ToString(), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking));
    }

    // GitHub allows only letters and digits in [], with ranges inside a-z, A-Z or 0-9.
    private static string ParseClass(string pattern, string content)
    {
        if (content.Length == 0)
            throw Invalid(pattern, "'[]' is empty");
        for (var i = 0; i < content.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(content[i]))
                throw Invalid(pattern, $"'[]' can contain only letters, digits and ranges, but contains '{content[i]}'");
            if (i + 2 < content.Length && content[i + 1] == '-')
            {
                var (start, end) = (content[i], content[i + 2]);
                if (!(char.IsAsciiLetterLower(start) && char.IsAsciiLetterLower(end)
                    || char.IsAsciiLetterUpper(start) && char.IsAsciiLetterUpper(end)
                    || char.IsAsciiDigit(start) && char.IsAsciiDigit(end)) || start > end)
                    throw Invalid(pattern, $"'{start}-{end}' is not a range inside a-z, A-Z or 0-9");
                i += 2;
            }
        }
        return content;
    }

    private static ActionCommandException Invalid(string pattern, string reason)
        => new($"Invalid branch pattern '{pattern}': {reason}.");

    private sealed record Rule(string Pattern, bool Negated, Regex Regex);
}
