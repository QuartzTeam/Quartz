using HarmonyLib;
using Quartz.Compat.Game;
using UnityEngine;
namespace Quartz.Features.Nostalgia;
public static partial class Nostalgia {
    [HarmonyPatch(typeof(scnEditor), "Play")]
    private static class Space360TilePatch {
        private static bool Prefix(scnEditor __instance) {
            if(ShouldSpace360Tile
               && !Input.GetKeyDown(KeyCode.P)
               && !Input.GetKey(KeyCode.LeftShift)
               && !Input.GetKey(KeyCode.RightShift)
               && Input.GetKeyDown(KeyCode.Space)) {
                if(__instance.SelectionIsSingle()) {
                    scrFloor floor = __instance.selectedFloors[0];
                    object floatDir = GameApi.RotateDirection(
                        GameApi.RotateDirection(floor.floatDirection, true), true);
                    object stringDir = GameApi.RotateDirection(
                        GameApi.RotateDirection(floor.stringDirection, true), true);
                    Traverse.Create(__instance).Method(
                        "CreateFloorWithCharOrAngle",
                        floatDir, stringDir, true, true).GetValue();
                }
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(scrShowIfDebug), "Update")]
    private static class WeakAutoPatch {
        private static void Prefix() {
            if(ShouldWeakAuto) RDC.useOldAuto = false;
        }
        private static void Postfix() {
            if(ShouldWeakAuto) RDC.useOldAuto = true;
        }
    }
    [HarmonyPatch(typeof(scnEditor), "get_highBPM")]
    private static class WhiteAutoHighBpmPatch {
        private static void Postfix(ref bool __result) {
            if(ShouldWhiteAuto) __result = false;
        }
    }
    [HarmonyPatch(typeof(scnGame), "ResetScene")]
    private static class WhiteAutoResetPatch {
        private static void Postfix() {
            if(ShouldWhiteAuto && scnEditor.instance != null) scnEditor.instance.autoFailed = false;
        }
    }
    [HarmonyPatch(typeof(scnEditor), "Awake")]
    private static class LegacyEditorButtonsPatch {
        private static void Postfix() {
            ChangeEditorButtons(Enabled && Conf.LegacyEditorButtonsPositions);
            RemoveShadowAddOutline(Enabled && Conf.LegacyEditorButtonsDesigns);
        }
    }
    [HarmonyPatch(typeof(RDString), "GetWithCheck")]
    private static class LegacyTextsPatch {
        private static void Postfix(ref string __result) {
            if(!ShouldLegacyTexts) return;
            switch(__result) {
                case "눈폭풍":
                    __result = "눈폭충";
                    break;
                case "세피아":
                    __result = "소피아";
                    break;
                case "작곡가":
                    __result = "아티스트";
                    break;
            }
        }
    }
}
