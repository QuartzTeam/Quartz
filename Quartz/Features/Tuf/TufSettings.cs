#nullable enable
using Newtonsoft.Json.Linq;
using Quartz.IO.Interface;

namespace Quartz.Features.Tuf;

public sealed class TufSettings : ISettingsFile {
    public int Sort = (int)TufSort.Recent;
    public bool Ascending;
    public int MinDifficultyIndex;
    public int MaxDifficultyIndex = TufDifficultyFilter.RankedNames.Count - 1;
    public bool QuantumEnabled;
    public int QuantumMinIndex;
    public int QuantumMaxIndex = TufDifficultyFilter.QuantumNames.Count - 1;
    public List<string> SpecialDifficulties = [];
    public bool LinkTufHelperLite;
    // Blurred YouTube thumbnail behind each browser card. On by default; off stops the
    // thumbnail downloads and frees the cached textures.
    public bool ShowPreviews = true;
    // Empty = install into Quartz's own Levels cache. Set = the folder the user
    // picked instead (typically on a roomier drive).
    public string CustomLevelsRoot = "";
    // Every root the library has ever lived at. Delete and move validate an index
    // path against this set, so a corrupt or hand-edited index cannot point a
    // recursive delete at a folder we never owned. Bounded: one entry per folder
    // change, deduped.
    public List<string> KnownRoots = [];

    public TufSort GetSort() => Enum.IsDefined(typeof(TufSort), Sort)
        ? (TufSort)Sort
        : TufSort.Recent;

    public TufDifficultyFilter GetDifficultyFilter() {
        TufDifficultyFilter filter = new(MinDifficultyIndex, MaxDifficultyIndex, SpecialDifficulties);
        return QuantumEnabled
            ? filter.WithQuantumRange(QuantumMinIndex, QuantumMaxIndex)
            : filter;
    }

    public void SetDifficultyFilter(TufDifficultyFilter filter, int quantumMinIndex, int quantumMaxIndex) {
        MinDifficultyIndex = filter.MinIndex;
        MaxDifficultyIndex = filter.MaxIndex;
        QuantumEnabled = filter.HasQuantum;
        QuantumMinIndex = Math.Clamp(quantumMinIndex, 0, TufDifficultyFilter.QuantumNames.Count - 1);
        QuantumMaxIndex = Math.Clamp(quantumMaxIndex, QuantumMinIndex, TufDifficultyFilter.QuantumNames.Count - 1);
        SpecialDifficulties = filter.SelectedDifficulties
            .Where(TufDifficultyFilter.SpecialNames.Contains).ToList();
    }

    public JToken Serialize() => new JObject {
        [nameof(Sort)] = Sort,
        [nameof(Ascending)] = Ascending,
        [nameof(MinDifficultyIndex)] = MinDifficultyIndex,
        [nameof(MaxDifficultyIndex)] = MaxDifficultyIndex,
        [nameof(QuantumEnabled)] = QuantumEnabled,
        [nameof(QuantumMinIndex)] = QuantumMinIndex,
        [nameof(QuantumMaxIndex)] = QuantumMaxIndex,
        [nameof(SpecialDifficulties)] = new JArray(SpecialDifficulties),
        [nameof(LinkTufHelperLite)] = LinkTufHelperLite,
        [nameof(ShowPreviews)] = ShowPreviews,
        [nameof(CustomLevelsRoot)] = CustomLevelsRoot,
        [nameof(KnownRoots)] = new JArray(KnownRoots)
    };

    public void RememberRoot(string? root) {
        if(string.IsNullOrWhiteSpace(root)) return;
        string full;
        try { full = Path.GetFullPath(root); } catch { return; }
        if(!KnownRoots.Any(r => string.Equals(r, full, PathComparison))) KnownRoots.Add(full);
    }

    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public void Deserialize(JToken token) {
        Sort = Read(token, nameof(Sort), Sort);
        if(!Enum.IsDefined(typeof(TufSort), Sort)) Sort = (int)TufSort.Recent;
        Ascending = Read(token, nameof(Ascending), Ascending);
        MinDifficultyIndex = Read(token, nameof(MinDifficultyIndex), MinDifficultyIndex);
        MaxDifficultyIndex = Read(token, nameof(MaxDifficultyIndex), MaxDifficultyIndex);
        QuantumEnabled = Read(token, nameof(QuantumEnabled), QuantumEnabled);
        QuantumMinIndex = Read(token, nameof(QuantumMinIndex), QuantumMinIndex);
        QuantumMaxIndex = Read(token, nameof(QuantumMaxIndex), QuantumMaxIndex);
        LinkTufHelperLite = Read(token, nameof(LinkTufHelperLite), LinkTufHelperLite);
        ShowPreviews = Read(token, nameof(ShowPreviews), ShowPreviews);
        CustomLevelsRoot = Read(token, nameof(CustomLevelsRoot), CustomLevelsRoot) ?? "";
        KnownRoots.Clear();
        if(token[nameof(KnownRoots)] is JArray roots) {
            foreach(JToken root in roots)
                if(root.Type == JTokenType.String) RememberRoot(root.Value<string>());
        }
        SpecialDifficulties.Clear();
        if(token[nameof(SpecialDifficulties)] is JArray values) {
            foreach(JToken value in values) {
                string? name = value.Type == JTokenType.String ? value.Value<string>() : null;
                if(name != null && TufDifficultyFilter.SpecialNames.Contains(name)
                    && !SpecialDifficulties.Contains(name)) SpecialDifficulties.Add(name);
            }
        }
        TufDifficultyFilter normalized = GetDifficultyFilter();
        int quantumMin = Math.Clamp(QuantumMinIndex, 0, TufDifficultyFilter.QuantumNames.Count - 1);
        int quantumMax = Math.Clamp(QuantumMaxIndex, 0, TufDifficultyFilter.QuantumNames.Count - 1);
        if(quantumMin > quantumMax) (quantumMin, quantumMax) = (quantumMax, quantumMin);
        SetDifficultyFilter(normalized, quantumMin, quantumMax);
    }

    private static T Read<T>(JToken token, string key, T fallback) {
        try {
            if(token[key] is not JToken value) return fallback;
            object? parsed = value.ToObject(typeof(T));
            return parsed is T typed ? typed : fallback;
        } catch {
            return fallback;
        }
    }
}
