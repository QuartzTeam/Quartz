using System.Net;
using Quartz.Core;

namespace Quartz.Features.Tuf;

public sealed class TufDownloadService : IDisposable {
    private const long MaxArchiveBytes = 512L * 1024 * 1024;
    private readonly string levelsRoot;
    private readonly HttpClient http;
    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private CancellationTokenSource active;

    // Cache layout version. v2 flattens the zip's "Artist - Title" wrapper folder so
    // charts live directly in Levels/<id>/; older caches are wiped once on upgrade.
    private const string LayoutMarker = ".layout-v2";

    public TufDownloadService(string levelsRoot) {
        this.levelsRoot = Path.GetFullPath(levelsRoot);
        Directory.CreateDirectory(this.levelsRoot);
        MigrateLayout();
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
            Timeout = Timeout.InfiniteTimeSpan
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-TUF/1.0");
    }

    private void MigrateLayout() {
        string marker = Path.Combine(levelsRoot, LayoutMarker);
        if(File.Exists(marker)) return;
        try {
            foreach(string dir in Directory.GetDirectories(levelsRoot))
                try { Directory.Delete(dir, true); } catch { }
            foreach(string file in Directory.GetFiles(levelsRoot))
                try { File.Delete(file); } catch { }
            File.WriteAllText(marker, "2");
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not migrate the level cache layout: " + e.Message);
        }
    }

    public bool TryGetCachedChart(int id, out string chart) {
        chart = null;
        if(id <= 0) return false;
        try {
            chart = TufArchive.SelectChart(LevelFolder(id));
            return chart != null && TufArchive.IsChartUnderRoot(chart, levelsRoot);
        } catch { return false; }
    }

    public string LevelFolder(int id) => Path.Combine(levelsRoot, id.ToString());

    // All playable charts cached for the level, preference-ordered and root-validated.
    public IReadOnlyList<string> ListCachedCharts(int id) {
        if(id <= 0) return Array.Empty<string>();
        try {
            return TufArchive.ListCharts(LevelFolder(id))
                .Where(c => TufArchive.IsChartUnderRoot(c, levelsRoot)).ToList();
        } catch { return Array.Empty<string>(); }
    }

    public async Task<string> DownloadAsync(TufLevel level, Action<TufItemState, float> progress, CancellationToken token) {
        if(level == null || level.Id <= 0 || !TufNetworkPolicy.IsAllowedDownloadUri(level.DownloadUri))
            throw new InvalidDataException("Level has no safe download URL.");
        if(TryGetCachedChart(level.Id, out string cached)) return cached;
        string part = Path.Combine(levelsRoot, level.Id + ".part");
        string extracting = Path.Combine(levelsRoot, level.Id + ".extracting");
        string final = Path.Combine(levelsRoot, level.Id.ToString());
        bool acquired = false;
        try {
            acquired = await oneAtATime.WaitAsync(0, token).ConfigureAwait(false);
            if(!acquired) throw new InvalidOperationException("Another TUF download is active.");
            active = CancellationTokenSource.CreateLinkedTokenSource(token);
            active.CancelAfter(TimeSpan.FromMinutes(10));
            CleanupFile(part);
            CleanupDirectory(extracting);
            progress?.Invoke(TufItemState.Downloading, 0f);
            Uri finalUri = await DownloadToFileAsync(level.DownloadUri, part, progress, active.Token)
                .ConfigureAwait(false);
            progress?.Invoke(TufItemState.Extracting, 1f);
            int skipped = TufArchive.Extract(part, extracting);
            TufArchive.FlattenSingleRoot(extracting);
            if(skipped > 0)
                MainCore.Log.Wrn($"[TUF] level {level.Id}: skipped {skipped} archive entr{(skipped == 1 ? "y" : "ies")} "
                    + "that could not be decompressed or written (unsupported zip method or illegal filename).");
            string archiveStem = Uri.UnescapeDataString(Path.GetFileName(finalUri.AbsolutePath));
            string chart = TufArchive.SelectChart(extracting, archiveStem);
            if(chart == null) throw new InvalidDataException("Archive contains no playable .adofai chart.");
            string relativeChart = Path.GetRelativePath(extracting, chart);
            if(Directory.Exists(final)) Directory.Delete(final, true);
            Directory.Move(extracting, final);
            string installed = Path.GetFullPath(Path.Combine(final, relativeChart));
            if(!TufArchive.IsChartUnderRoot(installed, levelsRoot)) throw new InvalidDataException("Installed chart path is unsafe.");
            return installed;
        } finally {
            if(acquired) {
                CleanupFile(part);
                CleanupDirectory(extracting);
                active?.Dispose();
                active = null;
                oneAtATime.Release();
            }
        }
    }

    private async Task<Uri> DownloadToFileAsync(Uri start, string path, Action<TufItemState, float> progress, CancellationToken token) {
        Uri current = start;
        for(int redirects = 0; redirects <= 5; redirects++) {
            await TufNetworkPolicy.EnsurePublicHostAsync(current, token).ConfigureAwait(false);
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            if((int)response.StatusCode is >= 300 and < 400) {
                if(redirects == 5 || response.Headers.Location == null) throw new HttpRequestException("Too many TUF download redirects.");
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location : new Uri(current, response.Headers.Location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            long? length = response.Content.Headers.ContentLength;
            if(length > MaxArchiveBytes) throw new InvalidDataException("Level archive is too large.");
            using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            byte[] buffer = new byte[65536];
            long total = 0;
            while(true) {
                int read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if(read == 0) break;
                total += read;
                if(total > MaxArchiveBytes) throw new InvalidDataException("Level archive is too large.");
                await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                progress?.Invoke(TufItemState.Downloading,
                    length is > 0 ? Math.Min(1f, (float)total / length.Value) : -1f);
            }
            return current;
        }
        throw new HttpRequestException("TUF download redirect failed.");
    }

    public void Cancel() => active?.Cancel();
    public void Dispose() {
        Cancel();
        http.Dispose();
    }
    private static void CleanupFile(string path) { try { if(File.Exists(path)) File.Delete(path); } catch { } }
    private static void CleanupDirectory(string path) { try { if(Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}
