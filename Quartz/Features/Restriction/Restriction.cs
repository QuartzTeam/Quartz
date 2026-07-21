using HarmonyLib;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Features.Status;
using Quartz.IO;
using System.Reflection;
using Quartz.Compat.Game;
namespace Quartz.Features.Restriction;
public static class Restriction {
    public static SettingsFile<RestrictionSettings> ConfMgr { get; private set; }
    public static RestrictionSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<RestrictionSettings>.Loaded("Restriction.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static int missCount;
    private static int overloadCount;
    private static bool failTriggered;
    private static void ResetCounters() {
        missCount = 0;
        overloadCount = 0;
        failTriggered = false;
    }
    private static void TriggerFail(string reason) {
        try {
            scrController c = scrController.instance;
            if(c == null || failTriggered) return;
            failTriggered = GameApi.FailByHitbox(c, reason);
        } catch { }
    }
    public static string JudgementName(HitMargin hit) {
        string key = hit switch {
            HitMargin.TooEarly => "JR_ALLOW_TOOEARLY",
            HitMargin.VeryEarly => "JR_ALLOW_VERYEARLY",
            HitMargin.EarlyPerfect => "JR_ALLOW_EARLYPERFECT",
            HitMargin.Perfect => "JR_ALLOW_PERFECT",
            HitMargin.LatePerfect => "JR_ALLOW_LATEPERFECT",
            HitMargin.VeryLate => "JR_ALLOW_VERYLATE",
            HitMargin.TooLate => "JR_ALLOW_TOOLATE",
            HitMargin.Multipress => "JR_ALLOW_MULTIPRESS",
            HitMargin.FailMiss => "JR_ALLOW_MISS",
            HitMargin.FailOverload => "JR_ALLOW_OVERLOAD_FAIL",
            HitMargin.OverPress => "JR_ALLOW_OVERLOAD_FAIL",
            _ => null,
        };
        string fallback = hit.ToString();
        return key == null ? fallback : MainCore.Tr.Get(key, fallback);
    }
    private static string FormatJrMessage(string msg, HitMargin hit) {
        if(string.IsNullOrEmpty(msg)) return msg;
        string name = JudgementName(hit);
        return msg.Replace("{judgement}", name).Replace("{judgment}", name);
    }
    private static bool ShouldFailFor(HitMargin margin) {
        int marginInt = (int)margin;
        switch(Conf.JRestrictMode) {
            case 1:
                return marginInt != (int)HitMargin.Perfect;
            case 2: {
                if(marginInt != (int)HitMargin.Perfect) return true;
                if(!XPerfectBridge.Active) return false;
                XPerfectBridge.Judge xj = XPerfectBridge.LastJudge();
                return xj != XPerfectBridge.Judge.None && xj != XPerfectBridge.Judge.X;
            }
            case 3: {
                int mask = Conf.JRestrictAllowedMask;
                if(mask == 0) return false;
                int bit = 1 << marginInt;
                return (mask & bit) == 0;
            }
            case 4:
                return margin == HitMargin.TooEarly;
            case 0:
            default: {
                try {
                    scrMistakesManager m = MistakesAccess.Get();
                    if(m == null) return false;
                    float acc = MistakesAccess.PercentAcc(m);
                    if(float.IsNaN(acc) || float.IsInfinity(acc)) return false;
                    return acc * 100f < Conf.JRestrictAccuracy;
                } catch {
                    return false;
                }
            }
        }
    }
    private static void AfterAddHit(HitMargin hit) {
        EnsureConf();
        if(!MainCore.IsModEnabled || hit == HitMargin.Auto) return;
        bool jrOn = Conf.JRestrictEnabled;
        bool dlOn = Conf.DeathLimitEnabled;
        if(!jrOn && !dlOn) return;
        if(hit == HitMargin.FailMiss) {
            missCount++;
        } else if(hit == HitMargin.FailOverload) {
            overloadCount++;
        }
        if(jrOn && ShouldFailFor(hit)) {
            TriggerFail(FormatJrMessage(Conf.JRestrictMessage, hit));
            return;
        }
        if(dlOn) {
            int deaths = missCount + overloadCount;
            if((Conf.MaxDeathsOn && deaths > Conf.MaxDeaths)
                || (Conf.MaxMissesOn && missCount > Conf.MaxMisses)
                || (Conf.MaxOverloadsOn && overloadCount > Conf.MaxOverloads)) {
                TriggerFail(Conf.DeathLimitMessage);
            }
        }
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(HitMargin hit) => AfterAddHit(hit);
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class ResetOnRunStartPatch {
        private static void Postfix() => ResetCounters();
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ResetOnRunExitPatch {
        private static void Postfix() => ResetCounters();
    }
}
