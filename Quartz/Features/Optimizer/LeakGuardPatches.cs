using System.Collections;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Quartz.Features.Optimizer;
public static class LeakGuardPatches {
    private static readonly HashSet<Sprite> OwnedThumbnails = [];
    [HarmonyPatch(typeof(scrVisualDecoration), "OnDestroy")]
    private static class VisualDecoMaterialPatch {
        private static readonly AccessTools.FieldRef<scrVisualDecoration, Material> MeshRendererMat =
            AccessTools.FieldRefAccess<scrVisualDecoration, Material>("meshRendererMat");
        private static bool Prepare() =>
            AccessTools.Method(typeof(scrVisualDecoration), "OnDestroy") != null
            && AccessTools.Field(typeof(scrVisualDecoration), "meshRendererMat") != null;
        private static void Postfix(scrVisualDecoration __instance) {
            if(!Optimizer.LeakGuardActive) return;
            try {
                Material mat = MeshRendererMat(__instance);
                if(mat != null) {
                    if(mat.mainTexture is RenderTexture rt) {
                        rt.Release();
                        Object.Destroy(rt);
                    }
                    Object.Destroy(mat);
                    MeshRendererMat(__instance) = null;
                }
                if(__instance.spareRT1 != null) {
                    Object.Destroy(__instance.spareRT1);
                    __instance.spareRT1 = null;
                }
                if(__instance.spareRT2 != null) {
                    Object.Destroy(__instance.spareRT2);
                    __instance.spareRT2 = null;
                }
            } catch { }
        }
    }
    [HarmonyPatch(typeof(scrCamera), nameof(scrCamera.SetCustomFrameRate))]
    private static class FrameRateRTPatch {
        private static readonly AccessTools.FieldRef<scrCamera, MeshRenderer> CamQuadMesh =
            AccessTools.FieldRefAccess<scrCamera, MeshRenderer>("camQuadMesh");
        private static readonly AccessTools.FieldRef<scrCamera, RenderTexture> CamRT =
            AccessTools.FieldRefAccess<scrCamera, RenderTexture>("camRT");
        private static bool Prepare() =>
            AccessTools.Method(typeof(scrCamera), "SetCustomFrameRate") != null
            && AccessTools.Field(typeof(scrCamera), "camQuadMesh") != null
            && AccessTools.Field(typeof(scrCamera), "camRT") != null;
        private static void Prefix(scrCamera __instance, out RenderTexture __state) {
            __state = null;
            if(!Optimizer.LeakGuardActive) return;
            try {
                MeshRenderer quad = CamQuadMesh(__instance);
                if(quad != null && quad.sharedMaterial != null)
                    __state = quad.sharedMaterial.mainTexture as RenderTexture;
            } catch { }
        }
        private static void Postfix(scrCamera __instance, RenderTexture __state) {
            if(__state == null) return;
            try {
                MeshRenderer quad = CamQuadMesh(__instance);
                Texture current = quad != null && quad.sharedMaterial != null
                    ? quad.sharedMaterial.mainTexture
                    : null;
                if(ReferenceEquals(__state, current) || ReferenceEquals(__state, CamRT(__instance))) return;
                __state.Release();
                Object.Destroy(__state);
            } catch { }
        }
    }
    [HarmonyPatch(typeof(WorkshopLevelList), "SelectLevel")]
    private static class WorkshopThumbnailPatch {
        private static bool Prepare() =>
            AccessTools.Method(typeof(WorkshopLevelList), "SelectLevel") != null;
        private static void Prefix(WorkshopLevelList __instance, out Sprite __state) =>
            __state = __instance.thumbnail != null ? __instance.thumbnail.sprite : null;
        private static void Postfix(WorkshopLevelList __instance, Sprite __state) {
            if(!Optimizer.LeakGuardActive) return;
            try {
                Sprite current = __instance.thumbnail != null ? __instance.thumbnail.sprite : null;
                if(current != null && !ReferenceEquals(current, __state)) OwnedThumbnails.Add(current);
                if(__state == null || ReferenceEquals(__state, current) || !OwnedThumbnails.Remove(__state)) return;
                Texture2D tex = __state.texture;
                Object.Destroy(__state);
                if(tex != null) Object.Destroy(tex);
            } catch { }
        }
    }
    [HarmonyPatch(typeof(PracticeTimeline), nameof(PracticeTimeline.Init))]
    private static class PracticeWaveformPatch {
        private static bool Prepare() =>
            AccessTools.Method(typeof(PracticeTimeline), "Init") != null;
        private static void Prefix(PracticeTimeline __instance, out Texture __state) =>
            __state = __instance.waveRaw != null ? __instance.waveRaw.texture : null;
        private static void Postfix(PracticeTimeline __instance, Texture __state) {
            if(!Optimizer.LeakGuardActive) return;
            try {
                Texture current = __instance.waveRaw != null ? __instance.waveRaw.texture : null;
                if(ReferenceEquals(__state, current)) return;
                if(__state is Texture2D old && old.name == "Waveform") Object.Destroy(old);
            } catch { }
        }
    }
    // Static caches the game never evicts; swept from Optimizer's sceneLoaded hook.
    internal static void SweepStaticCaches() {
        try {
            FloorMesh.cache.Clear();
        } catch { }
        PruneFilterDictionaries();
        OwnedThumbnails.Clear();
    }
    private static readonly string[] FilterDictFields = [
        "addedFilters", "usedFilters", "modifiedFilters",
        "filterOriginalValues", "filterFieldTweens", "initializedFilters",
    ];
    private static readonly List<object> DeadKeys = [];
    private static void PruneFilterDictionaries() {
        try {
            foreach(string fieldName in FilterDictFields) {
                var field = AccessTools.Field(typeof(ffxSetFilterAdvancedPlus), fieldName);
                if(field?.GetValue(null) is not IDictionary dict) continue;
                DeadKeys.Clear();
                foreach(object key in dict.Keys)
                    if(key is Object unityKey && unityKey == null)
                        DeadKeys.Add(key);
                foreach(object key in DeadKeys) dict.Remove(key);
            }
        } catch { }
        DeadKeys.Clear();
    }
}
