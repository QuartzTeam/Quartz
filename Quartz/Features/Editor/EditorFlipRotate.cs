using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using UnityEngine;
using Quartz.Compat.Game;
namespace Quartz.Features.Editor;
public static partial class EditorFeature {
    internal static bool ShouldAdjustOnFlip => Enabled && Conf.AdjustOnFlip;
    internal static bool ShouldAdjustOnRotate => Enabled && Conf.AdjustOnRotate;
    internal static bool ShouldCustomAngleRotation => Enabled && Conf.CustomAngleRotation;
    private static bool customRotateRunning;
    private static bool customRotateCW;
    private const string PositionOffsetKey = "positionOffset";
    private static bool TrySelectionRange(out int first, out int last) {
        first = last = 0;
        scnEditor ed = scnEditor.instance;
        List<scrFloor> sel = ed != null ? ed.selectedFloors : null;
        if(sel == null || sel.Count == 0) return false;
        first = sel[0].seqID;
        last = sel[sel.Count - 1].seqID;
        return first != 0;
    }
    private static void AdjustPositionTracks(int first, int last, Func<Vector2, Vector2> transform) {
        scnEditor ed = scnEditor.instance;
        if(ed == null) return;
        List<LevelEvent> targets = ed.events.FindAll(e =>
            e.eventType == LevelEventType.PositionTrack && e.floor >= first && e.floor <= last);
        if(targets.Count == 0) return;
        using(new SaveStateScope(ed, false, true, false)) {
            foreach(LevelEvent e in targets)
                e[PositionOffsetKey] = transform(GameApi.EventGet<Vector2>(e, PositionOffsetKey));
            ed.RemakePath(true, true);
        }
    }
    private static Func<Vector2, Vector2> FlipTransform(bool horizontal) =>
        horizontal
            ? v => new Vector2(-v.x, v.y)
            : v => new Vector2(v.x, -v.y);
    private static Func<Vector2, Vector2> RotateTransform(bool cw) {
        double deg = Conf.CustomAngleRotation ? Conf.CustomAngle : 90.0;
        if(cw) deg = -deg;
        double rad = deg * (Math.PI / 180.0);
        float cos = (float)Math.Cos(rad);
        float sin = (float)Math.Sin(rad);
        return v => new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
    }
    private static Vector2 Rotate180(Vector2 v) => new(-v.x, -v.y);
    [HarmonyPatch(typeof(scnEditor), "FlipSelection")]
    private static class FlipSelectionPatch {
        private static void Postfix(bool horizontal) {
            if(!ShouldAdjustOnFlip) return;
            if(TrySelectionRange(out int first, out int last))
                AdjustPositionTracks(first, last, FlipTransform(horizontal));
        }
    }
    [HarmonyPatch(typeof(scnEditor), "FlipFloor")]
    private static class FlipFloorPatch {
        private static void Postfix(scrFloor floor, bool horizontal, bool remakePath) {
            if(!ShouldAdjustOnFlip || !remakePath || floor == null) return;
            AdjustPositionTracks(floor.seqID, floor.seqID, FlipTransform(horizontal));
        }
    }
    [HarmonyPatch(typeof(scnEditor), "RotateSelection")]
    private static class RotateSelectionPatch {
        private static void Postfix(bool CW) {
            if(!ShouldAdjustOnRotate) return;
            if(TrySelectionRange(out int first, out int last))
                AdjustPositionTracks(first, last, RotateTransform(CW));
        }
    }
    [HarmonyPatch(typeof(scnEditor), "RotateFloor")]
    private static class RotateFloorPatch {
        private static void Prefix(bool CW) {
            customRotateRunning = true;
            customRotateCW = CW;
        }
        private static void Finalizer() => customRotateRunning = false;
        private static void Postfix(scrFloor floor, bool CW, bool remakePath) {
            if(!ShouldAdjustOnRotate || !remakePath || floor == null) return;
            AdjustPositionTracks(floor.seqID, floor.seqID, RotateTransform(CW));
        }
    }
    [HarmonyPatch(typeof(scnEditor), "RotateSelection180")]
    private static class RotateSelection180Patch {
        private static void Postfix() {
            if(!ShouldAdjustOnRotate) return;
            if(TrySelectionRange(out int first, out int last))
                AdjustPositionTracks(first, last, Rotate180);
        }
    }
    [HarmonyPatch(typeof(scnEditor), "RotateFloor180")]
    private static class RotateFloor180Patch {
        private static void Postfix(scrFloor floor, bool remakePath) {
            if(!ShouldAdjustOnRotate || !remakePath || floor == null) return;
            AdjustPositionTracks(floor.seqID, floor.seqID, Rotate180);
        }
    }
    [HarmonyPatch]
    private static class CustomAngleCharPatch {
        private const string Codes = "UoTEJpRAMCBYDVFZNxLWHQGq";
        private static bool Prepare() => GameApi.RotateCharTarget != null;
        private static MethodBase TargetMethod() => GameApi.RotateCharTarget;
        private static bool Prefix(char __0, ref char __result) {
            if(!ShouldCustomAngleRotation || !customRotateRunning) return true;
            __result = __0;
            int idx = Codes.IndexOf(__0);
            if(idx == -1) return false;
            for(idx += (customRotateCW ? 1 : -1) * (int)(Conf.CustomAngle / 15f); idx < 0; idx += Codes.Length) { }
            idx %= Codes.Length;
            __result = Codes[idx];
            return false;
        }
    }
    [HarmonyPatch]
    private static class CustomAngleFloatPatch {
        private static bool Prepare() => GameApi.RotateFloatTarget != null;
        private static MethodBase TargetMethod() => GameApi.RotateFloatTarget;
        private static bool Prefix(float __0, ref float __result) {
            if(!ShouldCustomAngleRotation || !customRotateRunning) return true;
            __result = __0;
            if(__0 == 999f) return false;
            __result = __0 + (customRotateCW ? -1 : 1) * Conf.CustomAngle;
            return false;
        }
    }
}
