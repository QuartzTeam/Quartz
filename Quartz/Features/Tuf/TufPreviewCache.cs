#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
using UnityEngine;
using static Quartz.Features.Tuf.TufThumbnail;

namespace Quartz.Features.Tuf;

// Where a preview comes from: a level's video (resolved to a YouTube/Bilibili cover) or
// a direct image URL (a pack's iconUrl). HasThumbnail decides whether a card should
// build the preview layer at all; the cache turns the source into an actual image.
public readonly struct TufPreviewSource {
    internal readonly string? VideoLink;
    internal readonly string? IconUrl;
    internal readonly int LevelId; // resolve this level's videoLink via byId, then treat as a video
    private TufPreviewSource(string? videoLink, string? iconUrl, int levelId) {
        VideoLink = videoLink; IconUrl = iconUrl; LevelId = levelId;
    }

    public static TufPreviewSource Video(string? videoLink) => new(videoLink, null, 0);
    public static TufPreviewSource Icon(string? iconUrl) => new(null, iconUrl, 0);
    public static TufPreviewSource Level(int levelId) => new(null, null, levelId);

    // A pack card: its uploaded icon if it has one, else its first level's video (most
    // packs have no custom icon). default (no icon, no level) has no thumbnail.
    public static TufPreviewSource ForPack(string? iconUrl, int firstLevelId) =>
        !string.IsNullOrWhiteSpace(iconUrl) ? Icon(iconUrl)
        : firstLevelId > 0 ? Level(firstLevelId)
        : default;

    // A level source is optimistic — the byId lookup that yields its videoLink is a
    // network call, so we assume it has one and fall back to a plain card if it does not.
    public bool HasThumbnail =>
        IconUrl != null ? TufPreviewCache.NormalizeIconUrl(IconUrl) != null :
        LevelId > 0 ? true :
        Resolve(VideoLink).HasThumbnail;
}

// Fetches a card's thumbnail, blurs it, and hands the UI a ready texture to sit behind
// the card. Built to never hitch the game:
//
//   * Small sources only — YouTube/Bilibili resolve to 320x180, pack icons to the CDN's
//     128x128 "medium" variant (the "original" can be 4000px+), so the two ops that
//     MUST run on the main thread (decode + GetPixels32, and the upload) stay cheap.
//   * The blur is a 3-pass box blur (fast gaussian approximation): O(pixels), radius
//     independent, and it runs on the thread pool.
//   * Main-thread steps drain through a per-frame budget (TufPreviewPump), so a burst
//     (paging back over many disk-cached previews) spreads across frames.
//
// Keyed by an opaque string ("512" for a level, "pack-RCAXIAv9" for a pack) so levels
// and packs share one cache; raw jpgs live on disk, blurred textures in memory.
public static class TufPreviewCache {
    private const float BlurPx = 8f;
    private const float NominalCardWidth = 880f;
    private const int MaxJpegBytes = 1024 * 1024;
    private const int MaxApiJsonBytes = 512 * 1024;
    private const int MaxTextures = 300;
    // Safety net: anything larger is box-averaged down before the blur, bounding blur
    // cost and texture memory even if some source arrives bigger than expected.
    private const int MaxBlurDim = 512;
    private const int MaxOpsPerFrame = 3;
    private const double MaxMillisPerFrame = 1.5;
    private const int MaxRedirects = 3;

    private enum Status { Pending, Ready, Failed }

    private sealed class Entry {
        public Status Status;
        public Texture2D? Texture;
    }

    private static readonly object gate = new();
    private static readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private static readonly Queue<string> insertionOrder = new();
    private static readonly SemaphoreSlim network = new(3, 3);
    private static readonly ConcurrentQueue<Action> mainQueue = new();
    private static readonly object httpGate = new();
    private static HttpClient? http;
    private static TufPreviewPump? pump;
    // Bumped on Clear so in-flight tasks whose textures land after a UMM reload are
    // discarded instead of leaking into the next session's cache.
    private static int generation;

    // Raised on the main thread whenever a new blurred texture becomes available.
    public static event Action? Changed;

    public static bool TryGet(string key, out Texture2D? texture) {
        lock(gate) {
            if(entries.TryGetValue(key, out Entry? entry) && entry.Status == Status.Ready) {
                texture = entry.Texture;
                return texture != null;
            }
        }
        texture = null;
        return false;
    }

    // Idempotent: the first call for a key kicks off the pipeline; later ones for the
    // same key are no-ops. Must be called from the main thread (it is, from card build).
    public static void Request(string key, TufPreviewSource source) {
        EnsurePump();
        int gen;
        lock(gate) {
            if(entries.ContainsKey(key)) return;
            if(!source.HasThumbnail) {
                entries[key] = new Entry { Status = Status.Failed }; // no known source — never retried
                return;
            }
            entries[key] = new Entry { Status = Status.Pending };
            gen = generation;
        }
        _ = Task.Run(() => DownloadAndBlur(key, source, gen));
    }

    // Frees every cached texture. Called from TufService.Dispose so a UMM in-process
    // reload does not leak the previous session's thumbnails.
    public static void Clear() {
        lock(gate) {
            generation++;
            foreach(Entry entry in entries.Values)
                if(entry.Texture != null) UnityEngine.Object.Destroy(entry.Texture);
            entries.Clear();
            insertionOrder.Clear();
        }
        while(mainQueue.TryDequeue(out _)) { }
    }

    // Drained by TufPreviewPump.Update on the main thread, under a count + time budget.
    internal static void Pump() {
        if(mainQueue.IsEmpty) return;
        Stopwatch stopwatch = Stopwatch.StartNew();
        int ops = 0;
        while(ops < MaxOpsPerFrame && stopwatch.Elapsed.TotalMilliseconds < MaxMillisPerFrame
            && mainQueue.TryDequeue(out Action? action)) {
            try { action(); } catch(Exception e) { MainCore.Log.Msg("[TUF] preview step failed: " + e.Message); }
            ops++;
        }
    }

    private static async Task DownloadAndBlur(string key, TufPreviewSource source, int gen) {
        await network.WaitAsync().ConfigureAwait(false);
        byte[]? jpeg;
        try {
            jpeg = LoadJpeg(key, source);
        } catch(Exception e) {
            MainCore.Log.Msg("[TUF] preview download failed: " + e.Message);
            jpeg = null;
        } finally {
            network.Release();
        }
        if(jpeg == null) {
            Fail(key, gen);
            return;
        }
        byte[] bytes = jpeg;
        mainQueue.Enqueue(() => DecodeOnMain(key, bytes, gen));
    }

    // Disk-cached raw jpg (TUF/Thumbnails/<key>.jpg), else a bounded download. The disk
    // check runs BEFORE any source resolution, so a cached Bilibili/pack thumbnail never
    // re-hits its metadata API. Saved to that path so the next launch never re-fetches.
    private static byte[]? LoadJpeg(string key, TufPreviewSource source) {
        string path = CachePath(key);
        try {
            if(File.Exists(path)) {
                byte[] cached = File.ReadAllBytes(path);
                if(cached.Length > 0) return cached;
            }
        } catch { }
        Uri? image = ResolveImageUri(source);
        if(image == null) return null;
        byte[]? data = Download(image, MaxJpegBytes, TrustedImageHost);
        if(data == null || data.Length == 0) return null;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data);
        } catch { }
        return data;
    }

    // The final image URL for a source. YouTube is a direct build; Bilibili needs one
    // API round trip; a pack icon is the CDN URL as given (retargeted to its bounded
    // "medium" size). Every returned URI is HTTPS on an allowlisted host.
    private static Uri? ResolveImageUri(TufPreviewSource source) {
        if(source.IconUrl != null) return NormalizeIconUrl(source.IconUrl);
        // A pack with no icon points at its first level; look that level's video up.
        string? videoLink = source.LevelId > 0 ? ResolveLevelVideoLink(source.LevelId) : source.VideoLink;
        return ResolveVideoUri(videoLink);
    }

    private static Uri? ResolveVideoUri(string? videoLink) {
        TufVideoRef reference = Resolve(videoLink);
        switch(reference.Kind) {
            case TufVideoKind.YouTube:
                string url = ThumbnailUrlForId(reference.Id, MediumRes);
                return Uri.TryCreate(url, UriKind.Absolute, out Uri? yt)
                    && yt.Scheme == Uri.UriSchemeHttps && yt.Host == Host ? yt : null;
            case TufVideoKind.Bilibili:
                return ResolveBilibiliCover(reference.Id);
            default:
                return null;
        }
    }

    // level id -> byId -> its videoLink (a flat top-level field). Used for a pack card's
    // fallback thumbnail when the pack has no uploaded icon.
    private static string? ResolveLevelVideoLink(int levelId) {
        string api = "https://api.tuforums.com/v2/database/levels/byId/" + levelId;
        if(!Uri.TryCreate(api, UriKind.Absolute, out Uri? apiUri)
            || apiUri.Scheme != Uri.UriSchemeHttps || !IsTufHost(apiUri.Host)) return null;
        byte[]? json = Download(apiUri, MaxApiJsonBytes, null);
        if(json == null || json.Length == 0) return null;
        try {
            return JObject.Parse(System.Text.Encoding.UTF8.GetString(json)).Value<string>("videoLink");
        } catch { return null; }
    }

    // A pack iconUrl is a full TUF URL like
    // https://api.tuforums.com/cdn/images/pack_icon/<uuid>/original — retargeted to the
    // "medium" (128x128) variant so the decode is bounded (the original can be 4000px+),
    // and validated to the TUF host. It 301s to cdn.tuforums.com, which Download follows.
    internal static Uri? NormalizeIconUrl(string? iconUrl) {
        if(string.IsNullOrWhiteSpace(iconUrl)
            || !Uri.TryCreate(iconUrl, UriKind.Absolute, out Uri? uri)) return null;
        if(uri.Scheme != Uri.UriSchemeHttps || !IsTufHost(uri.Host)) return null;
        string path = uri.AbsolutePath;
        if(path.EndsWith("/original", StringComparison.Ordinal))
            path = path.Substring(0, path.Length - "original".Length) + "medium";
        string url = "https://" + uri.Host + path;
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? normalized) ? normalized : null;
    }

    // BV id -> Bilibili view API -> data.pic (cover on *.hdslb.com) -> the 320x180 resize
    // variant. The pic host is validated before it is ever fetched.
    private static Uri? ResolveBilibiliCover(string bv) {
        string api = "https://" + BilibiliApiHost + "/x/web-interface/view?bvid=" + Uri.EscapeDataString(bv);
        if(!Uri.TryCreate(api, UriKind.Absolute, out Uri? apiUri)
            || apiUri.Scheme != Uri.UriSchemeHttps || apiUri.Host != BilibiliApiHost) return null;
        byte[]? json = Download(apiUri, MaxApiJsonBytes, null);
        if(json == null || json.Length == 0) return null;
        string? pic;
        try {
            JObject root = JObject.Parse(System.Text.Encoding.UTF8.GetString(json));
            if(root.Value<int?>("code") != 0) return null;
            pic = root["data"]?.Value<string>("pic");
        } catch { return null; }
        if(string.IsNullOrWhiteSpace(pic)
            || !Uri.TryCreate(pic, UriKind.Absolute, out Uri? picUri)) return null;
        string host = picUri.Host.ToLowerInvariant();
        if(!host.EndsWith(BilibiliImageHostSuffix, StringComparison.Ordinal)) return null;
        string cover = "https://" + host + picUri.AbsolutePath + BilibiliResize; // https, no query
        return Uri.TryCreate(cover, UriKind.Absolute, out Uri? final) ? final : null;
    }

    private static bool IsTufHost(string host) =>
        host.Equals("api.tuforums.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("cdn.tuforums.com", StringComparison.OrdinalIgnoreCase);

    // Redirect targets are restricted to the same trusted image hosts. YouTube/Bilibili
    // covers never redirect; only the TUF icon CDN (api -> cdn.tuforums.com) does.
    private static bool TrustedImageHost(Uri uri) =>
        uri.Host == Host
        || uri.Host.EndsWith(BilibiliImageHostSuffix, StringComparison.Ordinal)
        || IsTufHost(uri.Host);

    // Bounded GET. When allowRedirectHost is set, up to MaxRedirects hops are followed,
    // each validated HTTPS + on an allowed host (the shared client has auto-redirect off).
    private static byte[]? Download(Uri uri, int maxBytes, Func<Uri, bool>? allowRedirectHost) {
        for(int hop = 0; ; hop++) {
            using HttpResponseMessage response = Client()
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            int code = (int)response.StatusCode;
            if(code is >= 300 and < 400 && allowRedirectHost != null && hop < MaxRedirects) {
                Uri? location = response.Headers.Location;
                if(location == null) return null;
                if(!location.IsAbsoluteUri) location = new Uri(uri, location);
                if(location.Scheme != Uri.UriSchemeHttps || !allowRedirectHost(location)) return null;
                uri = location;
                continue;
            }
            if(!response.IsSuccessStatusCode) return null;
            if(response.Content.Headers.ContentLength > maxBytes) return null;
            return ReadBounded(response.Content.ReadAsStreamAsync().GetAwaiter().GetResult(), maxBytes);
        }
    }

    private static byte[] ReadBounded(Stream stream, int max) {
        using MemoryStream output = new();
        byte[] buffer = new byte[32768];
        int read;
        while((read = stream.Read(buffer, 0, buffer.Length)) > 0) {
            if(output.Length + read > max) throw new InvalidDataException("preview too large");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    // Decode is main-thread only. The source is small, so pulling the pixels is cheap;
    // it is dropped immediately and the blur runs off-thread.
    private static void DecodeOnMain(string key, byte[] jpeg, int gen) {
        if(gen != generation) return;
        Texture2D source = new(2, 2, TextureFormat.RGBA32, false);
        if(!source.LoadImage(jpeg) || source.width < 8 || source.height < 8) {
            UnityEngine.Object.Destroy(source);
            Fail(key, gen);
            return;
        }
        Color32[] pixels = source.GetPixels32();
        int w = source.width, h = source.height;
        UnityEngine.Object.Destroy(source);
        _ = Task.Run(() => {
            try {
                Color32[] blurred = Blur(pixels, ref w, ref h);
                int bw = w, bh = h;
                mainQueue.Enqueue(() => UploadOnMain(key, blurred, bw, bh, gen));
            } catch(Exception e) {
                MainCore.Log.Msg("[TUF] preview blur failed: " + e.Message);
                Fail(key, gen);
            }
        });
    }

    private static void UploadOnMain(string key, Color32[] pixels, int w, int h, int gen) {
        if(gen != generation) return;
        Texture2D texture = new(w, h, TextureFormat.RGB24, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        lock(gate) {
            if(gen != generation) {
                UnityEngine.Object.Destroy(texture);
                return;
            }
            if(!entries.TryGetValue(key, out Entry? entry)) entry = entries[key] = new Entry();
            if(entry.Texture != null) UnityEngine.Object.Destroy(entry.Texture);
            entry.Texture = texture;
            entry.Status = Status.Ready;
            insertionOrder.Enqueue(key);
            EvictIfNeeded();
        }
        Changed?.Invoke();
    }

    // FIFO cap on resident textures. An evicted card just re-blurs from the disk-cached
    // jpg if it scrolls back into view, so dropping the oldest is cheap.
    private static void EvictIfNeeded() {
        while(insertionOrder.Count > MaxTextures) {
            string old = insertionOrder.Dequeue();
            if(!entries.TryGetValue(old, out Entry? entry) || entry.Texture == null) continue;
            UnityEngine.Object.Destroy(entry.Texture);
            entries.Remove(old);
        }
    }

    private static void Fail(string key, int gen) {
        lock(gate) {
            if(gen != generation) return;
            if(entries.TryGetValue(key, out Entry? entry)) entry.Status = Status.Failed;
        }
    }

    private static void EnsurePump() {
        if(pump != null) return;
        if(MainCore.Root == null) return;
        pump = MainCore.Root.AddComponent<TufPreviewPump>();
    }

    // Keyed by the opaque preview key: TUF/Thumbnails/<key>.jpg (levels "512.jpg", packs
    // "pack-RCAXIAv9.jpg"), a sibling of Levels. Keys are already filesystem-safe;
    // sanitize defensively anyway.
    private static string CachePath(string key) {
        char[] safe = key.ToCharArray();
        for(int i = 0; i < safe.Length; i++) {
            char c = safe[i];
            if(!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9') || c == '_' || c == '-')) safe[i] = '_';
        }
        return Path.Combine(MainCore.Paths.TufPath, "Thumbnails", new string(safe) + ".jpg");
    }

    private static HttpClient Client() {
        if(http != null) return http;
        lock(httpGate) {
            if(http != null) return http;
            return http = BuildClient();
        }
    }

    private static HttpClient BuildClient() {
        try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        HttpClient client = new(new HttpClientHandler {
            AllowAutoRedirect = false,
            // Same reason as the TUF API clients: Unity Mono's outdated root store fails
            // otherwise, and every target host is allowlisted over HTTPS.
            ServerCertificateCustomValidationCallback =
                (HttpRequestMessage _, X509Certificate2 _, X509Chain _, SslPolicyErrors _) => true
        }) {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-TUF/1.0");
        return client;
    }

    // ---- blur ----------------------------------------------------------------

    // Box-average downscale to bound the working size, split into three channel planes,
    // run a 3-pass box blur on each (constant time per pixel), recombine. Alpha is
    // dropped — thumbnails are opaque and the card sits on an opaque panel.
    private static Color32[] Blur(Color32[] src, ref int w, ref int h) {
        if(w > MaxBlurDim || h > MaxBlurDim) {
            float scale = Mathf.Min((float)MaxBlurDim / w, (float)MaxBlurDim / h);
            int dw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
            int dh = Mathf.Max(1, Mathf.RoundToInt(h * scale));
            src = Downscale(src, w, h, dw, dh);
            w = dw; h = dh;
        }
        int n = w * h;
        float[] r = new float[n], g = new float[n], b = new float[n], tmp = new float[n];
        for(int i = 0; i < n; i++) {
            r[i] = src[i].r; g[i] = src[i].g; b[i] = src[i].b;
        }
        float sigma = Mathf.Max(0.5f, BlurPx * w / NominalCardWidth);
        int[] radii = BoxRadiiForGauss(sigma, 3);
        BoxBlur(r, tmp, w, h, radii);
        BoxBlur(g, tmp, w, h, radii);
        BoxBlur(b, tmp, w, h, radii);
        Color32[] output = new Color32[n];
        for(int i = 0; i < n; i++)
            output[i] = new Color32(ToByte(r[i]), ToByte(g[i]), ToByte(b[i]), 255);
        return output;
    }

    private static Color32[] Downscale(Color32[] src, int sw, int sh, int dw, int dh) {
        Color32[] dst = new Color32[dw * dh];
        for(int ty = 0; ty < dh; ty++) {
            int sy0 = ty * sh / dh, sy1 = Mathf.Max(sy0 + 1, (ty + 1) * sh / dh);
            for(int tx = 0; tx < dw; tx++) {
                int sx0 = tx * sw / dw, sx1 = Mathf.Max(sx0 + 1, (tx + 1) * sw / dw);
                int ar = 0, ag = 0, ab = 0, count = 0;
                for(int sy = sy0; sy < sy1; sy++) {
                    int row = sy * sw;
                    for(int sx = sx0; sx < sx1; sx++) {
                        Color32 c = src[row + sx];
                        ar += c.r; ag += c.g; ab += c.b; count++;
                    }
                }
                dst[ty * dw + tx] = new Color32((byte)(ar / count), (byte)(ag / count), (byte)(ab / count), 255);
            }
        }
        return dst;
    }

    // Three box passes (horizontal + vertical each) approximate a gaussian. Ping-pongs
    // between the channel buffer and scratch; the result lands back in `channel`.
    private static void BoxBlur(float[] channel, float[] scratch, int w, int h, int[] radii) {
        foreach(int radius in radii) {
            BoxBlurH(channel, scratch, w, h, radius);
            BoxBlurV(scratch, channel, w, h, radius);
        }
    }

    private static void BoxBlurH(float[] src, float[] dst, int w, int h, int radius) {
        if(radius < 1) { Array.Copy(src, dst, src.Length); return; }
        float norm = 1f / (2 * radius + 1);
        for(int y = 0; y < h; y++) {
            int row = y * w;
            float sum = 0f;
            for(int k = -radius; k <= radius; k++) sum += src[row + Mathf.Clamp(k, 0, w - 1)];
            for(int x = 0; x < w; x++) {
                dst[row + x] = sum * norm;
                sum += src[row + Mathf.Clamp(x + radius + 1, 0, w - 1)]
                    - src[row + Mathf.Clamp(x - radius, 0, w - 1)];
            }
        }
    }

    private static void BoxBlurV(float[] src, float[] dst, int w, int h, int radius) {
        if(radius < 1) { Array.Copy(src, dst, src.Length); return; }
        float norm = 1f / (2 * radius + 1);
        for(int x = 0; x < w; x++) {
            float sum = 0f;
            for(int k = -radius; k <= radius; k++) sum += src[Mathf.Clamp(k, 0, h - 1) * w + x];
            for(int y = 0; y < h; y++) {
                dst[y * w + x] = sum * norm;
                sum += src[Mathf.Clamp(y + radius + 1, 0, h - 1) * w + x]
                    - src[Mathf.Clamp(y - radius, 0, h - 1) * w + x];
            }
        }
    }

    // Kovesi's box sizes that best approximate a gaussian of the given sigma over n
    // passes; returned as radii (box width = 2*radius + 1).
    private static int[] BoxRadiiForGauss(float sigma, int n) {
        float wIdeal = Mathf.Sqrt(12f * sigma * sigma / n + 1f);
        int wl = Mathf.FloorToInt(wIdeal);
        if(wl % 2 == 0) wl--;
        int wu = wl + 2;
        float mIdeal = (12f * sigma * sigma - n * wl * wl - 4f * n * wl - 3f * n) / (-4f * wl - 4f);
        int m = Mathf.RoundToInt(mIdeal);
        int[] radii = new int[n];
        for(int i = 0; i < n; i++) radii[i] = Mathf.Max(0, ((i < m ? wl : wu) - 1) / 2);
        return radii;
    }

    private static byte ToByte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
}

// Drains the preview cache's main-thread work under a per-frame budget. One instance,
// parented to the mod root, created lazily on the first preview request.
internal sealed class TufPreviewPump : MonoBehaviour {
    private void Update() => TufPreviewCache.Pump();
}
