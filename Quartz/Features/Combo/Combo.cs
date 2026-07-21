using HarmonyLib;
using Quartz.Core;
using Quartz.Features.Interop;
using UnityEngine;
using System.Reflection;
using Quartz.Compat.Game;
namespace Quartz.Features.Combo;
internal static class Combo {
    internal static int Count;
    internal static float PulseStartTime = -1f;
    private const float OutFraction = 0.3f;
    internal static (float scale, float intensity) EvaluatePulse(float peakDelta, float duration) {
        ComboSettings conf = ComboOverlay.Conf;
        if(conf != null && conf.NoPopAnim) {
            PulseStartTime = -1f;
            return (1f, 0f);
        }
        if(PulseStartTime < 0f || duration <= 0f) return (1f, 0f);
        float peak = 1f + Mathf.Max(0f, peakDelta);
        float outDur = duration * OutFraction;
        float settleDur = duration - outDur;
        float elapsed = Time.realtimeSinceStartup - PulseStartTime;
        if(elapsed <= outDur) {
            float t = outDur <= 0f ? 1f : elapsed / outDur;
            float eased = t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
            return (Mathf.LerpUnclamped(1f, peak, eased), eased);
        }
        float settleElapsed = elapsed - outDur;
        if(settleDur <= 0f || settleElapsed >= settleDur) {
            PulseStartTime = -1f;
            return (1f, 0f);
        }
        float k = settleElapsed / settleDur;
        return (Mathf.Lerp(peak, 1f, k), 1f - k);
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class ResetOnRunStartPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            Count = 0;
            PulseStartTime = -1f;
        }
    }
    [HarmonyPatch(typeof(scrController), "Start")]
    private static class ResetOnControllerStartPatch {
        private static void Postfix(scrController __instance) {
            if(!MainCore.IsModEnabled) return;
            if(__instance.gameworld) {
                Count = 0;
                PulseStartTime = -1f;
            }
        }
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ResetOnRunExitPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            Count = 0;
            PulseStartTime = -1f;
        }
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(HitMargin hit) {
            if(!MainCore.IsModEnabled) return;
            ComboSettings conf = ComboOverlay.Conf;
            if(conf == null) return;
            bool xpComboMode = conf.XPerfectComboEnabled && XPerfectBridge.Active;
            bool incPerfect = xpComboMode && hit == HitMargin.Perfect
                ? XPerfectBridge.LastJudge() == XPerfectBridge.Judge.X
                : hit == HitMargin.Perfect;
            bool incAuto = conf.CountAuto && hit == HitMargin.Auto;
            if(incPerfect || incAuto) {
                Count++;
                PulseStartTime = conf.NoPopAnim ? -1f : Time.realtimeSinceStartup;
            } else if(conf.CountAuto || hit != HitMargin.Auto) {
                Count = 0;
                PulseStartTime = -1f;
            }
        }
    }
}
