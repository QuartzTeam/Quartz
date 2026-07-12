using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer;
internal static class KeyViewerPersistence {
    internal const float CountSaveIdleSeconds = 2f;
    private static readonly string[] DmPresetFields = [
        "selectedKeyType",
        "keys",
        "keyPositions",
        "positions",
        "statPositions",
        "graphPositions",
    ];
    internal static float CountSaveDeadline(float now) => now + CountSaveIdleSeconds;
    internal static bool ShouldFlushCounts(bool dirty, bool inGame, float now, float deadline) =>
        dirty && !inGame && now >= deadline;
    internal static bool ShouldPersistBoxCount(bool isStat, bool isFoot) => !isStat && !isFoot;
    internal static string SanitizeDmPreset(string json) {
        if(string.IsNullOrWhiteSpace(json)) return "";
        JObject source = JObject.Parse(json);
        if(source["keys"] is not JObject)
            throw new FormatException("Preset must contain a keys object.");
        if(source["keyPositions"] is not JObject && source["positions"] is not JObject)
            throw new FormatException("Preset must contain a keyPositions or positions object.");
        JObject result = [];
        foreach(string field in DmPresetFields) {
            if(source[field] is JToken value) result[field] = value.DeepClone();
        }
        return result.ToString(Formatting.None);
    }
}
