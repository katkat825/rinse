using System.Text;

namespace Rinse;

/// <summary>Run with: Rinse.exe --selftest [output.txt]. Exercises the cleaning logic and writes a report.</summary>
static class SelfTest
{
    public static int Run(string outPath)
    {
        var report = new StringBuilder();
        int pass = 0, fail = 0;

        void Check(string name, string actual, string expected)
        {
            if (actual == expected) { pass++; report.AppendLine($"PASS  {name}"); }
            else
            {
                fail++;
                report.AppendLine($"FAIL  {name}");
                report.AppendLine($"      expected: {Show(expected)}");
                report.AppendLine($"      actual:   {Show(actual)}");
            }
        }
        void Url(string name, string input, string expected, int trackers = -1)
        {
            var r = LinkCleaner.Clean(input);
            Check(name, r.Url, expected);
            if (trackers >= 0) Check(name + " (tracker count)", r.TrackersRemoved.ToString(), trackers.ToString());
        }

        Url("Outlook SafeLinks unwrap + utm",
            "https://nam12.safelinks.protection.outlook.com/?url=https%3A%2F%2Fexample.com%2Fpage%3Fid%3D5%26utm_source%3Dnewsletter&data=05%7C01%7Cme%40corp.com&sdata=abc%3D&reserved=0",
            "https://example.com/page?id=5", 1);
        Url("utm params, keeps real param + fragment",
            "https://example.com/a?utm_source=x&utm_medium=y&b=2#top", "https://example.com/a?b=2#top", 2);
        Url("fbclid only", "https://site.org/?fbclid=IwAR123", "https://site.org/", 1);
        Url("youtu.be si", "https://youtu.be/dQw4w9WgXcQ?si=abcDEF", "https://youtu.be/dQw4w9WgXcQ", 1);
        Url("youtube keeps v and t", "https://www.youtube.com/watch?v=abc&t=42s&si=x", "https://www.youtube.com/watch?v=abc&t=42s", 1);
        Url("github ?ref= is meaningful, untouched", "https://github.com/a/b/tree/main?ref=feature", "https://github.com/a/b/tree/main?ref=feature", 0);
        Url("si on other sites untouched", "https://example.com/?si=1", "https://example.com/?si=1", 0);
        Url("google redirect", "https://www.google.com/url?sa=t&q=&url=https%3A%2F%2Fnews.example.com%2Fstory%3Futm_campaign%3Dz",
            "https://news.example.com/story", 1);
        Url("proofpoint v2", "https://urldefense.proofpoint.com/v2/url?u=https-3A__www.example.com_path_page-3Fa-3D1&d=DwMF&c=x",
            "https://www.example.com/path/page?a=1");
        Url("proofpoint v3", "https://urldefense.com/v3/__https://www.example.com/path?a=1__;!!ABC!def$",
            "https://www.example.com/path?a=1");
        Url("facebook l.php", "https://l.facebook.com/l.php?u=https%3A%2F%2Fexample.com%2F%3Ffbclid%3Dabc&h=AT0",
            "https://example.com/", 1);
        Url("amazon ref path + search junk",
            "https://www.amazon.com/Some-Product/dp/B0ABC12345/ref=sr_1_3?crid=2XYZ&keywords=desk+lamp&qid=1700000000&sprefix=desk%2Caps%2C150&sr=8-3",
            "https://www.amazon.com/Some-Product/dp/B0ABC12345?keywords=desk+lamp", 5);
        Url("clean link unchanged", "https://example.com/search?q=hello%20world", "https://example.com/search?q=hello%20world", 0);
        Check("unchanged link reports no change", LinkCleaner.Clean("https://example.com/?q=1").Changed.ToString(), "False");

        Check("IsSingleUrl: sentence", LinkCleaner.IsSingleUrl("see https://x.com").ToString(), "False");
        Check("IsSingleUrl: url w/ spaces around", LinkCleaner.IsSingleUrl("  https://x.com/a  ").ToString(), "True");
        Check("IsSingleUrl: not http", LinkCleaner.IsSingleUrl("ftp://x.com").ToString(), "False");

        Check("links inside text, punctuation kept",
            LinkCleaner.CleanAllInText("See (https://example.com/a?utm_source=x). Thanks, https://b.com/?gclid=1!", out int l, out int t),
            "See (https://example.com/a). Thanks, https://b.com/!");
        Check("links inside text counts", $"{l}/{t}", "2/2");
        Check("wiki link with parens kept intact",
            LinkCleaner.CleanAllInText("https://en.wikipedia.org/wiki/Tea_(meal)", out _, out _),
            "https://en.wikipedia.org/wiki/Tea_(meal)");

        Check("fix line breaks (PDF wrap + hyphen)",
            TextTools.FixLineBreaks("This is a para-\ngraph that was\nwrapped.\n\nSecond one\nhere."),
            "This is a paragraph that was wrapped.\r\n\r\nSecond one here.");
        Check("fix line breaks keeps bullets",
            TextTools.FixLineBreaks("Items:\n- one\n- two"), "Items:\r\n- one\r\n- two");
        Check("fix line breaks keeps numbered list",
            TextTools.FixLineBreaks("Steps\r\n1. Open it\r\n2. Close it"), "Steps\r\n1. Open it\r\n2. Close it");
        Check("tidy spaces", TextTools.TidySpaces("  a\u00A0 b\u200B  \n\n\n\nc  "), "a b\r\n\r\nc");
        Check("straight quotes", TextTools.StraightQuotes("\u201CHi\u201D it\u2019s\u2026"), "\"Hi\" it's...");
        Check("sentence case", TextTools.SentenceCase("HELLO THERE. i AM here! ok"), "Hello there. I am here! Ok");
        Check("title case", TextTools.TitleCase("the lord of the rings"), "The Lord of the Rings");
        Check("title case apostrophe", TextTools.TitleCase("DON'T stop me now"), "Don't Stop Me Now");
        Check("sort lines", TextTools.SortLines("b\na\n\nC"), "a\r\nb\r\nC");
        Check("remove duplicates (case-insensitive)", TextTools.RemoveDuplicateLines("b\na\nb\nA"), "b\r\na");
        Check("remove blank lines", TextTools.RemoveBlankLines("a\n\n  \nb"), "a\r\nb");

        report.Insert(0, $"Rinse self-test: {pass} passed, {fail} failed{Environment.NewLine}{Environment.NewLine}");
        File.WriteAllText(outPath, report.ToString());
        return fail == 0 ? 0 : 1;
    }

    static string Show(string s) => s == null ? "(null)" : s.Replace("\r", "\\r").Replace("\n", "\\n");
}
