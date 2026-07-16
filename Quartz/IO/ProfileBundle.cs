#nullable disable
using System.Text;
using Newtonsoft.Json.Linq;
namespace Quartz.IO;
// The rules for turning an export bundle's Files object into the files that land in a
// profile directory. Free of Unity and MainCore so Quartz.Tests can cover the preset
// contract: the caller names the config file and the fields a preset may not impose.
public static class ProfileBundle {
    public static Dictionary<string, byte[]> ReadFiles(
        JObject files, ISet<string> excluded,
        bool asPreset, string configFileName, string[] presetImposed
    ) {
        Dictionary<string, byte[]> imported = new(StringComparer.OrdinalIgnoreCase);
        foreach(JProperty prop in files.Properties()) {
            string fileName = Path.GetFileName(prop.Name);
            if(!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || excluded.Contains(fileName)) continue;
            JToken contents = asPreset
                ? StripPresetImposed(fileName, prop.Value, configFileName, presetImposed)
                : prop.Value;
            imported[fileName] = Encoding.UTF8.GetBytes(contents.ToString());
        }
        return imported;
    }
    // Dropping the key instead of rewriting it is what keeps the applier's own value:
    // an absent key makes CoreSettings.Deserialize fall back to the live field, because
    // SettingsFile<T>.Data is one reused instance rather than a fresh default.
    public static JToken StripPresetImposed(string fileName, JToken contents, string configFileName, string[] presetImposed) {
        if(!fileName.Equals(configFileName, StringComparison.OrdinalIgnoreCase)
            || contents is not JObject settings) return contents;
        JObject stripped = (JObject)settings.DeepClone();
        foreach(string field in presetImposed) stripped.Remove(field);
        return stripped;
    }
}
