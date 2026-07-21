using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Quartz.Compat.Game;
namespace Quartz.Features.PlanetColors;
public static partial class PlanetColors {
    private static IEnumerable<MethodBase> ExistingMethods(Type type, params string[] names) {
        for(int i = 0; i < names.Length; i++) {
            MethodInfo method = AccessTools.Method(type, names[i]);
            if(method != null) yield return method;
        }
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ClearCachesOnSceneChangePatch {
        private static void Postfix() => ClearSceneCaches();
    }
    [HarmonyPatch(typeof(scrPlanet), "Start")]
    private static class PlanetStartPatch {
        private static void Postfix(scrPlanet __instance) {
            InvalidatePlanetCache();
            if(ShouldChange) {
                ApplyPlanetColor(__instance);
                try { ApplyPlanetRing(__instance.planetRenderer); } catch { }
            }
        }
    }
    [HarmonyPatch(typeof(PlanetRenderer), "Awake")]
    private static class PlanetRendererAwakePatch {
        private static void Postfix(PlanetRenderer __instance) {
            InvalidatePlanetCache();
            if(ShouldChange) ApplyPlanetRendererColor(__instance);
        }
    }
    [HarmonyPatch(typeof(PlanetRenderer), "Revive")]
    private static class PlanetRendererRevivePatch {
        private static void Postfix(PlanetRenderer __instance) {
            if(ShouldChange) ApplyPlanetRendererColor(__instance);
        }
    }
    [HarmonyPatch(typeof(PlanetRenderer), "PlayParticles")]
    private static class PlanetRendererPlayParticlesPatch {
        private static void Postfix(PlanetRenderer __instance) {
            if(!ShouldChange) return;
            ApplyTailParticleColor(__instance, TailColor(GetPlanetSlot(__instance)));
        }
    }
    [HarmonyPatch(typeof(PlanetRenderer), "LateUpdate")]
    private static class PlanetRendererLateUpdatePatch {
        private static void Postfix(PlanetRenderer __instance) {
            if(ShouldChange) ApplyPlanetRing(__instance);
        }
    }
    [HarmonyPatch]
    private static class PlanetRendererColorBlockPatch {
        private static IEnumerable<MethodBase> TargetMethods()
            => ExistingMethods(typeof(PlanetRenderer), "SetRainbow", "LoadPlanetColor", "SetColor");
        private static bool Prefix(PlanetRenderer __instance) {
            if(applying || !ShouldChange) return true;
            ApplyPlanetRendererColor(__instance);
            ApplyLogoColor(scrLogoText.instance);
            return false;
        }
    }
    [HarmonyPatch]
    private static class PlanetRendererForceColorPatch {
        private static IEnumerable<MethodBase> TargetMethods()
            => ExistingMethods(typeof(PlanetRenderer), "SetPlanetColor", "SetCoreColor", "SetTailColor", "SetFaceColor");
        private static void Prefix(PlanetRenderer __instance, MethodBase __originalMethod, ref Color __0) {
            if(applying || !ShouldChange) return;
            int slot = GetPlanetSlot(__instance);
            __0 = __originalMethod != null && __originalMethod.Name == "SetTailColor"
                ? TailColor(slot)
                : BallColor(slot);
        }
        private static void Postfix(PlanetRenderer __instance, MethodBase __originalMethod) {
            if(applying || !ShouldChange || __originalMethod == null || __originalMethod.Name != "SetTailColor") return;
            ApplyTailParticleColor(__instance, TailColor(GetPlanetSlot(__instance)));
        }
    }
    [HarmonyPatch(typeof(scrLogoText), "Awake")]
    private static class LogoAwakePatch {
        private static void Postfix(scrLogoText __instance) {
            if(ShouldChange) ApplyLogoColor(__instance);
        }
    }
    [HarmonyPatch(typeof(scrLogoText), "UpdateColors")]
    private static class LogoUpdateColorsPatch {
        private static bool Prefix(scrLogoText __instance) {
            if(!ShouldChange) return true;
            ApplyLogoColor(__instance);
            return false;
        }
    }
    [HarmonyPatch(typeof(scrLogoText), "LateUpdate")]
    private static class LogoLateUpdatePatch {
        private static bool Prefix() => !ShouldChange;
    }
    [HarmonyPatch]
    private static class LevelSelectRainbowPatch {
        private static MethodBase TargetMethod() => GameApi.PlanetPaletteMethod("RainbowMode");
        private static bool Prefix() => !ShouldChange;
    }
    [HarmonyPatch]
    private static class LevelSelectEnbyPatch {
        private static MethodBase TargetMethod() => GameApi.PlanetPaletteMethod("EnbyMode");
        private static bool Prefix() => !ShouldChange;
    }
}
