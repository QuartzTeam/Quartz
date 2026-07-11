using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Quartz.Features.Tuf;

public sealed class TufApiClient : IDisposable {
    private const int MaxJsonBytes = 2 * 1024 * 1024;
    private static readonly Uri ApiOrigin = new("https://api.tuforums.com/");
    private readonly HttpClient http;
    private CancellationTokenSource staleRequest;

    public TufApiClient() {
        try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
            BaseAddress = ApiOrigin,
            Timeout = TimeSpan.FromSeconds(20)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-TUF/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<TufPage> FetchAsync(string query, TufSort sort, bool ascending, int offset,
        TufDifficultyFilter filter, CancellationToken token) {
        staleRequest?.Cancel();
        staleRequest?.Dispose();
        staleRequest = CancellationTokenSource.CreateLinkedTokenSource(token);
        CancellationToken requestToken = staleRequest.Token;
        string path = BuildPath(query, sort, ascending, offset, filter);
        using HttpResponseMessage response = await http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, requestToken)
            .ConfigureAwait(false);
        if((int)response.StatusCode is >= 300 and < 400) throw new HttpRequestException("Unexpected API redirect.");
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength > MaxJsonBytes) throw new InvalidDataException("TUF response is too large.");
        using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        byte[] bytes = await ReadBoundedAsync(stream, MaxJsonBytes, requestToken).ConfigureAwait(false);
        requestToken.ThrowIfCancellationRequested();
        return Parse(bytes);
    }

    internal static string BuildPath(string query, TufSort sort, bool ascending, int offset,
        TufDifficultyFilter filter) => TufApiQuery.BuildPath(query, sort, ascending, offset, filter);

    internal static TufPage Parse(byte[] bytes) {
        JObject root;
        try {
            using MemoryStream ms = new(bytes, false);
            using StreamReader sr = new(ms);
            using JsonTextReader reader = new(sr) { MaxDepth = 32 };
            root = JObject.Load(reader);
        } catch(Exception e) when(e is JsonException or InvalidOperationException) {
            throw new InvalidDataException("TUF returned malformed data.", e);
        }
        if(root["results"] is not JArray results || results.Count > 50)
            throw new InvalidDataException("TUF returned an invalid result count.");
        List<TufLevel> levels = [];
        foreach(JToken token in results) {
            int id = token.Value<int?>("id") ?? 0;
            if(id <= 0) continue;
            string link = token.Value<string>("dlLink");
            Uri.TryCreate(link, UriKind.Absolute, out Uri download);
            if(!TufNetworkPolicy.IsAllowedDownloadUri(download)) download = null;
            JToken diff = token["difficulty"];
            levels.Add(new TufLevel(
                id,
                TufInput.CapDisplay(token.Value<string>("song"), "Unknown song"),
                TufInput.CapDisplay(token.Value<string>("artist"), "Unknown artist"),
                TufInput.CapDisplay(token.Value<string>("creator") ?? token.Value<string>("charter"), "Unknown creator"),
                TufInput.CapDisplay(diff?.Value<string>("name"), "Unranked", 40),
                TufInput.NormalizeColor(diff?.Value<string>("color")),
                Math.Max(0, token.Value<int?>("clears") ?? 0),
                Math.Max(0, token.Value<int?>("likes") ?? 0),
                download
            ));
        }
        return new TufPage(levels, root.Value<bool?>("hasMore") == true, results.Count);
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

    public void Dispose() {
        staleRequest?.Cancel();
        staleRequest?.Dispose();
        http.Dispose();
    }
}
