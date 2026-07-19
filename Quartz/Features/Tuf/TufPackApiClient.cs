#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Quartz.Features.Tuf;

// Read-only client for the TUF pack endpoints. Mirrors TufApiClient's hardening:
// no redirects, bounded response size, HTTPS-only origin. All parsing is defensive
// because the payloads are attacker-influenceable community data.
public sealed class TufPackApiClient : IDisposable {
    private const int MaxListJsonBytes = 2 * 1024 * 1024;
    private const int MaxTreeJsonBytes = 12 * 1024 * 1024;
    private const int MaxDifficultiesJsonBytes = 2 * 1024 * 1024;
    private const int MaxPackLevels = 5000;
    private static readonly Uri ApiOrigin = new("https://api.tuforums.com/");
    private readonly HttpClient http;

    public TufPackApiClient() {
#pragma warning disable SYSLIB0014 // legacy TLS knob still matters under the game's Mono runtime
        try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
#pragma warning restore SYSLIB0014
        http = new HttpClient(new HttpClientHandler {
            AllowAutoRedirect = false,
            // Unity Mono's UnityTLS fails certificate verification (UNITYTLS_X509VERIFY_NOT_DONE)
            // on many systems because its root cert store is outdated. All request targets are
            // already domain-allowlisted via TufNetworkPolicy, so bypassing the broken verifier is safe.
            ServerCertificateCustomValidationCallback = (HttpRequestMessage _, X509Certificate2 _, X509Chain _, SslPolicyErrors _) => true
        }) {
            BaseAddress = ApiOrigin,
            Timeout = TimeSpan.FromSeconds(20)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-TUF/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<TufPacksPage> FetchPacksAsync(string query, TufPackSort sort, bool ascending, int offset,
        int limit, CancellationToken token) {
        string order = sort switch { TufPackSort.Name => "NAME", TufPackSort.Levels => "LEVELS", _ => "RECENT" };
        string path = "v2/database/levels/packs?limit=" + Math.Clamp(limit, 1, 50)
            + "&offset=" + Math.Max(0, offset)
            + "&sort=" + order
            + "&order=" + (ascending ? "ASC" : "DESC")
            + "&query=" + Uri.EscapeDataString(TufInput.NormalizeQuery(query));
        byte[] bytes = await GetAsync(path, MaxListJsonBytes, token).ConfigureAwait(false);
        return ParsePacks(bytes);
    }

    public async Task<TufDifficultyDictionary> FetchDifficultiesAsync(CancellationToken token) {
        byte[] bytes = await GetAsync("v2/database/difficulties", MaxDifficultiesJsonBytes, token).ConfigureAwait(false);
        return ParseDifficulties(bytes);
    }

    public async Task<IReadOnlyList<TufPackItem>> FetchPackItemsAsync(string packId, TufDifficultyDictionary difficulties,
        CancellationToken token) {
        if(!IsValidPackId(packId)) throw new InvalidDataException("Invalid pack id.");
        byte[] bytes = await GetAsync("v2/database/levels/packs/" + Uri.EscapeDataString(packId) + "?tree=true",
            MaxTreeJsonBytes, token).ConfigureAwait(false);
        return ParsePackItems(bytes, difficulties);
    }

    // Pack ids are short opaque link codes ("RCAXIAv9"). Bound + charset-check them
    // before they are ever embedded in a request path.
    internal static bool IsValidPackId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 64
        && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    private async Task<byte[]> GetAsync(string path, int maxBytes, CancellationToken token) {
        using HttpResponseMessage response = await http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);
        if((int)response.StatusCode is >= 300 and < 400) throw new HttpRequestException("Unexpected API redirect.");
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength > maxBytes) throw new InvalidDataException("TUF response is too large.");
        using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        byte[] bytes = await ReadBoundedAsync(stream, maxBytes, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return bytes;
    }

    private static JToken LoadJson(byte[] bytes) {
        try {
            using MemoryStream ms = new(bytes, false);
            using StreamReader sr = new(ms);
            using JsonTextReader reader = new(sr) { MaxDepth = 64 };
            return JToken.ReadFrom(reader);
        } catch(Exception e) when(e is JsonException or InvalidOperationException) {
            throw new InvalidDataException("TUF returned malformed data.", e);
        }
    }

    internal static TufPacksPage ParsePacks(byte[] bytes) {
        if(LoadJson(bytes) is not JObject root) throw new InvalidDataException("TUF returned malformed pack data.");
        if(root["packs"] is not JArray packs || packs.Count > 100)
            throw new InvalidDataException("TUF returned an invalid pack count.");
        List<TufPack> result = [];
        foreach(JToken token in packs) {
            string? id = token.Value<string>("id");
            if(!IsValidPackId(id)) continue;
            JToken? owner = token["packOwner"];
            string ownerName = TufInput.CapDisplay(
                owner?.Value<string>("nickname") ?? owner?.Value<string>("username"), "Unknown", 60);
            List<string> preview = [];
            int firstLevelId = 0;
            if(token["packItems"] is JArray items)
                foreach(JToken item in items) {
                    JToken referenced = item["referencedLevel"];
                    if(firstLevelId <= 0) firstLevelId = Math.Max(0, SafeInt(referenced, "id"));
                    string song = referenced?.Value<string>("song") ?? "";
                    if(!string.IsNullOrWhiteSpace(song)) preview.Add(TufInput.CapDisplay(song, "", 60));
                    if(preview.Count >= 3) break;
                }
            int levelCount = SafeInt(token, "levelCount");
            if(levelCount <= 0) levelCount = SafeInt(token, "totalLevelCount");
            result.Add(new TufPack(
                id!,
                TufInput.CapDisplay(token.Value<string>("name"), "Untitled pack", 80),
                ownerName,
                Math.Max(0, levelCount),
                Math.Max(0, SafeInt(token, "favoritesCount")),
                preview.AsReadOnly()) {
                    IconUrl = TufInput.CapDisplay(token.Value<string>("iconUrl"), "", 300),
                    FirstLevelId = firstLevelId
                });
        }
        return new TufPacksPage(result, Math.Max(SafeInt(root, "total"), result.Count));
    }

    internal static TufDifficultyDictionary ParseDifficulties(byte[] bytes) {
        if(LoadJson(bytes) is not JArray array) return TufDifficultyDictionary.Empty;
        Dictionary<int, (string, string, int)> map = new();
        foreach(JToken token in array) {
            int id = SafeInt(token, "id");
            if(id <= 0) continue;
            string name = TufInput.CapDisplay(token.Value<string>("name"), "Unranked", 40);
            string color = TufInput.NormalizeColor(token.Value<string>("color"));
            map[id] = (name, color, SafeInt(token, "sortOrder"));
        }
        return new TufDifficultyDictionary(map);
    }

    internal static IReadOnlyList<TufPackItem> ParsePackItems(byte[] bytes, TufDifficultyDictionary difficulties) {
        JToken root = LoadJson(bytes);
        JToken? items = root["items"] ?? (root["pack"]?["items"]);
        if(items is not JArray array) return Array.Empty<TufPackItem>();
        HashSet<int> seen = [];
        int total = 0;
        long syntheticKey = -1;
        return BuildItems(array, difficulties, seen, ref total, ref syntheticKey);
    }

    // Flat, on-site-ordered level list derived from the tree (used by tests and by
    // the service to seed the shared action runner's owner list).
    internal static IReadOnlyList<TufLevel> ParsePackLevels(byte[] bytes, TufDifficultyDictionary difficulties) {
        List<TufLevel> levels = [];
        FlattenLevels(ParsePackItems(bytes, difficulties), levels);
        return levels;
    }

    internal static void FlattenLevels(IReadOnlyList<TufPackItem> items, List<TufLevel> output) {
        foreach(TufPackItem item in items) {
            if(item.Level != null) output.Add(item.Level);
            if(item.Children.Count > 0) FlattenLevels(item.Children, output);
        }
    }

    // Depth-first over the folder/level tree, preserving each container's sortOrder so
    // the tree mirrors the pack's on-site ordering. Levels are de-duplicated by id;
    // folders keep their identity for the UI's expand/collapse state.
    private static IReadOnlyList<TufPackItem> BuildItems(JArray items, TufDifficultyDictionary difficulties,
        HashSet<int> seen, ref int total, ref long syntheticKey) {
        List<TufPackItem> result = [];
        foreach(JToken item in items.OrderBy(t => SafeInt(t, "sortOrder"))) {
            if(total >= MaxPackLevels) break;
            long key = item.Value<long?>("id") ?? syntheticKey--;
            string type = item.Value<string>("type") ?? "";
            if(type == "folder") {
                IReadOnlyList<TufPackItem> children = item["children"] is JArray childArray
                    ? BuildItems(childArray, difficulties, seen, ref total, ref syntheticKey)
                    : Array.Empty<TufPackItem>();
                result.Add(new TufPackItem(key,
                    TufInput.CapDisplay(item.Value<string>("name"), "Folder", 60), children));
                continue;
            }
            JToken? level = item["referencedLevel"];
            if(level is not JObject) continue;
            int id = SafeInt(level, "id");
            if(id <= 0) id = SafeInt(item, "levelId");
            if(id <= 0 || !seen.Add(id)) continue;
            string? link = level.Value<string>("dlLink") ?? level.Value<string>("dl");
            Uri.TryCreate(link, UriKind.Absolute, out Uri? download);
            if(!TufNetworkPolicy.IsAllowedDownloadUri(download!)) download = null;
            (string diffName, string diffColor) = difficulties.Resolve(SafeInt(level, "diffId"));
            total++;
            result.Add(new TufPackItem(key, new TufLevel(
                id,
                TufInput.CapDisplay(level.Value<string>("song"), "Unknown song"),
                TufInput.CapDisplay(level.Value<string>("artist"), "Unknown artist"),
                TufInput.CapDisplay(ExtractCreator(level), "Unknown creator"),
                diffName,
                diffColor,
                Math.Max(0, SafeInt(level, "clears")),
                Math.Max(0, SafeInt(level, "likes")),
                download) {
                    DifficultyRank = difficulties.RankOf(SafeInt(level, "diffId")),
                    VideoLink = TufInput.CapDisplay(level.Value<string>("videoLink"), "", 300)
                }));
        }
        return result;
    }

    // Tree levels carry no flat "creator" string; the charters live in
    // levelCredits[].creator.name. Prefer charter-role credits, else any credit.
    private static string? ExtractCreator(JToken level) {
        string? flat = level.Value<string>("creator") ?? level.Value<string>("charter");
        if(!string.IsNullOrWhiteSpace(flat)) return flat;
        if(level["levelCredits"] is not JArray credits) return null;
        List<string> charters = [];
        List<string> others = [];
        foreach(JToken credit in credits) {
            string? name = credit["creator"]?.Value<string>("name");
            if(string.IsNullOrWhiteSpace(name)) continue;
            string role = credit.Value<string>("role") ?? "";
            (role.Contains("charter", StringComparison.OrdinalIgnoreCase) ? charters : others).Add(name);
        }
        List<string> chosen = charters.Count > 0 ? charters : others;
        if(chosen.Count == 0) return null;
        return chosen.Count <= 3 ? string.Join(" & ", chosen) : string.Join(" & ", chosen.Take(3)) + " …";
    }

    // The API is community data; a field that should be a number can arrive as any
    // token type. Json.NET conversions throw FormatException on garbage — swallow to 0.
    private static int SafeInt(JToken? token, string name) {
        try { return token?.Value<int?>(name) ?? 0; } catch { return 0; }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int max, CancellationToken token) {
        using MemoryStream output = new();
        byte[] buffer = new byte[32768];
        while(true) {
            int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            if(read == 0) return output.ToArray();
            if(output.Length + read > max) throw new InvalidDataException("TUF response is too large.");
            output.Write(buffer, 0, read);
        }
    }

    public void Dispose() => http.Dispose();
}
