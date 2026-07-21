using System;
using System.Collections.Generic;
using HarmonyLib;
using MonsterLove.StateMachine;
using System.Reflection;
using Quartz.Compat.Game;
namespace Quartz.Features.Calibration;
internal static class CalibrationTiming {
    private static readonly List<float> timings = [];
    private static float lastTooEarly = float.NaN;
    private static float lastTooLate = float.NaN;
    internal static bool HasSamples => timings.Count > 0;
    internal static float Average() {
        if(timings.Count == 0) return 0f;
        float sum = 0f;
        foreach(float t in timings) sum += t;
        return sum / timings.Count;
    }
    private static void ResetLastTooJudge() {
        lastTooEarly = float.NaN;
        lastTooLate = float.NaN;
    }
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    private static class ChangeStatePatch {
        private static void Postfix(Enum newState) {
            if(!Calibration.Enabled) return;
            if(newState is not States state) return;
            if(state != States.Fail2) ResetLastTooJudge();
            if(state == States.Start) timings.Clear();
        }
    }
    [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
    private static class TogglePauseGamePatch {
        private static void Postfix() {
            if(Calibration.Enabled) ResetLastTooJudge();
        }
    }
    [HarmonyPatch(typeof(scrMisc), "GetHitMargin",
        new[] { typeof(float), typeof(float), typeof(bool), typeof(float), typeof(float), typeof(double) })]
    private static class GetHitMarginPatch {
        private static void Postfix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, HitMargin __result) {
            if(!Calibration.Enabled || RDC.auto || __result == HitMargin.Auto) return;
            float angle = (hitangle - refangle) * (isCW ? 1f : -1f) * 57.29578f;
            float timing = angle / 180f / bpmTimesSpeed / conductorPitch * 60000f;
            switch(__result) {
                case HitMargin.TooEarly:
                    lastTooEarly = timing;
                    break;
                case HitMargin.TooLate:
                    lastTooLate = timing;
                    break;
                default:
                    timings.Add(timing);
                    ResetLastTooJudge();
                    break;
            }
        }
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(HitMargin hit) {
            if(!Calibration.Enabled || hit != HitMargin.FailMiss) return;
            if(float.IsNaN(lastTooEarly) || float.IsNaN(lastTooLate)) return;
            timings.Add(lastTooLate);
            timings.Add(lastTooEarly);
            ResetLastTooJudge();
        }
    }
}
