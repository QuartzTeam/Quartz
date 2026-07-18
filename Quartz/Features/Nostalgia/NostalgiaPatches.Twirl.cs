using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Quartz.Features.Nostalgia;
public static partial class Nostalgia {
    // Floors that actually have arrow child objects. The old scene-global
    // "ever applied" latch made EVERY UpdateIconSprite call in the scene pay
    // 2× transform.Find + DestroyImmediate once the toggle had ever been on
    // (~10k string Finds on a 5k-tile load, and it kept paying after the
    // toggle went off until the next scene). Tracking the floors that hold
    // arrows confines the cleanup cost to exactly those floors.
    private static readonly HashSet<int> twirlArrowFloors = [];
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class TwirlSceneResetPatch {
        // fresh scenes carry no arrow objects
        private static void Postfix() => twirlArrowFloors.Clear();
    }
    [HarmonyPatch(typeof(scrFloor), "UpdateIconSprite")]
    private static class LegacyTwirlPatch {
        private static void Postfix(scrFloor __instance) {
            bool hadArrows = twirlArrowFloors.Count > 0
                && twirlArrowFloors.Remove(__instance.GetInstanceID());
            if(!ShouldLegacyTwirl && !hadArrows) return;
            if(hadArrows) {
                Object.DestroyImmediate(__instance.transform.Find("arrow_renderer")?.gameObject);
                Object.DestroyImmediate(__instance.transform.Find("arrow_outline_renderer")?.gameObject);
            }
            if(!ShouldLegacyTwirl
               || __instance.isportal
               || (__instance.floorIcon != FloorIcon.Swirl && __instance.floorIcon != FloorIcon.SwirlCW)) {
                return;
            }
            NostalgiaImages.EnsureLoaded();
            float num = (float)scrMisc.GetAngleMoved(
                (float)__instance.entryangle, (float)__instance.exitangle, !__instance.isCCW);
            if(Mathf.Abs(num) <= 1E-06f && !__instance.midSpin) num = Mathf.PI * 2;
            __instance.SetIconSprite(__instance.isCCW ? NostalgiaImages.SwirlCcw : NostalgiaImages.SwirlCw);
            __instance.SetIconFlipped(false);
            float num2 = 0f;
            if(__instance.floorRenderer is FloorSpriteRenderer) {
                float num3 = (ADOBase.lm?.lm2?.BigTiles ?? false) ? Mathf.PI / -2 : Mathf.PI / 2;
                num2 = (float)(((scrMisc.mod((float)(__instance.exitangle - __instance.entryangle), Mathf.PI * 2) <= Mathf.PI)
                    ? __instance.entryangle : __instance.exitangle) - num3);
            }
            float num4 = -(float)__instance.entryangle + Mathf.PI / 2
                - num / 2f * (__instance.isCCW ? -1 : 1) - Mathf.PI / 2 + num2;
            __instance.SetIconAngle((__instance.floorRenderer is FloorSpriteRenderer) ? num4 : (-num4));
            __instance.SetIconOutlineSprite(__instance.isCCW ? NostalgiaImages.SwirlCcwOutline : NostalgiaImages.SwirlCwOutline);
            if(Conf.TwirlWithoutArrow) return;
            Renderer iconRef = (Renderer)__instance.iconsprite ?? __instance.floorRenderer.renderer;
            twirlArrowFloors.Add(__instance.GetInstanceID());
            GameObject arrowObj = new();
            arrowObj.transform.parent = __instance.transform;
            TwirlRenderer arrow = arrowObj.AddComponent<TwirlRenderer>();
            arrow.outline = false;
            arrow.floor = __instance;
            arrow.sr.sprite = __instance.isCCW ? NostalgiaImages.ArrowCcw : NostalgiaImages.ArrowCw;
            arrow.sr.sortingLayerID = iconRef.sortingLayerID;
            arrow.sr.sortingLayerName = iconRef.sortingLayerName;
            arrow.name = "arrow_renderer";
            Vector3 localPos = new(
                0.3f * Mathf.Cos(num4 + 90 * Mathf.Deg2Rad),
                0.3f * Mathf.Sin(num4 + 90 * Mathf.Deg2Rad), 0f);
            arrow.transform.localPosition = localPos;
            arrow.transform.localEulerAngles = new Vector3(0f, 0f, num4 * Mathf.Rad2Deg);
            bool forward = num < Mathf.PI - Mathf.Pow(10f, -6f);
            arrow.sr.color = forward ? Color.red : Color.blue;
            if(__instance.outline) {
                GameObject arrowOutlineObj = new();
                arrowOutlineObj.transform.parent = __instance.transform;
                TwirlRenderer arrowOutline = arrowOutlineObj.AddComponent<TwirlRenderer>();
                arrowOutline.outline = true;
                arrowOutline.floor = __instance;
                arrowOutline.sr.sprite = __instance.isCCW ? NostalgiaImages.ArrowCcwOutline : NostalgiaImages.ArrowCwOutline;
                arrowOutline.sr.sortingLayerID = iconRef.sortingLayerID;
                arrowOutline.sr.sortingLayerName = iconRef.sortingLayerName;
                arrowOutline.name = "arrow_outline_renderer";
                arrowOutline.transform.localPosition = localPos;
                arrowOutline.transform.localEulerAngles = new Vector3(0f, 0f, num4 * Mathf.Rad2Deg);
            }
        }
    }
}
public sealed class TwirlRenderer : MonoBehaviour {
    public SpriteRenderer sr;
    public scrFloor floor;
    public bool outline;
    private void Awake() => sr = gameObject.GetOrAddComponent<SpriteRenderer>();
    private void LateUpdate() {
        if(floor == null) return;
        if(floor.floorIcon != FloorIcon.Swirl && floor.floorIcon != FloorIcon.SwirlCW) {
            Destroy(gameObject); // whole arrow object, or the orphaned SpriteRenderer keeps drawing
            return;
        }
        Renderer iconRef = (Renderer)floor.iconsprite ?? floor.floorRenderer.renderer;
        int order = iconRef.sortingOrder + (outline ? 1 : 2);
        if(sr.sortingOrder != order) sr.sortingOrder = order;
        Vector3 scale = floor.transform.localScale;
        Vector3 current = sr.transform.localScale;
        if(current.x != scale.x || current.y != scale.y || current.z != scale.z)
            sr.transform.localScale = scale;
        float alpha = floor.floorRenderer.color.a * floor.opacity;
        if(sr.color.a != alpha) sr.SetAlpha(alpha);
    }
}
