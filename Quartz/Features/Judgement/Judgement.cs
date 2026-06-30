using HarmonyLib;
using Quartz.Core;
using UnityEngine;

namespace Quartz.Features.Judgement;

// Tracks per-judgement hit counts for the current run, ported from the
// original KorenResourcePack. Nine display slots, symmetric around Perfect:
// Overload, Too Early, Very Early, Early Perfect, Perfect (+Auto), Late
// Perfect, Very Late, Too Late, Miss.
internal static class Judgement {
    internal const int Slots = 9;

    // Slot tint colors, same values as v1: purple at the extremes, red/orange
    // approaching center, greens around Perfect.
    internal static readonly Color[] SlotColors = [
        new(0.78f, 0.35f, 1f, 1f),
        new(1f, 0.22f, 0.22f, 1f),
        new(1f, 0.44f, 0.31f, 1f),
        new(0.63f, 1f, 0.31f, 1f),
        new(0.38f, 1f, 0.31f, 1f),
        new(0.63f, 1f, 0.31f, 1f),
        new(1f, 0.44f, 0.31f, 1f),
        new(1f, 0.22f, 0.22f, 1f),
        new(0.78f, 0.35f, 1f, 1f),
    ];

    private static readonly int[] counts = new int[16];

    // TEMP DIAG (remove after debugging the overlay): throttle so each run logs
    // only its first handful of hits, re-armed on every Reset.
    private static int diagHits;

    internal static void Reset() {
        System.Array.Clear(counts, 0, counts.Length);
        diagHits = 0;
    }

    internal static int SlotCount(int slot) => slot switch {
        0 => Count(HitMargin.FailOverload),
        1 => Count(HitMargin.TooEarly),
        2 => Count(HitMargin.VeryEarly),
        3 => Count(HitMargin.EarlyPerfect),
        4 => Count(HitMargin.Perfect) + Count(HitMargin.Auto),
        5 => Count(HitMargin.LatePerfect),
        6 => Count(HitMargin.VeryLate),
        7 => Count(HitMargin.TooLate),
        8 => Count(HitMargin.FailMiss),
        _ => 0,
    };

    private static int Count(HitMargin hit) {
        int idx = (int)hit;
        return idx >= 0 && idx < counts.Length ? counts[idx] : 0;
    }

    [HarmonyPatch(typeof(scrMarginTracker), "AddHit", typeof(HitMargin))]
    private static class AddHitPatch {
        private static void Postfix(HitMargin hit) {
            int idx = (int)hit;
            if(MainCore.IsModEnabled && idx >= 0 && idx < counts.Length) counts[idx]++;

            // TEMP DIAG: logs every AddHit (even when disabled) for the first few
            // hits of each run, plus the live XPerfect bridge readout.
            if(diagHits < 8) {
                diagHits++;
                MainCore.Log.Msg(
                    $"[JDIAG] AddHit hit={hit}({idx}) modEnabled={MainCore.IsModEnabled} " +
                    $"perfect={counts[(int)HitMargin.Perfect]} auto={counts[(int)HitMargin.Auto]} " +
                    $"miss={counts[(int)HitMargin.FailMiss]} overload={counts[(int)HitMargin.FailOverload]} " +
                    $"| xActive={Interop.XPerfectBridge.Active} X={Interop.XPerfectBridge.XCount()} " +
                    $"plus={Interop.XPerfectBridge.PlusCount()} minus={Interop.XPerfectBridge.MinusCount()}");
            }
        }
    }

    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class ResetOnRunStartPatch {
        private static void Postfix() {
            MainCore.Log.Msg($"[JDIAG] Reset via scnGame.Play (modEnabled={MainCore.IsModEnabled})");
            if(MainCore.IsModEnabled) Reset();
        }
    }

    // Built-in/official levels never instantiate scnGame, so scnGame.Play (the
    // custom-level run-start) never fires for them — their run begins in
    // scrController.Start via WaitForStartCo. Without this the counts carried
    // over across restarts of a main level. Start runs on every scene (re)load
    // and Restart reloads the scene, so this covers first play and every retry.
    [HarmonyPatch(typeof(scrController), "Start")]
    private static class ResetOnControllerStartPatch {
        private static void Postfix(scrController __instance) {
            MainCore.Log.Msg($"[JDIAG] scrController.Start (gameworld={__instance.gameworld}, modEnabled={MainCore.IsModEnabled})");
            if(MainCore.IsModEnabled && __instance.gameworld) Reset();
        }
    }

    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ResetOnRunExitPatch {
        private static void Postfix() {
            MainCore.Log.Msg($"[JDIAG] Reset via StartLoadingScene (modEnabled={MainCore.IsModEnabled})");
            if(MainCore.IsModEnabled) Reset();
        }
    }
}
