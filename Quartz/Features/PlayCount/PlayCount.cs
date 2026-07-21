using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using MonsterLove.StateMachine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Reflection;
using Quartz.Compat.Game;
namespace Quartz.Features.PlayCount;
public sealed class PlayCount : IRuntimeService, IRuntimeTick {
    private static readonly Dictionary<string, PlayData> playDatas = new();
    private static string currentMapKey = "";
    private static int sessionAttempts;
    private static float bestObservedThisRun;
    private static bool runHadFail;
    private static bool dirty;
    private static bool wasInGame;
    private static bool gameStateKnown;
    public void Initialize() => Load();
    public void Dispose() => FlushIfDirty();
    public void Tick() {
        if(!MainCore.IsModEnabled) return;
        bool inGame = Status.GameStats.InGame;
        if(gameStateKnown && wasInGame && !inGame) FlushIfDirty();
        wasInGame = inGame;
        gameStateKnown = true;
        if(!inGame) return;
        ObserveProgress(Status.GameStats.Progress);
    }
    public static PlayData For(string key) {
        if(!playDatas.TryGetValue(key, out PlayData d)) {
            d = new PlayData();
            playDatas[key] = d;
        }
        return d;
    }
    public static int SessionAttempts => sessionAttempts;
    public static int TotalAttemptsForCurrentMap() {
        if(string.IsNullOrEmpty(currentMapKey)) return 0;
        return playDatas.TryGetValue(currentMapKey, out PlayData d) ? d.TotalAttempts : 0;
    }
    public static float BestForCurrentMap() {
        if(string.IsNullOrEmpty(currentMapKey)) return 0f;
        playDatas.TryGetValue(currentMapKey, out PlayData d);
        float storedStart = d?.BestStartProgress ?? 0f;
        float storedEnd = d?.BestProgress ?? 0f;
        return LiveRunWins(storedStart, storedEnd) ? bestObservedThisRun : storedEnd;
    }
    public static float BestStartForCurrentMap() {
        if(string.IsNullOrEmpty(currentMapKey)) return 0f;
        playDatas.TryGetValue(currentMapKey, out PlayData d);
        float storedStart = d?.BestStartProgress ?? 0f;
        float storedEnd = d?.BestProgress ?? 0f;
        return LiveRunWins(storedStart, storedEnd) ? CurrentRunStart() : storedStart;
    }
    private static float SpanOf(float start, float end) => Mathf.Max(0f, end - start);
    private static bool LiveRunWins(float storedStart, float storedEnd) =>
        SpanOf(CurrentRunStart(), bestObservedThisRun) > SpanOf(storedStart, storedEnd);
    private static float CurrentRunStart() {
        try {
            return Status.ProgressTracker.RunStartedFromFirstTile
                ? 0f
                : Mathf.Clamp01(Status.ProgressTracker.RunStartProgress);
        } catch {
            return 0f;
        }
    }
    public static void ObserveProgress(float progress) {
        if(runHadFail) return;
        if(float.IsNaN(progress) || float.IsInfinity(progress)) return;
        progress = Mathf.Clamp01(progress);
        if(progress > bestObservedThisRun) bestObservedThisRun = progress;
    }
    private static void OnRunStart() {
        string key = ComputeMapKey();
        if(key != currentMapKey) {
            currentMapKey = key;
            sessionAttempts = 0;
        }
        sessionAttempts++;
        bestObservedThisRun = 0f;
        runHadFail = false;
        PlayData d = For(key);
        d.TotalAttempts++;
        dirty = true;
    }
    private static void OnRunDeath() {
        if(string.IsNullOrEmpty(currentMapKey)) return;
        if(!runHadFail) {
            float progress = CurrentProgress();
            if(progress > bestObservedThisRun) bestObservedThisRun = progress;
        }
        PlayData d = For(currentMapKey);
        float runStart = CurrentRunStart();
        if(SpanOf(runStart, bestObservedThisRun) > SpanOf(d.BestStartProgress, d.BestProgress)) {
            d.BestProgress = bestObservedThisRun;
            d.BestStartProgress = runStart;
            dirty = true;
        }
    }
    private static void OnRunClear() {
        if(string.IsNullOrEmpty(currentMapKey)) return;
        PlayData d = For(currentMapKey);
        float runStart = CurrentRunStart();
        if(!runHadFail) {
            if(SpanOf(runStart, 1f) > SpanOf(d.BestStartProgress, d.BestProgress)) {
                d.BestProgress = 1f;
                d.BestStartProgress = runStart;
                dirty = true;
            }
            bestObservedThisRun = 1f;
        } else {
            if(SpanOf(runStart, bestObservedThisRun) > SpanOf(d.BestStartProgress, d.BestProgress)) {
                d.BestProgress = bestObservedThisRun;
                d.BestStartProgress = runStart;
                dirty = true;
            }
        }
    }
    private static float CurrentProgress() {
        try {
            scrController c = scrController.instance;
            return c != null ? Mathf.Clamp01(c.percentComplete) : 0f;
        } catch {
            return 0f;
        }
    }
    internal static string ComputeMapKey() {
        bool isOfficial;
        try {
            isOfficial = ADOBase.isOfficialLevel;
        } catch {
            isOfficial = false;
        }
        if(isOfficial) {
            try {
                string official = ADOBase.currentLevel;
                if(!string.IsNullOrEmpty(official)) return "official:" + official;
            } catch {
            }
        }
        string fileMapKey = TryGetLevelFileMapKey();
        if(!string.IsNullOrEmpty(fileMapKey)) return fileMapKey;
        try {
            scrLevelMaker lm = scrLevelMaker.instance;
            if(lm != null) {
                if(lm.isOldLevel && !string.IsNullOrEmpty(lm.leveldata)) return "old:" + Sha256(lm.leveldata);
                if(lm.floorAngles != null) {
                    float[] arr = System.Linq.Enumerable.ToArray(lm.floorAngles);
                    StringBuilder sb = new();
                    sb.Append("angles:").Append(arr.Length);
                    for(int i = 0; i < arr.Length; i++) {
                        sb.Append(':').Append(arr[i].ToString("0.###", CultureInfo.InvariantCulture));
                    }
                    return "new:" + Sha256(sb.ToString());
                }
            }
        } catch {
        }
        return "unknown";
    }
    private static readonly object hashGate = new();
    private static readonly Dictionary<string, string> fileHashCache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> hashesInFlight = new(StringComparer.Ordinal);
    private static string TryGetLevelFileMapKey() {
        try {
            string path = ADOBase.levelPath;
            if(string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            FileInfo info = new(path);
            string cacheKey = path + "|"
                + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "|"
                + info.Length.ToString(CultureInfo.InvariantCulture);
            lock(hashGate) {
                if(fileHashCache.TryGetValue(cacheKey, out string cachedHash)) return "custom:" + cachedHash;
            }
            string pendingKey = "pending:" + Sha256(cacheKey);
            bool start;
            lock(hashGate) {
                start = hashesInFlight.Add(cacheKey);
            }
            if(start) _ = Task.Run(() => HashLevelFile(path, cacheKey, pendingKey));
            return pendingKey;
        } catch {
            return null;
        }
    }
    private static void HashLevelFile(string path, string cacheKey, string pendingKey) {
        string hash = null;
        Exception error = null;
        try {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using SHA256 sha = SHA256.Create();
            hash = Hex(sha.ComputeHash(stream));
        } catch(Exception e) {
            error = e;
        }
        MainThread.Enqueue(() => {
            lock(hashGate) {
                hashesInFlight.Remove(cacheKey);
                if(hash != null) fileHashCache[cacheKey] = hash;
            }
            if(hash == null) {
                MainCore.Log.Wrn("PlayCount map hash failed: " + error?.Message);
                return;
            }
            MigratePendingMapKey(pendingKey, "custom:" + hash);
        });
    }
    private static void MigratePendingMapKey(string pendingKey, string finalKey) {
        if(pendingKey == finalKey) return;
        if(playDatas.TryGetValue(pendingKey, out PlayData pending)) {
            PlayData final = For(finalKey);
            final.TotalAttempts += pending.TotalAttempts;
            if(pending.BestProgress > final.BestProgress) {
                final.BestProgress = pending.BestProgress;
                final.BestStartProgress = pending.BestStartProgress;
            }
            playDatas.Remove(pendingKey);
            dirty = true;
        }
        if(currentMapKey == pendingKey) currentMapKey = finalKey;
    }
    private static string Sha256(string s) => Sha256(Encoding.UTF8.GetBytes(s));
    private static string Sha256(byte[] bytes) {
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(bytes));
    }
    private static string Hex(byte[] hash) {
        StringBuilder sb = new(hash.Length * 2);
        for(int i = 0; i < hash.Length; i++) {
            sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
    private static string FilePath => Path.Combine(MainCore.Paths.RootPath, "PlayCount.json");
    private static void Load() {
        playDatas.Clear();
        currentMapKey = "";
        sessionAttempts = 0;
        bestObservedThisRun = 0f;
        runHadFail = false;
        dirty = false;
        try {
            string path = FilePath;
            if(!File.Exists(path)) return;
            string raw = File.ReadAllText(path);
            JObject root = JObject.Parse(raw);
            JObject maps = root["maps"] as JObject;
            if(maps == null) return;
            foreach(KeyValuePair<string, JToken> kv in maps) playDatas[kv.Key] = PlayData.Deserialize(kv.Value);
        } catch(Exception e) {
            MainCore.Log.Wrn("PlayCount load failed: " + e.Message);
        }
    }
    private static void FlushIfDirty() {
        if(dirty) Save();
    }
    public static void Save() {
        try {
            JObject maps = new();
            foreach(KeyValuePair<string, PlayData> kv in playDatas) maps[kv.Key] = kv.Value.Serialize();
            JObject root = new() {
                ["maps"] = maps,
            };
            AtomicFile.WriteAllText(FilePath, root.ToString(Formatting.Indented));
            dirty = false;
        } catch(Exception e) {
            MainCore.Log.Err("PlayCount save failed: " + e.Message);
        }
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class ScnGamePlayPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            OnRunStart();
        }
    }
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    private static class StateChangePatch {
        private static void Postfix(Enum newState) {
            if(!MainCore.IsModEnabled) return;
            if(newState is not States state) return;
            if(state == States.Fail2) OnRunDeath();
            else if(state == States.Won) OnRunClear();
        }
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(HitMargin hit) {
            if(!MainCore.IsModEnabled || runHadFail) return;
            if(hit == HitMargin.FailMiss || hit == HitMargin.FailOverload) {
                ObserveProgress(CurrentProgress());
                runHadFail = true;
            }
        }
    }
}
