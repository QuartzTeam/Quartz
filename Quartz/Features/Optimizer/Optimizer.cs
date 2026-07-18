using System.Diagnostics;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
namespace Quartz.Features.Optimizer;
public static class Optimizer {
    public static SettingsFile<OptimizerSettings> ConfMgr { get; private set; }
    public static OptimizerSettings Conf => ConfMgr?.Data;
    public static readonly IRuntimeTick Ticker = new TickImpl();
    private const long GCReserveBytes = 64L * 1024 * 1024;
    private static bool defaultsCaptured;
    private static bool defaultRunInBackground;
    private static ProcessPriorityClass defaultPriority = ProcessPriorityClass.Normal;
    private static bool gcDeferred;
    private static bool usingNoGcRegion;
    private static bool loggedGcStrategy;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<OptimizerSettings>.Loaded("Optimizer.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Active {
        get {
            EnsureConf();
            return MainCore.IsModEnabled;
        }
    }
    public static void Initialize() {
        EnsureConf();
        CaptureDefaults();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        HitSoundRenderer.EnsureSceneHook();
        Apply();
    }
    private static void CaptureDefaults() {
        if(defaultsCaptured) return;
        defaultRunInBackground = Application.runInBackground;
        try {
            defaultPriority = Process.GetCurrentProcess().PriorityClass;
        } catch {
            defaultPriority = ProcessPriorityClass.Normal;
        }
        defaultsCaptured = true;
    }
    public static void Apply() {
        EnsureConf();
        CaptureDefaults();
        bool on = MainCore.IsModEnabled;
        Application.runInBackground = on && Conf.RunInBackground
            ? true
            : defaultRunInBackground;
        SetPriority(on && Conf.BoostProcessPriority
            ? ProcessPriorityClass.AboveNormal
            : defaultPriority);
        if(gcDeferred && !(on && Conf.SmoothGC && GameStats.InGame)) ResumeGC();
        TMPTextShadow.UnderlayOffsetScale = Conf.ShadowUnderlayOffsetScale;
        TMPTextShadow.UseMaterialUnderlay = on && Conf.LightTextShadows;
        if(!(on && Conf.RenderAllHitSounds)) HitSoundRenderer.StopAll("disabled");
    }
    public static void Restore() {
        if(gcDeferred) ResumeGC();
        Application.runInBackground = defaultRunInBackground;
        SetPriority(defaultPriority);
    }
    internal static bool LeakGuardActive {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf != null && Conf.LeakGuard;
        }
    }
    internal static bool FastBloomActive {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf != null && Conf.FastBloom;
        }
    }
    internal static bool SkipNoOpScreenFiltersActive {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf != null && Conf.SkipNoOpScreenFilters;
        }
    }
    private static void SetPriority(ProcessPriorityClass priority) {
        try {
            Process proc = Process.GetCurrentProcess();
            if(proc.PriorityClass != priority) proc.PriorityClass = priority;
        } catch {
        }
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if(!Active) return;
        if(Conf.LeakGuard) LeakGuardPatches.SweepStaticCaches();
        if(Conf.CollectOnLevelLoad) GC.Collect();
    }
    public static void Unhook() => SceneManager.sceneLoaded -= OnSceneLoaded;
    private static void Tick() {
        bool wantDefer = Active && Conf.SmoothGC && GameStats.InGame;
        if(wantDefer != gcDeferred) {
            if(wantDefer) DeferGC(); else ResumeGC();
        }
        HitSoundRenderer.Pump();
    }
    private static void DeferGC() {
        if(GarbageCollector.isIncremental) {
            usingNoGcRegion = false;
            gcDeferred = true;
            LogGcStrategyOnce("incremental GC present — leaving collection enabled.");
            return;
        }
        try {
            usingNoGcRegion = GC.TryStartNoGCRegion(GCReserveBytes);
            gcDeferred = true;
            LogGcStrategyOnce(usingNoGcRegion
                ? "no-GC region reserved (auto-recovers when the budget is spent)."
                : "no-GC region unavailable — leaving collection enabled.");
        } catch {
            usingNoGcRegion = false;
            gcDeferred = true;
            LogGcStrategyOnce("no-GC region unsupported — leaving collection enabled.");
        }
    }
    private static void ResumeGC() {
        if(usingNoGcRegion) {
            try { GC.EndNoGCRegion(); } catch { }
            try { GC.Collect(); } catch { }
        }
        usingNoGcRegion = false;
        gcDeferred = false;
    }
    private static void LogGcStrategyOnce(string detail) {
        if(loggedGcStrategy) return;
        loggedGcStrategy = true;
        MainCore.Log.Msg("[Optimizer] SmoothGC: " + detail);
    }
    private sealed class TickImpl : IRuntimeTick {
        public void Tick() => Optimizer.Tick();
    }
}
