using HarmonyLib;
using MonsterLove.StateMachine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Features.Calibration;
internal static class CalibrationTimingLogger {
    private static readonly List<float> all = [];
    private static readonly Dictionary<string, List<float>> maps = [];
    private static bool loaded;
    private static bool loggedThisRun;
    private static bool dirty;
    private static bool wasInGame;
    private static bool gameStateKnown;
    internal static readonly IRuntimeTick Ticker = new TickImpl();
    private sealed class TickImpl : IRuntimeTick {
        public void Tick() {
            if(!Calibration.Enabled) return;
            bool inGame = Status.GameStats.InGame;
            // Timings are a rolling in-memory cache; write them once gameplay ends rather than on
            // the death frame, so the fsync never lands on a rendered frame.
            if(gameStateKnown && wasInGame && !inGame) FlushIfDirty();
            wasInGame = inGame;
            gameStateKnown = true;
        }
    }
    internal static void FlushIfDirty() {
        if(!dirty) return;
        dirty = false;
        Save();
    }
    internal static IReadOnlyList<float> RecentAll() {
        EnsureLoaded();
        return all;
    }
    internal static IReadOnlyList<float> RecentForCurrentMap() {
        EnsureLoaded();
        string key = Features.PlayCount.PlayCount.ComputeMapKey();
        return maps.TryGetValue(key, out List<float> list) ? list : Array.Empty<float>();
    }
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    private static class ChangeStatePatch {
        private static void Postfix(Enum newState) {
            if(!Calibration.Enabled) return;
            if(newState is not States state) return;
            if(state == States.Start) loggedThisRun = false;
            else if(state != States.Fail2) LogTiming();
        }
    }
    [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
    private static class TogglePauseGamePatch {
        private static void Postfix() {
            if(Calibration.Enabled) LogTiming();
        }
    }
    private static void LogTiming() {
        EnsureLoaded();
        if(loggedThisRun || !CalibrationTiming.HasSamples) return;
        float offset = Calibration.GetOffsetMs() + CalibrationTiming.Average();
        string mapKey = Features.PlayCount.PlayCount.ComputeMapKey();
        AddCapped(all, offset, Calibration.Conf.MaxTimings);
        if(!maps.TryGetValue(mapKey, out List<float> list)) maps[mapKey] = list = [];
        AddCapped(list, offset, Calibration.Conf.MaxTimingsPerMap);
        loggedThisRun = true;
        dirty = true;
    }
    private static void AddCapped(List<float> list, float value, int cap) {
        list.Add(value);
        while(list.Count > Math.Max(1, cap)) list.RemoveAt(0);
    }
    private static string FilePath => Path.Combine(MainCore.Paths.RootPath, "CalibrationTimings.json");
    private static void EnsureLoaded() {
        if(loaded) return;
        loaded = true;
        try {
            string path = FilePath;
            if(!File.Exists(path)) return;
            JObject root = JObject.Parse(File.ReadAllText(path));
            if(root["all"] is JArray allArr)
                foreach(JToken v in allArr) all.Add((float)v);
            if(root["maps"] is JObject mapsObj) {
                foreach(JProperty prop in mapsObj.Properties()) {
                    if(prop.Value is not JArray arr) continue;
                    List<float> list = [];
                    foreach(JToken v in arr) list.Add((float)v);
                    maps[prop.Name] = list;
                }
            }
        } catch(Exception e) {
            MainCore.Log.Wrn("CalibrationTimingLogger load failed: " + e.Message);
        }
    }
    private static void Save() {
        try {
            JObject mapsObj = new();
            foreach(KeyValuePair<string, List<float>> kv in maps) mapsObj[kv.Key] = new JArray(kv.Value);
            JObject root = new() {
                ["all"] = new JArray(all),
                ["maps"] = mapsObj,
            };
            AtomicFile.WriteAllText(FilePath, root.ToString(Formatting.Indented));
        } catch(Exception e) {
            MainCore.Log.Err("CalibrationTimingLogger save failed: " + e.Message);
        }
    }
}
