using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Rinse;

/// <summary>One-click fixes for text pasted out of PDFs, emails, web pages and chats.</summary>
static class TextTools
{
    static string N(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n');
    static string W(string s) => s.Replace("\n", "\r\n");

    static readonly Regex ListItem = new(@"^([-•*‣◦▪–]\s|\d{1,3}[.)]\s|[a-zA-Z][.)]\s)");

    /// <summary>Re-joins lines that were hard-wrapped (e.g. copied from a PDF), keeping paragraphs and lists.</summary>
    public static string FixLineBreaks(string s)
    {
        var paras = Regex.Split(N(s).Trim('\n'), @"\n[ \t]*\n+");
        var output = new List<string>();
        foreach (var p in paras)
        {
            var lines = p.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (sb.Length == 0) { sb.Append(line); continue; }
                if (ListItem.IsMatch(line)) { sb.Append('\n').Append(line); continue; }
                bool hyphenBreak = sb.Length >= 2 && sb[^1] == '-' && char.IsLetter(sb[^2]) && char.IsLower(line[0]);
                if (hyphenBreak) { sb.Length -= 1; sb.Append(line); }
                else sb.Append(' ').Append(line);
            }
            if (sb.Length > 0) output.Add(sb.ToString());
        }
        return W(string.Join("\n\n", output));
    }

    /// <summary>Removes invisible characters, odd spaces and runs of blank lines.</summary>
    public static string TidySpaces(string s)
    {
        s = N(s);
        s = Regex.Replace(s, "[     　]", " ");
        s = Regex.Replace(s, "[​‌‍⁠﻿­]", "");
        var lines = s.Split('\n').Select(l => Regex.Replace(l, @"[ \t]+", " ").Trim());
        s = string.Join("\n", lines);
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return W(s.Trim());
    }

    public static string StraightQuotes(string s) => s
        .Replace('‘', '\'').Replace('’', '\'').Replace('‚', '\'').Replace('‛', '\'')
        .Replace('“', '"').Replace('”', '"').Replace('„', '"').Replace('‟', '"')
        .Replace('′', '\'').Replace('″', '"').Replace("…", "...");

    public static string RemoveBlankLines(string s) =>
        W(string.Join("\n", N(s).Split('\n').Where(l => !string.IsNullOrWhiteSpace(l))));

    public static string Upper(string s) => s.ToUpper(CultureInfo.CurrentCulture);
    public static string Lower(string s) => s.ToLower(CultureInfo.CurrentCulture);

    static readonly HashSet<string> SmallWords = new(StringComparer.OrdinalIgnoreCase)
    { "a", "an", "the", "and", "but", "or", "nor", "for", "so", "yet", "on", "at", "to", "from", "by", "of", "in", "with", "as", "vs" };

    public static string TitleCase(string s)
    {
        var lower = Lower(s);
        return Regex.Replace(lower, @"[\p{L}\p{N}][\p{L}\p{N}'’]*", m =>
        {
            int lineStart = m.Index == 0 ? 0 : lower.LastIndexOf('\n', m.Index - 1) + 1;
            bool first = string.IsNullOrWhiteSpace(lower.Substring(lineStart, m.Index - lineStart));
            if (!first && SmallWords.Contains(m.Value)) return m.Value;
            return char.ToUpper(m.Value[0], CultureInfo.CurrentCulture) + m.Value[1..];
        });
    }

    public static string SentenceCase(string s)
    {
        var lower = Lower(s);
        var result = Regex.Replace(lower, @"(^\s*|[.!?]\s+|\n\s*)(\p{Ll})",
            m => m.Groups[1].Value + m.Groups[2].Value.ToUpper(CultureInfo.CurrentCulture));
        return Regex.Replace(result, @"\bi\b", "I");
    }

    public static string SortLines(string s) =>
        W(string.Join("\n", N(s).Split('\n').Where(l => l.Trim().Length > 0)
            .OrderBy(l => l.Trim(), StringComparer.CurrentCultureIgnoreCase)));

    public static string RemoveDuplicateLines(string s)
    {
        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var kept = N(s).Split('\n').Where(l => l.Trim().Length == 0 || seen.Add(l.Trim()));
        return W(string.Join("\n", kept));
    }
}
