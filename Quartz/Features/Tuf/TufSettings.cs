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
        [nameof(SpecialDifficulties)] = new JArray(SpecialDifficulties)
    };

    public void Deserialize(JToken token) {
        Sort = Read(token, nameof(Sort), Sort);
        if(!Enum.IsDefined(typeof(TufSort), Sort)) Sort = (int)TufSort.Recent;
        Ascending = Read(token, nameof(Ascending), Ascending);
        MinDifficultyIndex = Read(token, nameof(MinDifficultyIndex), MinDifficultyIndex);
        MaxDifficultyIndex = Read(token, nameof(MaxDifficultyIndex), MaxDifficultyIndex);
        QuantumEnabled = Read(token, nameof(QuantumEnabled), QuantumEnabled);
        QuantumMinIndex = Read(token, nameof(QuantumMinIndex), QuantumMinIndex);
        QuantumMaxIndex = Read(token, nameof(QuantumMaxIndex), QuantumMaxIndex);
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
