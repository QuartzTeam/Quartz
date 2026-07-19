#nullable enable
using System.Text;

namespace Quartz.Features.Tuf;

public enum TufSort { Recent, Difficulty, Clears, Likes }
public enum TufListState { Idle, Loading, Ready, Empty, Error }
public enum TufItemState { Download, Downloading, Extracting, Loading, Load, Retry, Unavailable, ChooseChart }

public sealed class TufDifficultyFilter : IEquatable<TufDifficultyFilter> {
    public static readonly IReadOnlyList<string> RankedNames = BuildRankedNames();
    private static readonly Dictionary<string, int> RankIndex = BuildRankIndex();

    // Rank of a difficulty name ("G15"), or -1 for specials and unknowns. Used by the
    // Installed view's sort comparator, so it must not allocate per call.
    public static int RankOf(string? name) =>
        name != null && RankIndex.TryGetValue(name, out int rank) ? rank : -1;
    public static readonly IReadOnlyList<string> SpecialNames = Array.AsReadOnly(new[] { "Unranked", "Censored", "Impossible" });
    public static readonly IReadOnlyList<string> QuantumNames = Array.AsReadOnly(new[] {
        "Qq", "GQ0 (G1~G4)", "GQ1 (G5~G8)", "GQ2 (G9~G12)", "GQ3 (G13~G16)",
        "GQ4 (G17~G20)", "UQ0 (U1~U4)", "UQ1 (U5~U8)", "UQ2 (U9~U12)",
        "UQ3 (U13~U16)", "UQ4 (U17~U20)"
    });
    public static TufDifficultyFilter AllRanked => new(0, RankedNames.Count - 1, Array.Empty<string>());

    public int MinIndex { get; }
    public int MaxIndex { get; }
    public string MinName => RankedNames[MinIndex];
    public string MaxName => RankedNames[MaxIndex];
    public IReadOnlyList<string> SelectedDifficulties { get; }

    public TufDifficultyFilter(int minIndex, int maxIndex, IEnumerable<string>? selected = null) {
        int last = RankedNames.Count - 1;
        minIndex = Math.Clamp(minIndex, 0, last);
        maxIndex = Math.Clamp(maxIndex, 0, last);
        if(minIndex > maxIndex) (minIndex, maxIndex) = (maxIndex, minIndex);
        MinIndex = minIndex;
        MaxIndex = maxIndex;
        HashSet<string> requested = new(selected ?? Array.Empty<string>(), StringComparer.Ordinal);
        SelectedDifficulties = Array.AsReadOnly(
            SpecialNames.Concat(QuantumNames).Where(requested.Contains).ToArray());
    }

    public TufDifficultyFilter WithRange(int minIndex, int maxIndex) =>
        new(minIndex, maxIndex, SelectedDifficulties);
    public TufDifficultyFilter Toggle(string name) {
        if(!SpecialNames.Contains(name) && !QuantumNames.Contains(name)) return this;
        HashSet<string> selected = new(SelectedDifficulties, StringComparer.Ordinal);
        if(!selected.Add(name)) selected.Remove(name);
        return new(MinIndex, MaxIndex, selected);
    }
    public bool IsSelected(string name) => SelectedDifficulties.Contains(name);

    public static bool IsQuantum(string name) => QuantumNames.Contains(name);
    public bool HasQuantum => SelectedDifficulties.Any(IsQuantum);
    // Quantum is selected as a contiguous range over QuantumNames; these derive the
    // slider's endpoints from the stored name set (both round-trip WithQuantumRange).
    public int QuantumMinIndex {
        get {
            for(int i = 0; i < QuantumNames.Count; i++)
                if(SelectedDifficulties.Contains(QuantumNames[i])) return i;
            return 0;
        }
    }
    public int QuantumMaxIndex {
        get {
            for(int i = QuantumNames.Count - 1; i >= 0; i--)
                if(SelectedDifficulties.Contains(QuantumNames[i])) return i;
            return QuantumNames.Count - 1;
        }
    }
    // Replace the quantum portion of the selection with the contiguous range
    // [minIndex, maxIndex], leaving any special difficulties untouched.
    public TufDifficultyFilter WithQuantumRange(int minIndex, int maxIndex) {
        int last = QuantumNames.Count - 1;
        minIndex = Math.Clamp(minIndex, 0, last);
        maxIndex = Math.Clamp(maxIndex, 0, last);
        if(minIndex > maxIndex) (minIndex, maxIndex) = (maxIndex, minIndex);
        List<string> selected = SelectedDifficulties.Where(SpecialNames.Contains).ToList();
        for(int i = minIndex; i <= maxIndex; i++) selected.Add(QuantumNames[i]);
        return new(MinIndex, MaxIndex, selected);
    }
    public TufDifficultyFilter WithoutQuantum() =>
        new(MinIndex, MaxIndex, SelectedDifficulties.Where(SpecialNames.Contains));

    public bool Equals(TufDifficultyFilter? other) => other != null && MinIndex == other.MinIndex
        && MaxIndex == other.MaxIndex && SelectedDifficulties.SequenceEqual(other.SelectedDifficulties);
    public override bool Equals(object? obj) => Equals(obj as TufDifficultyFilter);
    public override int GetHashCode() {
        int hash = HashCode.Combine(MinIndex, MaxIndex);
        foreach(string name in SelectedDifficulties) hash = HashCode.Combine(hash, name);
        return hash;
    }

    private static IReadOnlyList<string> BuildRankedNames() {
        List<string> values = new(60);
        foreach(string band in new[] { "P", "G", "U" })
            for(int i = 1; i <= 20; i++) values.Add(band + i);
        return values.AsReadOnly();
    }

    private static Dictionary<string, int> BuildRankIndex() {
        Dictionary<string, int> index = new(RankedNames.Count, StringComparer.Ordinal);
        for(int i = 0; i < RankedNames.Count; i++) index[RankedNames[i]] = i;
        return index;
    }
}

public sealed class TufLevel {
    public int Id { get; }
    public string Song { get; }
    public string Artist { get; }
    public string Creator { get; }
    public string Difficulty { get; }
    public string DifficultyColor { get; }
    public int Clears { get; }
    public int Likes { get; }
    public Uri? DownloadUri { get; }
    // The level's showcase video (YouTube, Bilibili, …), straight from the API. Only
    // YouTube links become a preview thumbnail; see TufThumbnail. Empty when unknown.
    public string VideoLink { get; set; } = "";
    // Ordering key for difficulty sorts (difficulties endpoint sortOrder). Only
    // populated for pack levels; the plain browser sorts server-side.
    public int DifficultyRank { get; set; }
    public TufItemState State { get; set; }
    public float Progress { get; set; }
    public string Error { get; set; } = "";
    // Populated only while State == ChooseChart: the cached level folder and the
    // full paths of every playable chart in it, in SelectChart preference order.
    public IReadOnlyList<string>? Charts { get; set; }
    public string? ChartsRoot { get; set; }
    // Where this level is installed, once it is. Read from the install index rather
    // than derived from the active root, so a level still resolves after the library
    // moves or a move is interrupted partway. Null means "not installed".
    public string? InstallFolder { get; set; }
    public long InstalledAtUtc { get; set; }

    public TufLevel(int id, string song, string artist, string creator, string difficulty,
        string difficultyColor, int clears, int likes, Uri? downloadUri) {
        Id = id;
        Song = song;
        Artist = artist;
        Creator = creator;
        Difficulty = difficulty;
        DifficultyColor = difficultyColor;
        Clears = clears;
        Likes = likes;
        DownloadUri = downloadUri;
        State = downloadUri == null ? TufItemState.Unavailable : TufItemState.Download;
    }
}

public sealed class TufPage {
    public IReadOnlyList<TufLevel> Results { get; }
    public bool HasMore { get; }
    public int ConsumedCount { get; }
    public TufPage(IReadOnlyList<TufLevel> results, bool hasMore, int consumedCount = -1) {
        Results = results ?? Array.Empty<TufLevel>();
        HasMore = hasMore;
        ConsumedCount = consumedCount < 0 ? Results.Count : Math.Max(0, consumedCount);
    }
}

public static class TufInput {
    public static string NormalizeQuery(string? value) {
        string normalized = (value ?? "").Normalize(NormalizationForm.FormKC);
        string[] parts = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string result = string.Join(" ", parts);
        return result.Length <= 128 ? result : result[..128];
    }

    public static string CapDisplay(string? value, string fallback, int max = 120) {
        string clean = string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if(string.IsNullOrWhiteSpace(clean)) return fallback;
        return clean.Length <= max ? clean : clean[..max];
    }

    public static string NormalizeColor(string? value) {
        if(value?.Length != 7 || value[0] != '#') return "#FFFFFF";
        for(int i = 1; i < value.Length; i++)
            if(!Uri.IsHexDigit(value[i])) return "#FFFFFF";
        return value.ToUpperInvariant();
    }
}
