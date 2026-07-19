#nullable enable
namespace Quartz.Features.Tuf;

// Turns a level's videoLink into a thumbnail source. YouTube resolves to a direct
// image URL; Bilibili resolves to a BV id that the preview cache turns into a cover
// through Bilibili's API. Anything else (an empty link, a malformed one, some other
// site) returns None and the card stays plain. Every URL we ultimately fetch is one we
// build from a validated id, never the raw videoLink — that origin is community data.
public static class TufThumbnail {
    // mqdefault is the sweet spot for a blurred background: 320x180, a clean 16:9 frame
    // (hqdefault/sddefault add 4:3 black bars), and — unlike maxresdefault — it is
    // always generated, so there is no 404 to fall back from. All live on this host.
    public const string MediumRes = "mqdefault";
    public const string Host = "i.ytimg.com";
    // Bilibili: the metadata API that maps a BV id to its cover, the CDN the cover
    // lives on, and the resize suffix that yields the same cheap 320x180 16:9 frame.
    public const string BilibiliApiHost = "api.bilibili.com";
    public const string BilibiliImageHostSuffix = ".hdslb.com";
    public const string BilibiliResize = "@320w_180h_1c.jpg";

    public enum TufVideoKind { None, YouTube, Bilibili }

    public readonly struct TufVideoRef {
        public readonly TufVideoKind Kind;
        public readonly string Id; // YouTube video id, or Bilibili BV id
        public TufVideoRef(TufVideoKind kind, string id) { Kind = kind; Id = id; }
        public static readonly TufVideoRef None = new(TufVideoKind.None, "");
        public bool HasThumbnail => Kind != TufVideoKind.None;
    }

    // The first token that yields a usable source. YouTube wins over Bilibili when a
    // link carries both, since its thumbnail needs no extra API round trip.
    public static TufVideoRef Resolve(string? videoLink) {
        string? youtube = ExtractYouTubeId(videoLink);
        if(youtube != null) return new TufVideoRef(TufVideoKind.YouTube, youtube);
        string? bilibili = ExtractBilibiliId(videoLink);
        if(bilibili != null) return new TufVideoRef(TufVideoKind.Bilibili, bilibili);
        return TufVideoRef.None;
    }

    public static string? ThumbnailUrl(string? videoLink, string quality = MediumRes) {
        string? id = ExtractYouTubeId(videoLink);
        return id == null ? null : ThumbnailUrlForId(id, quality);
    }

    public static string ThumbnailUrlForId(string id, string quality) =>
        $"https://{Host}/vi/{id}/{quality}.jpg";

    // Pulls the 11-ish char id out of the watch/share/embed/shorts forms YouTube uses.
    // A level's videoLink is sometimes several URLs separated by whitespace (a second
    // camera angle, a mirror, a Bilibili + YouTube pair), so scan the tokens and take
    // the first that is a usable YouTube link.
    public static string? ExtractYouTubeId(string? videoLink) {
        if(string.IsNullOrWhiteSpace(videoLink)) return null;
        foreach(string token in videoLink!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) {
            string? id = ExtractOne(token);
            if(id != null) return id;
        }
        return null;
    }

    private static string? ExtractOne(string token) {
        if(!Uri.TryCreate(token, UriKind.Absolute, out Uri? uri)) return null;
        if(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        string host = uri.Host.ToLowerInvariant();
        if(host.StartsWith("www.", StringComparison.Ordinal)) host = host.Substring(4);
        if(host.StartsWith("m.", StringComparison.Ordinal)) host = host.Substring(2);
        string? candidate = host switch {
            "youtu.be" => uri.AbsolutePath.Trim('/'),
            "youtube.com" or "youtube-nocookie.com" => FromYouTubeComUri(uri),
            _ => null
        };
        return IsValidId(candidate) ? candidate : null;
    }

    private static string? FromYouTubeComUri(Uri uri) {
        string path = uri.AbsolutePath;
        if(path is "/watch" or "/watch/") {
            foreach(string pair in uri.Query.TrimStart('?').Split('&')) {
                int eq = pair.IndexOf('=');
                if(eq > 0 && pair.Substring(0, eq) == "v") return Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
            return null;
        }
        foreach(string prefix in new[] { "/embed/", "/shorts/", "/v/", "/live/" })
            if(path.StartsWith(prefix, StringComparison.Ordinal)) {
                string rest = path.Substring(prefix.Length);
                int slash = rest.IndexOf('/');
                return slash < 0 ? rest : rest.Substring(0, slash);
            }
        return null;
    }

    // Pulls the BV id out of a bilibili.com/video/BV... link, scanning tokens the same
    // way (a videoLink may pair a Bilibili and a YouTube url).
    public static string? ExtractBilibiliId(string? videoLink) {
        if(string.IsNullOrWhiteSpace(videoLink)) return null;
        foreach(string token in videoLink!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) {
            string? bv = BilibiliOne(token);
            if(bv != null) return bv;
        }
        return null;
    }

    private static string? BilibiliOne(string token) {
        if(!Uri.TryCreate(token, UriKind.Absolute, out Uri? uri)) return null;
        if(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        string host = uri.Host.ToLowerInvariant();
        if(host.StartsWith("www.", StringComparison.Ordinal)) host = host.Substring(4);
        if(host.StartsWith("m.", StringComparison.Ordinal)) host = host.Substring(2);
        if(host != "bilibili.com") return null;
        foreach(string segment in uri.AbsolutePath.Split('/'))
            if(IsValidBv(segment)) return segment;
        return null;
    }

    // A BV id is "BV" + 10 base58 chars; accept a slightly wider length band but keep
    // it strictly ASCII-alphanumeric so it is a safe query value and file segment.
    private static bool IsValidBv(string? id) {
        if(string.IsNullOrEmpty(id) || id!.Length is < 10 or > 14) return false;
        if(id[0] != 'B' || id[1] != 'V') return false;
        for(int i = 2; i < id.Length; i++) {
            char c = id[i];
            if(!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))) return false;
        }
        return true;
    }

    // YouTube ids are 11 chars of [A-Za-z0-9_-]; accept a small range but reject
    // anything outside that ASCII set so the id is always a safe path/file segment.
    private static bool IsValidId(string? id) {
        if(string.IsNullOrEmpty(id) || id!.Length is < 8 or > 20) return false;
        foreach(char c in id)
            if(!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
        return true;
    }
}
