using System.Reflection;
using HarmonyLib;
using Quartz.Compat.Game;
using Quartz.Core;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Quartz.Features.Calibration;
internal static class CalibrationDetail {
    private static Text text;
    private static float? max;
    private static float? min;
    [HarmonyPatch]
    private static class StartPatch {
        private static MethodBase TargetMethod() => GameApi.CalibrationMethod("Start");
        private static void Postfix(Text ___txtResults) {
            text = ___txtResults;
            Calibration.InCalibrationScreen = true;
            SceneManager.sceneUnloaded += OnCalibrationSceneUnloaded;
        }
    }
    private static void OnCalibrationSceneUnloaded(Scene _) {
        Calibration.InCalibrationScreen = false;
        SceneManager.sceneUnloaded -= OnCalibrationSceneUnloaded;
    }
    [HarmonyPatch]
    private static class GetOffsetPatch {
        private static MethodBase TargetMethod() => GameApi.CalibrationMethod("GetOffset");
        private static void Postfix(double __result) {
            if(!text) return;
            float timing = (float)(__result * 1000);
            if(max == null || timing > max) max = timing;
            if(min == null || timing < min) min = timing;
        }
    }
    [HarmonyPatch]
    private static class PutDataPointPatch {
        private static MethodBase TargetMethod() => GameApi.CalibrationMethod("PutDataPoint");
        private static void Postfix(object __instance) {
            if(GameApi.CalibrationDone(__instance) || !text || !Calibration.ShouldShowDetail) return;
            double mean = GameApi.AverageOffset(GameApi.CalibrationOffsets(__instance), out int count);
            float avg = count == 0 ? 0f : (float)mean * 1000f;
            text.text = string.Format(
                MainCore.Tr.Get("CALIBRATION_DETAIL_STATS", "Avg {0}ms / Max {1}ms / Min {2}ms"),
                Calibration.FormatMs(avg), Calibration.FormatMs(max ?? 0f), Calibration.FormatMs(min ?? 0f)
            );
        }
    }
    [HarmonyPatch]
    private static class SetMessageNumberPatch {
        private static MethodBase TargetMethod() => GameApi.CalibrationMethod("SetMessageNumber");
        private static void Postfix(int n, Text ___txtResults) {
            if(!___txtResults) return;
            ___txtResults.fontSize = n == 1 ? 30 : 40;
            max = null;
            min = null;
        }
    }
}
