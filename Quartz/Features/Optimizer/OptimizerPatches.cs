using ADOFAI;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
using Object = UnityEngine.Object;
using Quartz.Compat.Game;
namespace Quartz.Features.Optimizer;
public static class OptimizerPatches {
    [HarmonyPatch(typeof(TextureManager), "LoadTexture")]
    private static class LoadTextureCompressPatch {
        private static bool Prefix(string filePath, ref LoadResult status, int maxSideSize, ref Texture2D __result) {
            Optimizer.EnsureConf();
            if(!MainCore.IsModEnabled || Optimizer.Conf == null || !Optimizer.Conf.LossyTextureCompression) return true;
            if(GCS.internalLevelName != null || GameApi.IsBundleLevel()) return true;
            status = LoadResult.MissingFile;
            if(!RDFile.Exists(filePath)) {
                __result = null;
                return false;
            }
            byte[] data = RDFile.ReadAllBytes(filePath, out status);
            Texture2D tex = new(2, 2, TextureFormat.RGBA32, false);
            if(!tex.LoadImage(data)) {
                Object.Destroy(tex);
                __result = null;
                return false;
            }
            if(maxSideSize != -1) TextureManager.ShrinkImage(tex, maxSideSize);
            tex.name = filePath;
            if(tex.width % 4 == 0 && tex.height % 4 == 0) tex.Compress(false);
            if(tex.isReadable) tex.Apply(false, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            __result = tex;
            return false;
        }
    }
    [HarmonyPatch(typeof(VideoBloom), "OnRenderImage")]
    private static class FastBloomPatch {
        private static bool Prepare() => AccessTools.Method(typeof(VideoBloom), "OnRenderImage") != null;
        private static void Prefix(VideoBloom __instance, out bool __state) {
            __state = __instance.HighQuality;
            if(Optimizer.FastBloomActive) __instance.HighQuality = false;
        }
        private static void Postfix(VideoBloom __instance, bool __state) {
            __instance.HighQuality = __state;
        }
    }
    [HarmonyPatch(typeof(ScreenTile), "OnRenderImage")]
    private static class NoOpScreenTilePatch {
        private static bool Prepare() => AccessTools.Method(typeof(ScreenTile), "OnRenderImage") != null;
        private static bool Prefix(ScreenTile __instance, RenderTexture sourceTexture, RenderTexture destTexture) {
            if(!Optimizer.SkipNoOpScreenFiltersActive) return true;
            if(IsOne(__instance.tileX) && IsOne(__instance.tileY)) {
                Graphics.Blit(sourceTexture, destTexture);
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(ScreenScroll), "OnRenderImage")]
    private static class NoOpScreenScrollPatch {
        private static bool Prepare() => AccessTools.Method(typeof(ScreenScroll), "OnRenderImage") != null;
        private static bool Prefix(ScreenScroll __instance, RenderTexture sourceTexture, RenderTexture destTexture) {
            if(!Optimizer.SkipNoOpScreenFiltersActive) return true;
            if(IsZero(__instance.scrollOffset) && IsZero(__instance.scrollSpeed)) {
                Graphics.Blit(sourceTexture, destTexture);
                return false;
            }
            return true;
        }
    }
    private static bool IsOne(float value) => Mathf.Abs(value - 1f) <= 0.0001f;
    private static bool IsZero(Vector2 value) => value.sqrMagnitude <= 0.00000001f;
}
