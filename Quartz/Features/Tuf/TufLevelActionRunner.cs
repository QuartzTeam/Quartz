using Quartz.Async;
using Quartz.Core;
using Quartz.UI;

namespace Quartz.Features.Tuf;

// Drives download/extract/launch for a single owner list of levels. Both the
// Levels browser (TufService) and the Packs browser (TufPackService) reuse this
// so the per-item state machine lives in exactly one place. The runner shares the
// download service and launcher across owners: the download semaphore and the
// single launcher instance keep two browsers from acting at the same time.
internal sealed class TufLevelActionRunner {
    private readonly IReadOnlyList<TufLevel> owner;
    private readonly TufDownloadService downloads;
    private readonly TufLevelLauncher launcher;
    private readonly Action notify;
    private CancellationTokenSource actionRequest;
    private int activeLevelId;
    private bool disposed;

    public bool IsBusy => activeLevelId != 0;

    public TufLevelActionRunner(IReadOnlyList<TufLevel> owner, TufDownloadService downloads,
        TufLevelLauncher launcher, Action notify) {
        this.owner = owner;
        this.downloads = downloads;
        this.launcher = launcher;
        this.notify = notify;
    }

    public void Act(TufLevel level) {
        if(level == null || IsBusy
            || level.State is TufItemState.Downloading or TufItemState.Extracting or TufItemState.Loading) return;
        if(level.State == TufItemState.ChooseChart) {
            ExitChoose(level);
            return;
        }
        activeLevelId = level.Id;
        if(downloads.TryGetCachedChart(level.Id, out string cached)) {
            IReadOnlyList<string> charts = downloads.ListCachedCharts(level.Id);
            if(charts.Count > 1) EnterChoose(level, charts);
            else Launch(level, cached);
            return;
        }
        if(level.DownloadUri == null) {
            activeLevelId = 0;
            return;
        }
        actionRequest?.Cancel();
        actionRequest?.Dispose();
        actionRequest = new CancellationTokenSource();
        Update(level, TufItemState.Downloading, 0f, "");
        Download(level, actionRequest.Token);
    }

    // Launch the chart the user picked from the ChooseChart list.
    public void LaunchChart(TufLevel level, string chart) {
        if(level == null || IsBusy || level.State != TufItemState.ChooseChart) return;
        if(level.Charts == null || !level.Charts.Contains(chart, StringComparer.Ordinal)) return;
        activeLevelId = level.Id;
        ExitChoose(level, notify: false);
        Launch(level, chart);
    }

    private void EnterChoose(TufLevel level, IReadOnlyList<string> charts) {
        activeLevelId = 0;
        foreach(TufLevel other in owner)
            if(!ReferenceEquals(other, level) && other.State == TufItemState.ChooseChart) ExitChoose(other, notify: false);
        level.State = TufItemState.ChooseChart;
        level.Progress = 0f;
        level.Error = "";
        level.Charts = charts;
        level.ChartsRoot = downloads.LevelFolder(level.Id);
        notify();
    }

    private void ExitChoose(TufLevel level, bool notify = true) {
        level.Charts = null;
        level.ChartsRoot = null;
        if(level.State == TufItemState.ChooseChart) level.State = TufItemState.Load;
        if(notify) this.notify();
    }

    private async void Download(TufLevel level, CancellationToken token) {
        int lastPercent = -2;
        try {
            await downloads.DownloadAsync(level, (state, progress) => {
                int percent = progress < 0 ? -1 : (int)(progress * 100f);
                if(state == TufItemState.Downloading && percent >= 0 && lastPercent >= 0
                    && percent / 5 == lastPercent / 5) return;
                lastPercent = percent;
                MainThread.Enqueue(() => Update(level, state, progress, ""));
            }, token);
            MainThread.Enqueue(() => {
                if(disposed || token.IsCancellationRequested) return;
                FinishAction(level, TufItemState.Load, "");
            });
        } catch(OperationCanceledException) {
            MainThread.Enqueue(() => FinishAction(level, TufItemState.Download, ""));
        }
        catch(Exception e) {
            MainThread.Enqueue(() => {
                // The card only shows this on hover (and a missing glyph renders as a
                // box), so write the full exception to the log where it is legible and
                // copyable for a bug report.
                MainCore.Log.Wrn($"[TUF] level {level.Id} could not be downloaded or extracted: {e}");
                FinishAction(level, TufItemState.Retry, e.Message);
            });
        }
    }

    private void Launch(TufLevel level, string chart) {
        if(disposed) return;
        Update(level, TufItemState.Loading, 1f, "");
        launcher.Launch(chart, (success, error) => MainThread.Enqueue(() => {
            if(disposed) return;
            if(!success) {
                MainCore.Log.Wrn("[TUF] automatic play failed: " + error);
                // The launcher closed the settings menu before loading; bring it back
                // so the user lands on the TUF page with the Retry button and error
                // instead of a bare scene that looks frozen.
                UICore.Open(true);
            }
            FinishAction(level, success ? TufItemState.Load : TufItemState.Retry, error);
        }));
    }

    private void FinishAction(TufLevel level, TufItemState state, string error) {
        activeLevelId = 0;
        if(disposed) return;
        if(owner.Contains(level)) {
            level.State = state;
            level.Progress = 0f;
            level.Error = error ?? "";
        }
        notify();
    }

    private void Update(TufLevel level, TufItemState state, float progress, string error) {
        if(disposed || !owner.Contains(level)) return;
        level.State = state;
        level.Progress = progress;
        level.Error = error ?? "";
        notify();
    }

    public void Cancel() => actionRequest?.Cancel();

    public void Dispose() {
        disposed = true;
        actionRequest?.Cancel();
        actionRequest?.Dispose();
        actionRequest = null;
        activeLevelId = 0;
    }
}
