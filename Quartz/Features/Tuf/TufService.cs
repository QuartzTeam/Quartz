using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using Quartz.UI;

namespace Quartz.Features.Tuf;

public enum TufMoveState { Idle, Moving, Done, Failed }

public sealed class TufService : IRuntimeService {
    public static TufService Instance { get; private set; }
    public IReadOnlyList<TufLevel> Levels => levels;
    public TufListState State { get; private set; } = TufListState.Idle;
    public string Error { get; private set; } = "";
    public string Query { get; private set; } = "";
    public TufSort Sort { get; private set; } = TufSort.Recent;
    public bool Ascending { get; private set; }
    public bool HasMore { get; private set; }
    public bool LoadingMore { get; private set; }
    public bool IsBusy => actions?.IsBusy ?? false;
    // Local library mode: the list comes from the install index instead of the API.
    public bool ShowInstalled { get; private set; }
    // Shared with TufPackService so a level downloaded/launched from either browser
    // reuses one cache, one download semaphore, and one launcher instance.
    internal TufDownloadService Downloads => downloads;
    internal TufLevelLauncher Launcher => launcher;
    public TufDifficultyFilter DifficultyFilter { get; private set; } = TufDifficultyFilter.AllRanked;
    public int MinDifficultyIndex => DifficultyFilter.MinIndex;
    public int MaxDifficultyIndex => DifficultyFilter.MaxIndex;
    public bool QuantumEnabled => DifficultyFilter.HasQuantum;
    public int QuantumMinIndex => quantumMinIndex;
    public int QuantumMaxIndex => quantumMaxIndex;
    public IReadOnlyList<string> SelectedDifficulties => DifficultyFilter.SelectedDifficulties;
    public event Action Changed = delegate { };

    // Library relocation progress, read by the settings page.
    public TufMoveState MoveState { get; private set; } = TufMoveState.Idle;
    public int MoveDone { get; private set; }
    public int MoveTotal { get; private set; }
    public string MoveError { get; private set; } = "";

    private readonly List<TufLevel> levels = [];
    private TufApiClient api;
    private TufDownloadService downloads;
    private TufLevelLauncher launcher;
    private TufLevelActionRunner actions;
    private CancellationTokenSource listRequest;
    private CancellationTokenSource debounce;
    private CancellationTokenSource moveRequest;
    private int listGeneration;
    private int nextOffset;
    private bool appendFailed;
    private bool disposed;
    private SettingsFile<TufSettings> settings;
    private SettingsFile<TufInstallIndex> index;
    private int quantumMinIndex;
    private int quantumMaxIndex = TufDifficultyFilter.QuantumNames.Count - 1;

    public void Initialize() {
        Instance = this;
        // One filesystem scan per load, not per settings open. UMM reloads in-process,
        // so this is the only point where the answer can legitimately change.
        TufHelperLiteLink.Reset();
        settings = new SettingsFile<TufSettings>(Path.Combine(MainCore.Paths.TufPath, "Settings.json"));
        settings.Load();
        index = new SettingsFile<TufInstallIndex>(Path.Combine(MainCore.Paths.TufPath, "Installed.json"));
        index.Load();
        Sort = settings.Data.GetSort();
        Ascending = settings.Data.Ascending;
        DifficultyFilter = settings.Data.GetDifficultyFilter();
        quantumMinIndex = settings.Data.QuantumMinIndex;
        quantumMaxIndex = settings.Data.QuantumMaxIndex;
        api = new TufApiClient();
        downloads = new TufDownloadService(MainCore.Paths.TufLevelsPath, ResolveInstallRoot);
        launcher = MainCore.Root.AddComponent<TufLevelLauncher>();
        launcher.Initialize(MainCore.Paths.TufLevelsPath, TrustedRoots);
        actions = new TufLevelActionRunner(levels, downloads, launcher, Notify, RecordInstalledLevel);
    }

    public void EnsureLoaded() {
        if(State == TufListState.Idle) Refresh();
    }

    public void SetQuery(string value) {
        string query = TufInput.NormalizeQuery(value);
        if(query == Query) return;
        Query = query;
        InvalidateListRequest();
        CancelDebounce();
        levels.Clear();
        HasMore = false;
        LoadingMore = false;
        nextOffset = 0;
        appendFailed = false;
        // The local library needs no network round trip, so filter it as the user
        // types instead of showing a spinner for 300ms first. Filter only — the
        // prune/adopt disk pass ran when the view was opened, and repeating it per
        // keystroke stats every installed folder on the main thread.
        if(ShowInstalled) {
            RebuildInstalledList();
            return;
        }
        State = TufListState.Loading;
        Error = "";
        Notify();
        debounce = new CancellationTokenSource();
        DebouncedRefresh(debounce.Token);
    }

    private async void DebouncedRefresh(CancellationToken token) {
        try {
            await Task.Delay(300, token);
            if(!token.IsCancellationRequested) {
                if(debounce != null && debounce.Token == token) {
                    debounce.Dispose();
                    debounce = null;
                }
                Fetch(false);
            }
        } catch(OperationCanceledException) { }
    }

    public void SetSort(TufSort value) {
        if(Sort == value) return;
        Sort = value;
        SaveSettings();
        Refresh();
    }

    public void ToggleAscending() {
        Ascending = !Ascending;
        SaveSettings();
        Refresh();
    }

    public void ToggleInstalled() {
        ShowInstalled = !ShowInstalled;
        InvalidateListRequest();
        CancelDebounce();
        levels.Clear();
        HasMore = false;
        LoadingMore = false;
        nextOffset = 0;
        appendFailed = false;
        Refresh();
    }

    public void SetDifficultyRange(int minIndex, int maxIndex) =>
        SetDifficultyFilter(DifficultyFilter.WithRange(minIndex, maxIndex));
    public void ToggleSpecialDifficulty(string name) =>
        SetDifficultyFilter(DifficultyFilter.Toggle(name));
    public void SetQuantumRange(int minIndex, int maxIndex) {
        int last = TufDifficultyFilter.QuantumNames.Count - 1;
        quantumMinIndex = Math.Clamp(minIndex, 0, last);
        quantumMaxIndex = Math.Clamp(maxIndex, 0, last);
        if(quantumMinIndex > quantumMaxIndex) (quantumMinIndex, quantumMaxIndex) = (quantumMaxIndex, quantumMinIndex);
        SetDifficultyFilter(DifficultyFilter.WithQuantumRange(quantumMinIndex, quantumMaxIndex));
    }
    public void ClearQuantum() => SetDifficultyFilter(DifficultyFilter.WithoutQuantum());
    public void ResetDifficultyFilter() => SetDifficultyFilter(TufDifficultyFilter.AllRanked);

    private void SetDifficultyFilter(TufDifficultyFilter filter) {
        if(DifficultyFilter.Equals(filter)) return;
        DifficultyFilter = filter;
        SaveSettings();
        levels.Clear();
        HasMore = false;
        nextOffset = 0;
        Refresh();
    }

    public bool LinkTufHelperLite => settings?.Data.LinkTufHelperLite ?? false;

    public void SetLinkTufHelperLite(bool value) {
        if(settings == null || settings.Data.LinkTufHelperLite == value) return;
        settings.Data.LinkTufHelperLite = value;
        settings.Data.RememberRoot(ResolveInstallRoot().Path);
        settings.RequestSave();
        if(ShowInstalled) LoadInstalled();
        else Notify();
    }

    public string CustomLevelsRoot => settings?.Data.CustomLevelsRoot ?? "";
    public string ActiveRootPath => downloads?.ActiveRoot().Path ?? MainCore.Paths.TufLevelsPath;

    // Installs go to TUFHelperLite's shared folder when that link is on (it is an
    // explicit interop opt-in and names folders differently), otherwise to the
    // user's own library, otherwise to Quartz's cache.
    private TufInstallRoot ResolveInstallRoot() {
        if(settings?.Data.LinkTufHelperLite == true) {
            string helper = TufHelperLiteLink.DownloadsRoot();
            if(!string.IsNullOrEmpty(helper)) return new(helper, true);
        }
        string custom = settings?.Data.CustomLevelsRoot;
        if(!string.IsNullOrWhiteSpace(custom)) {
            try { if(Directory.Exists(custom)) return new(Path.GetFullPath(custom), false); }
            catch { }
        }
        return new(MainCore.Paths.TufLevelsPath, false);
    }

    // Every root a level may legitimately sit in: the cache, the active target, and
    // anywhere the library has previously lived (a move can be interrupted, and old
    // installs stay loadable).
    private IEnumerable<string> TrustedRoots() {
        List<string> roots = [MainCore.Paths.TufLevelsPath, ResolveInstallRoot().Path];
        if(settings != null) roots.AddRange(settings.Data.KnownRoots);
        return roots;
    }

    private void SaveSettings() {
        if(settings == null) return;
        settings.Data.Sort = (int)Sort;
        settings.Data.Ascending = Ascending;
        settings.Data.SetDifficultyFilter(DifficultyFilter, quantumMinIndex, quantumMaxIndex);
        settings.RequestSave();
    }

    public void Refresh() {
        CancelDebounce();
        if(ShowInstalled) LoadInstalled();
        else Fetch(false);
    }
    public void LoadMore() {
        if(HasMore && !LoadingMore
            && (State == TufListState.Ready || (State == TufListState.Error && appendFailed))) Fetch(true);
    }

    // ---- install index -------------------------------------------------------

    // Called by the action runner after a level finishes installing, from either
    // browser, so one index covers Levels and Packs alike.
    internal void RecordInstalledLevel(TufLevel level) {
        if(disposed || index == null || level?.InstallFolder == null) return;
        index.Data.Record(level, level.InstallFolder);
        index.RequestSave();
        settings?.Data.RememberRoot(Path.GetDirectoryName(Path.GetFullPath(level.InstallFolder)));
        settings?.RequestSave();
    }

    // Folders sitting in the active root with no index record: installs from before
    // the index existed, or ones TUFHelperLite made. Their metadata is unknown, so
    // they show as an id until the level appears in a search again.
    private void AdoptOrphans() {
        if(index == null) return;
        TufInstallRoot root = downloads.ActiveRoot();
        bool changed = false;
        try {
            foreach(string dir in Directory.EnumerateDirectories(root.Path)) {
                if(!TufInstallPaths.IsLevelFolderName(Path.GetFileName(dir), out int id)) continue;
                if(index.Data.Find(id) != null) continue;
                if(TufArchive.SelectChart(dir) == null) continue;
                long stamp;
                try { stamp = Directory.GetCreationTimeUtc(dir).Ticks; } catch { stamp = 0; }
                index.Data.Adopt(id, dir, stamp);
                changed = true;
            }
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not scan the level library: " + e.Message);
        }
        if(!changed) return;
        // A root we adopted from is one we may later move or delete out of, and it
        // predates this index — record it or the guard will refuse those levels.
        settings?.Data.RememberRoot(root.Path);
        settings?.RequestSave();
        index.RequestSave();
    }

    private void LoadInstalled() {
        if(index == null) return;
        bool pruned = index.Data.PruneMissing();
        AdoptOrphans();
        if(pruned) index.RequestSave();
        RebuildInstalledList();
    }

    // Filter + sort straight from the in-memory index — no disk IO, so the
    // search-as-you-type path never touches the filesystem.
    private void RebuildInstalledList() {
        if(index == null) return;
        InvalidateListRequest();
        levels.Clear();
        foreach(TufInstallEntry entry in index.Data.Entries) {
            if(!MatchesInstalledFilters(entry)) continue;
            TufLevel level = entry.ToLevel();
            level.InstallFolder = entry.Folder;
            level.InstalledAtUtc = entry.InstalledAtUtc;
            level.State = TufItemState.Load;
            levels.Add(level);
        }
        SortInstalled();
        HasMore = false;
        LoadingMore = false;
        appendFailed = false;
        Error = "";
        State = levels.Count == 0 ? TufListState.Empty : TufListState.Ready;
        Notify();
    }

    private bool MatchesInstalledFilters(TufInstallEntry entry) {
        if(!string.IsNullOrEmpty(Query)) {
            string needle = Query;
            bool hit = Contains(entry.Song, needle) || Contains(entry.Artist, needle)
                || Contains(entry.Creator, needle) || entry.Id.ToString() == needle;
            if(!hit) return false;
        }
        // Adopted folders have no difficulty recorded, and specials are not on the
        // ranked scale; never let the range bar hide a level whose difficulty we
        // cannot place.
        int rank = TufDifficultyFilter.RankOf(entry.Difficulty);
        if(rank < 0) return true;
        return rank >= DifficultyFilter.MinIndex && rank <= DifficultyFilter.MaxIndex;
    }

    private static bool Contains(string haystack, string needle) =>
        !string.IsNullOrEmpty(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void SortInstalled() {
        Comparison<TufLevel> compare = Sort switch {
            TufSort.Clears => (a, b) => a.Clears.CompareTo(b.Clears),
            TufSort.Likes => (a, b) => a.Likes.CompareTo(b.Likes),
            TufSort.Difficulty => (a, b) => InstalledRank(a).CompareTo(InstalledRank(b)),
            // Recent means "recently installed" here, not "recently published" — it is
            // the order that answers "what did I just download".
            _ => (a, b) => a.InstalledAtUtc.CompareTo(b.InstalledAtUtc)
        };
        levels.Sort(compare);
        if(!Ascending) levels.Reverse();
    }

    // Unranked and unknown difficulties sort to the end rather than the top.
    private static int InstalledRank(TufLevel level) {
        int rank = TufDifficultyFilter.RankOf(level.Difficulty);
        return rank < 0 ? int.MaxValue : rank;
    }

    // ---- delete --------------------------------------------------------------

    public void DeleteInstalled(TufLevel level) {
        if(disposed || level == null || index == null || IsBusy) return;
        TufInstallEntry entry = index.Data.Find(level.Id);
        string folder = level.InstallFolder ?? entry?.Folder;
        if(string.IsNullOrEmpty(folder)) return;
        bool removed = downloads.DeleteLevel(level.Id, folder, settings?.Data.KnownRoots);
        if(!removed) {
            // The files are still there, so the card must stay exactly as it is — only
            // the reason is new. It surfaces as the action tooltip (the browser treats
            // a non-empty Error as structural and rebuilds), and DeleteLevel has
            // already written the underlying cause to the log.
            level.Error = MainCore.Tr.Get("TUF_DELETE_FAILED", "Could not delete this level; see the log.");
            Notify();
            return;
        }
        index.Data.Remove(level.Id);
        index.RequestSave();
        if(ShowInstalled) LoadInstalled();
        else {
            // Browsing the API: the row stays, it just reverts to Download.
            level.InstallFolder = null;
            level.State = level.DownloadUri == null ? TufItemState.Unavailable : TufItemState.Download;
            level.Error = "";
            Notify();
        }
    }

    // ---- library location ----------------------------------------------------

    // Points the library at `path` and relocates everything already installed.
    // Validation happens here rather than in the picker so a stale settings file or a
    // folder that vanished cannot silently aim installs somewhere unexpected.
    // The TUFHelperLite link is itself a destination choice, and it wins. Rather
    // than keep two selectors that disagree about where installs go, the folder
    // picker refuses while the link owns the target.
    private bool LinkOwnsTarget => settings?.Data.LinkTufHelperLite == true && TufHelperLiteLink.Installed;

    public bool SetCustomLevelsRoot(string path, out string reason) {
        reason = "";
        if(settings == null || disposed) return false;
        if(MoveState == TufMoveState.Moving) {
            reason = "busy";
            return false;
        }
        if(LinkOwnsTarget) {
            reason = "linked";
            return false;
        }
        string full;
        try { full = Path.GetFullPath(path); } catch { reason = "invalid"; return false; }
        if(!TufInstallPaths.IsUsableLibraryRoot(full, out reason)) return false;
        if(string.Equals(Path.GetFullPath(ResolveInstallRoot().Path), full, PathComparison)) return true;
        if(TufInstallPaths.IsSameOrNested(full, MainCore.Paths.TufLevelsPath)) {
            reason = "nested";
            return false;
        }
        settings.Data.CustomLevelsRoot = full;
        settings.Data.RememberRoot(full);
        settings.Save();
        StartMove(full);
        return true;
    }

    // Back to Quartz's own cache. Also relocates, for the same reason as above.
    public bool ClearCustomLevelsRoot(out string reason) {
        reason = "";
        if(settings == null || disposed) return false;
        if(MoveState == TufMoveState.Moving) {
            reason = "busy";
            return false;
        }
        if(LinkOwnsTarget) {
            reason = "linked";
            return false;
        }
        if(string.IsNullOrEmpty(settings.Data.CustomLevelsRoot)) return true;
        settings.Data.CustomLevelsRoot = "";
        settings.Save();
        StartMove(MainCore.Paths.TufLevelsPath);
        return true;
    }

    // toRoot is passed explicitly rather than re-derived: the pending set and the
    // destination must be the same folder, or levels get filtered against one root
    // and copied into another.
    private void StartMove(string toRoot) {
        moveRequest?.Cancel();
        moveRequest?.Dispose();
        moveRequest = new CancellationTokenSource();
        string destination = Path.GetFullPath(toRoot);
        List<(int Id, string From)> pending = index.Data.Entries
            .Where(e => !string.Equals(Path.GetDirectoryName(e.Folder), destination, PathComparison))
            .Select(e => (e.Id, e.Folder))
            .ToList();
        MoveDone = 0;
        MoveTotal = pending.Count;
        MoveError = "";
        if(pending.Count == 0) {
            MoveState = TufMoveState.Done;
            Notify();
            return;
        }
        MoveState = TufMoveState.Moving;
        Notify();
        // Custom and default libraries both use plain "<id>" naming; only the
        // TUFHelperLite target uses "tuf-<id>", and it never reaches this path.
        MoveLibrary(pending, new TufInstallRoot(destination, false), moveRequest.Token);
    }

    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // Copies run off the main thread — a multi-GB library would freeze the game
    // otherwise. Each level is committed to the index as it lands, so an interrupted
    // move leaves every level pointing at wherever it actually is.
    private async void MoveLibrary(List<(int Id, string From)> pending, TufInstallRoot target, CancellationToken token) {
        // Snapshot on the main thread: the workers below must not read settings.
        List<string> roots = TrustedRoots().ToList();
        int failures = 0;
        string firstError = "";
        foreach((int id, string from) in pending) {
            if(token.IsCancellationRequested) break;
            try {
                string moved = await Task.Run(
                    () => downloads.MoveLevel(id, from, target.Path, target.Linked, roots, token), token);
                MainThread.Enqueue(() => {
                    if(disposed || token.IsCancellationRequested) return;
                    index.Data.SetFolder(id, moved);
                    index.RequestSave();
                    MoveDone++;
                    Notify();
                });
            } catch(OperationCanceledException) {
                break;
            } catch(Exception e) {
                failures++;
                if(firstError.Length == 0) firstError = e.Message;
                MainCore.Log.Wrn($"[TUF] could not move level {id} to the new library: {e}");
                MainThread.Enqueue(() => {
                    if(disposed) return;
                    MoveDone++;
                    Notify();
                });
            }
        }
        MainThread.Enqueue(() => {
            if(disposed) return;
            MoveState = failures > 0 ? TufMoveState.Failed : TufMoveState.Done;
            MoveError = failures > 0
                ? string.Format(MainCore.Tr.Get("TUF_MOVE_FAILED_COUNT",
                    "{0} level(s) could not be moved and stayed where they were: {1}"), failures, firstError)
                : "";
            index.Save();
            if(ShowInstalled) LoadInstalled();
            else Notify();
        });
    }

    private async void Fetch(bool append) {
        listRequest?.Cancel();
        listRequest?.Dispose();
        listRequest = new CancellationTokenSource();
        CancellationToken token = listRequest.Token;
        int generation = ++listGeneration;
        string query = Query;
        TufSort sort = Sort;
        bool ascending = Ascending;
        TufDifficultyFilter filter = DifficultyFilter;
        if(append) {
            LoadingMore = true;
            appendFailed = false;
        }
        else {
            appendFailed = false;
            State = TufListState.Loading;
            Error = "";
        }
        Notify();
        try {
            TufPage page = await api.FetchAsync(query, sort, ascending, append ? nextOffset : 0, filter, token);
            MainThread.Enqueue(() => ApplyPage(page, append, token, generation, query, sort, ascending, filter));
        } catch(OperationCanceledException) { }
        catch(Exception e) {
            MainThread.Enqueue(() => {
                if(!RequestIsCurrent(token, generation, query, sort, ascending, filter)) return;
                LoadingMore = false;
                appendFailed = append;
                State = TufListState.Error;
                Error = e.Message;
                Notify();
            });
        }
    }

    private void ApplyPage(TufPage page, bool append, CancellationToken token, int generation,
        string query, TufSort sort, bool ascending, TufDifficultyFilter filter) {
        if(!RequestIsCurrent(token, generation, query, sort, ascending, filter)) return;
        if(!append) {
            levels.Clear();
            nextOffset = 0;
        }
        nextOffset += page.ConsumedCount;
        HashSet<int> existing = levels.Select(x => x.Id).ToHashSet();
        foreach(TufLevel level in page.Results) {
            if(existing.Add(level.Id)) {
                MarkIfInstalled(level);
                levels.Add(level);
            }
        }
        HasMore = page.HasMore && page.ConsumedCount > 0;
        LoadingMore = false;
        appendFailed = false;
        State = levels.Count == 0 ? TufListState.Empty : TufListState.Ready;
        Notify();
    }

    // Marks an API result that is already on disk, and backfills the index record
    // for one adopted earlier with no metadata — the search that surfaced it is
    // exactly when we learn its song and artist.
    internal void MarkIfInstalled(TufLevel level) {
        if(disposed || index == null || level == null) return;
        TufInstallEntry entry = index.Data.Find(level.Id);
        if(entry != null) {
            if(downloads.TryGetCachedChart(level.Id, entry.Folder, out _)) {
                level.State = TufItemState.Load;
                level.InstallFolder = entry.Folder;
                level.InstalledAtUtc = entry.InstalledAtUtc;
                if(string.IsNullOrEmpty(entry.Song)) {
                    index.Data.Record(level, entry.Folder);
                    index.RequestSave();
                }
                return;
            }
            // Recorded but gone from disk — deleted outside the game.
            index.Data.Remove(level.Id);
            index.RequestSave();
        }
        if(downloads.TryGetCachedChart(level.Id, out _)) {
            level.State = TufItemState.Load;
            level.InstallFolder = downloads.LevelFolder(level.Id);
            RecordInstalledLevel(level);
        }
    }

    private bool RequestIsCurrent(CancellationToken token, int generation, string query,
        TufSort sort, bool ascending, TufDifficultyFilter filter) =>
        !token.IsCancellationRequested && !disposed && generation == listGeneration
        && query == Query && sort == Sort && ascending == Ascending && filter.Equals(DifficultyFilter);

    private void InvalidateListRequest() {
        listRequest?.Cancel();
        listGeneration++;
    }

    private void CancelDebounce() {
        debounce?.Cancel();
        debounce?.Dispose();
        debounce = null;
    }

    public void Act(TufLevel level) => actions?.Act(level);

    // Launch the chart the user picked from the ChooseChart list.
    public void LaunchChart(TufLevel level, string chart) => actions?.LaunchChart(level, chart);

    private void Notify() => Changed?.Invoke();
    public void Dispose() {
        disposed = true;
        settings?.Save();
        index?.Save();
        if(settings != null) SettingsRegistry.Unregister(settings);
        if(index != null) SettingsRegistry.Unregister(index);
        debounce?.Cancel();
        listRequest?.Cancel();
        moveRequest?.Cancel();
        actions?.Dispose();
        downloads?.Cancel();
        launcher?.Cancel();
        debounce?.Dispose();
        listRequest?.Dispose();
        moveRequest?.Dispose();
        downloads?.Dispose();
        api?.Dispose();
        Changed = delegate { };
        levels.Clear();
        if(ReferenceEquals(Instance, this)) Instance = null;
    }
}
