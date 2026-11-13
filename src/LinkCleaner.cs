using System.Text.RegularExpressions;

namespace Rinse;

record RinseResult(string Url, int TrackersRemoved, bool Unwrapped)
{
    public bool Changed => TrackersRemoved > 0 || Unwrapped;
}

/// <summary>
/// Unwraps redirect wrappers (Outlook SafeLinks, Proofpoint, Google, Facebook...)
/// and strips well-known tracking parameters, leaving meaningful ones alone.
/// </summary>
static class LinkCleaner
{
    static readonly string[] TrackerPrefixes = { "utm_", "_hs", "__hs", "hsa_", "mtm_", "pd_rd_", "pf_rd_", "oly_", "vero_" };

    static readonly HashSet<string> Trackers = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "gclsrc", "dclid", "gbraid", "wbraid", "msclkid", "mc_cid", "mc_eid",
        "igshid", "igsh", "yclid", "twclid", "ttclid", "li_fat_id", "mkt_tok", "_openstat",
        "rb_clickid", "s_cid", "ref_src", "ref_url", "trk", "trkCampaign", "sc_campaign",
        "sc_channel", "sc_content", "sc_medium", "sc_outcome", "sc_geo", "sc_country", "cmpid",
        "spm", "scm", "_ga", "_gl", "wickedid", "epik", "srsltid", "ns_mchannel", "ns_source",
        "ns_campaign", "ns_linkname", "ns_fee", "hsCtaTracking", "mibextid",
    };

    // Parameters that are only tracking on specific sites (elsewhere they may mean something).
    static readonly Dictionary<string, string[]> SiteTrackers = new()
    {
        ["youtube.com"] = new[] { "si", "feature" },
        ["youtu.be"] = new[] { "si", "feature" },
        ["spotify.com"] = new[] { "si" },
        ["x.com"] = new[] { "s", "t" },
        ["twitter.com"] = new[] { "s", "t" },
        ["linkedin.com"] = new[] { "trackingId", "lipi", "midToken", "midSig", "trkEmail", "rcm" },
        ["facebook.com"] = new[] { "rdid", "share_url" },
        ["tiktok.com"] = new[] { "_r", "_t", "is_from_webapp", "sender_device", "sender_web_id" },
        ["reddit.com"] = new[] { "share_id", "rdt" },
        ["medium.com"] = new[] { "source" },
    };

    static readonly string[] AmazonTrackers = { "ref", "ref_", "crid", "sprefix", "qid", "sr", "dib", "dib_tag", "content-id", "social_share", "starsLeft", "linkCode" };

    static readonly Regex AmazonHost = new(@"(^|\.)amazon\.[a-z.]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex ProofpointV3 = new(@"^https?://urldefense(?:\.proofpoint)?\.com/v3/__(.+?)__;", RegexOptions.IgnoreCase);
    static readonly Regex UrlInText = new(@"https?://[^\s<>""'`]+", RegexOptions.IgnoreCase);

    public static bool IsSingleUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        if (t.Length > 8192 || t.Any(char.IsWhiteSpace)) return false;
        if (!t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        return Uri.TryCreate(t, UriKind.Absolute, out _);
    }

    public static RinseResult Clean(string url)
    {
        bool unwrapped = false;
        for (int i = 0; i < 5; i++)
        {
            var inner = TryUnwrap(url);
            if (inner == null || inner == url) break;
            url = inner.Trim();
            unwrapped = true;
        }
        var (clean, removed) = StripTrackers(url);
        return new RinseResult(clean, removed, unwrapped);
    }

    /// <summary>Cleans every link found inside a larger block of text.</summary>
    public static string CleanAllInText(string text, out int links, out int trackers)
    {
        int l = 0, t = 0;
        var result = UrlInText.Replace(text, m =>
        {
            string raw = m.Value, tail = "";
            while (raw.Length > 0)
            {
                char c = raw[^1];
                bool strip = ".,;:!?".IndexOf(c) >= 0
                    || (c == ')' && raw.Count(x => x == ')') > raw.Count(x => x == '('))
                    || (c == ']' && raw.Count(x => x == ']') > raw.Count(x => x == '['));
                if (!strip) break;
                tail = c + tail;
                raw = raw[..^1];
            }
            var r = Clean(raw);
            if (r.Changed && r.Url != raw) { l++; t += r.TrackersRemoved; }
            return r.Url + tail;
        });
        links = l; trackers = t;
        return result;
    }

    static string TryUnwrap(string url)
    {
        var v3 = ProofpointV3.Match(url);
        if (v3.Success)
        {
            var inner = v3.Groups[1].Value;
            // '*' marks characters Proofpoint moved into its trailer; don't guess at those.
            return inner.Contains('*') || !LooksLikeHttp(inner) ? null : inner;
        }

        int hash = url.IndexOf('#');
        string noFrag = hash >= 0 ? url[..hash] : url;
        int q = noFrag.IndexOf('?');
        string basePart = q >= 0 ? noFrag[..q] : noFrag;
        string query = q >= 0 ? noFrag[(q + 1)..] : "";
        if (!Uri.TryCreate(basePart, UriKind.Absolute, out var uri)) return null;

        string host = uri.Host.ToLowerInvariant();
        string path = uri.AbsolutePath;

        if (host.EndsWith("safelinks.protection.outlook.com")) return GetUrl(query, "url");
        if (host == "urldefense.proofpoint.com" && path.StartsWith("/v2/url"))
        {
            var raw = GetRaw(query, "u");
            if (raw == null) return null;
            var decoded = SafeUnescape(Regex.Replace(raw.Replace('_', '/'), "-([0-9A-Fa-f]{2})", "%$1"));
            return LooksLikeHttp(decoded) ? decoded : null;
        }
        if ((host.StartsWith("www.google.") || host.StartsWith("google.")) && path == "/url")
            return GetUrl(query, "q") ?? GetUrl(query, "url");
        if ((host == "l.facebook.com" || host == "lm.facebook.com" || host == "l.messenger.com") && path == "/l.php")
            return GetUrl(query, "u");
        if (host == "l.instagram.com") return GetUrl(query, "u");
        if (host == "out.reddit.com") return GetUrl(query, "url");
        if (host == "www.youtube.com" && path == "/redirect") return GetUrl(query, "q");
        if (host == "slack-redir.net" && path == "/link") return GetUrl(query, "url");
        if (host.EndsWith("linkedin.com") && path.StartsWith("/redir/redirect")) return GetUrl(query, "url");
        if (host == "steamcommunity.com" && path.StartsWith("/linkfilter")) return GetUrl(query, "url") ?? GetUrl(query, "u");
        return null;
    }

    static (string, int) StripTrackers(string url)
    {
        string original = url;
        string frag = "";
        int hash = url.IndexOf('#');
        if (hash >= 0) { frag = url[hash..]; url = url[..hash]; }
        string basePart = url, query = "";
        int q = url.IndexOf('?');
        if (q >= 0) { basePart = url[..q]; query = url[(q + 1)..]; }

        if (!Uri.TryCreate(basePart, UriKind.Absolute, out var uri)) return (original, 0);
        string host = uri.Host.ToLowerInvariant();
        bool amazon = AmazonHost.IsMatch(host);
        int removed = 0;

        if (amazon)
        {
            var nb = Regex.Replace(basePart, "/ref=[^/]*$", "");
            if (nb != basePart) { basePart = nb; removed++; }
        }

        var kept = new List<string>();
        foreach (var pair in query.Split('&'))
        {
            if (pair.Length == 0) continue;
            int eq = pair.IndexOf('=');
            string key = SafeUnescape((eq >= 0 ? pair[..eq] : pair).Replace('+', ' '));
            if (IsTracker(key, host, amazon)) removed++;
            else kept.Add(pair);
        }

        if (removed == 0) return (original, 0);
        return (basePart + (kept.Count > 0 ? "?" + string.Join("&", kept) : "") + frag, removed);
    }

    static bool IsTracker(string key, string host, bool amazon)
    {
        if (Trackers.Contains(key)) return true;
        foreach (var p in TrackerPrefixes)
            if (key.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var kv in SiteTrackers)
            if ((host == kv.Key || host.EndsWith("." + kv.Key)) &&
                kv.Value.Contains(key, StringComparer.OrdinalIgnoreCase)) return true;
        if (amazon && AmazonTrackers.Contains(key, StringComparer.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string GetRaw(string query, string name)
    {
        foreach (var pair in query.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            if (string.Equals(pair[..eq], name, StringComparison.OrdinalIgnoreCase)) return pair[(eq + 1)..];
        }
        return null;
    }

    static string GetUrl(string query, string name)
    {
        var raw = GetRaw(query, name);
        if (raw == null) return null;
        var v = SafeUnescape(raw);
        return LooksLikeHttp(v) ? v : null;
    }

    static bool LooksLikeHttp(string s) =>
        s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    static string SafeUnescape(string s)
    {
        try { return Uri.UnescapeDataString(s); } catch { return s; }
    }
}
