using HarmonyLib;
using Quartz.Core;
using UnityEngine.SceneManagement;
using System.Reflection;
using Quartz.Compat.Game;
namespace Quartz.Addons;
public static class AddonEvents {
    public static event Action LevelStart;
    public static event Action LevelEnd;
    public static event Action<HitMargin> Hit;
    public static event Action<Scene> SceneLoaded;
    public static event Action<bool> ModEnabledChanged;
    internal static void RaiseSceneLoaded(Scene scene) => SafeRaise(SceneLoaded, scene);
    internal static void RaiseModEnabledChanged(bool enabled) => SafeRaise(ModEnabledChanged, enabled);
    internal static void Clear() {
        LevelStart = null;
        LevelEnd = null;
        Hit = null;
        SceneLoaded = null;
        ModEnabledChanged = null;
    }
    private static void SafeRaise(Action evt) {
        if(evt == null) return;
        foreach(Action handler in evt.GetInvocationList()) {
            try {
                handler();
            } catch(Exception e) {
                MainCore.Log.Err($"[Addons] event handler threw: {e}");
            }
        }
    }
    private static void SafeRaise<T>(Action<T> evt, T arg) {
        if(evt == null) return;
        foreach(Action<T> handler in evt.GetInvocationList()) {
            try {
                handler(arg);
            } catch(Exception e) {
                MainCore.Log.Err($"[Addons] event handler threw: {e}");
            }
        }
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class RunStartPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            SafeRaise(LevelStart);
        }
    }
    [HarmonyPatch(typeof(scrController), "Start")]
    private static class ControllerStartPatch {
        private static void Postfix(scrController __instance) {
            if(!MainCore.IsModEnabled) return;
            if(__instance.gameworld) SafeRaise(LevelStart);
        }
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class RunExitPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            SafeRaise(LevelEnd);
        }
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(HitMargin hit) {
            if(!MainCore.IsModEnabled) return;
            SafeRaise(Hit, hit);
        }
    }
}
