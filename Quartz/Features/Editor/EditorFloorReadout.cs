using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ADOFAI;
using Quartz.Resource;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Quartz.Compat.Game;
namespace Quartz.Features.Editor;
public static partial class EditorFeature {
    internal static bool ShouldShowFloorReadout => Enabled && Conf.ShowAny;
    private static scrFloor readoutFloor;
    private static GameObject readoutLabel;
    private static TextMeshProUGUI readoutTmp;
    private static string readoutCache;
    private static long readoutSig;
    private static bool hasReadoutSig;
    private const string AngleColor = "#ff5252";
    private const string BeatsColor = "#52a9ff";
    private const string CountColor = "#8a8a8a";
    private const string DurationColor = "#ffffff";
    private const float ReadoutFontScale = 0.7f;
    private const float GameTextSizeScale = 0.5f;
    private static void ReconcileFloorReadout() {
        bool want;
        try {
            want = ShouldShowFloorReadout
                && ADOBase.isLevelEditor
                && scnEditor.instance != null
                && !scnEditor.instance.playMode;
        } catch {
            return;
        }
        if(!want) {
            ClearReadout();
            return;
        }
        try {
            scnEditor editor = scnEditor.instance;
            if(editor == null) {
                ClearReadout();
                return;
            }
            if(editor.SelectionIsEmpty() || editor.showFloorNums) {
                ClearReadout();
                return;
            }
            UpdateReadout(editor);
        } catch {
        }
    }
    private static void UpdateReadout(scnEditor editor) {
        List<scrFloor> selected = editor.selectedFloors;
        if(selected == null || selected.Count == 0) {
            ClearReadout();
            return;
        }
        scrFloor host = PickReadoutFloor(selected);
        if(host == null) {
            ClearReadout();
            return;
        }
        long sig = ReadoutSignature(editor, selected);
        string text;
        if(hasReadoutSig && sig == readoutSig) {
            text = readoutCache;
        } else {
            text = BuildReadout(editor, selected) ?? "";
            readoutCache = text;
            readoutSig = sig;
            hasReadoutSig = true;
        }
        if(text.Length == 0) {
            ClearReadout();
            return;
        }
        if(!EnsureLabel(host)) return;
        bool dirty = false;
        TMP_FontAsset want = FontManager.Current;
        if(want != null && readoutTmp.font != want) {
            readoutTmp.font = want;
            dirty = true;
        }
        if(readoutTmp.text != text) {
            readoutTmp.text = text;
            dirty = true;
        }
        if(dirty) ApplyReadoutShadow();
    }
    private static void ApplyReadoutShadow() {
        if(readoutTmp == null) return;
        float offset = readoutTmp.fontSize * 0.12f;
        TMPTextShadow.Apply(readoutTmp, true, offset, -offset, 0f, new Color(0f, 0f, 0f, 0.5f));
    }
    private static bool EnsureLabel(scrFloor host) {
        if(readoutLabel != null && readoutFloor == host && readoutTmp != null) return true;
        ClearReadout();
        return CreateLabel(host);
    }
    private static bool CreateLabel(scrFloor host) {
        scrLetterPress src = host.editorNumText;
        if(src == null) return false;
        GameObject clone = Object.Instantiate(src.gameObject, src.transform.parent);
        clone.name = "QuartzFloorReadout";
        clone.transform.localPosition = src.transform.localPosition;
        clone.transform.localRotation = src.transform.localRotation;
        clone.transform.localScale = src.transform.localScale;
        float baseSize = 24f;
        GameObject textGo = clone;
        Text gameText = clone.GetComponentInChildren<Text>(true);
        if(gameText != null) {
            baseSize = gameText.fontSize;
            textGo = gameText.gameObject;
            Object.DestroyImmediate(gameText);
        }
        foreach(scrLetterPress lp in clone.GetComponentsInChildren<scrLetterPress>(true)) Object.DestroyImmediate(lp);
        foreach(BaseMeshEffect fx in clone.GetComponentsInChildren<BaseMeshEffect>(true)) Object.DestroyImmediate(fx);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = FontManager.Current;
        tmp.alignment = TextAlignmentOptions.Center;
        TextCompat.NoWrap(tmp);
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;
        tmp.color = Color.white;
        tmp.fontSize = Mathf.Max(1f, baseSize * GameTextSizeScale * ReadoutFontScale);
        clone.SetActive(true);
        readoutLabel = clone;
        readoutTmp = tmp;
        readoutFloor = host;
        return true;
    }
    private static scrFloor PickReadoutFloor(List<scrFloor> selected) {
        scrFloor first = selected[0];
        if(first != null && first.enabled) return first;
        scrFloor last = selected[selected.Count - 1];
        if(last != null && last.enabled) return last;
        scrCamera camera = GameApi.Camera;
        Vector3 cam = camera != null ? camera.transform.position : Vector3.zero;
        cam.z = 0f;
        float best = float.PositiveInfinity;
        scrFloor nearest = null;
        foreach(scrFloor floor in selected) {
            if(floor == null || !floor.enabled) continue;
            Vector3 p = floor.transform.position;
            p.z = 0f;
            float d = Vector3.Distance(p, cam);
            if(d < best) {
                best = d;
                nearest = floor;
            }
        }
        return nearest;
    }
    private static long ReadoutSignature(scnEditor editor, List<scrFloor> selected) {
        unchecked {
            long h = 17;
            h = h * 31 + selected.Count;
            foreach(scrFloor floor in selected) {
                if(floor == null) {
                    h = h * 31 + 1;
                    continue;
                }
                h = h * 31 + floor.seqID;
                h = h * 31 + floor.floatDirection.GetHashCode();
                h = h * 31 + floor.speed.GetHashCode();
            }
            h = h * 31 + editor.levelData.bpm.GetHashCode();
            int flags = (Conf.ShowFloorAngle ? 1 : 0)
                | (Conf.ShowFloorBeats ? 2 : 0)
                | (Conf.ShowFloorCount ? 4 : 0)
                | (Conf.ShowFloorDuration ? 8 : 0)
                | (Conf.UseTulttakModBehavior ? 16 : 0);
            h = h * 31 + flags;
            return h;
        }
    }
    private static string BuildReadout(scnEditor editor, List<scrFloor> selected) {
        int iterations = selected.Count;
        if(Conf.UseTulttakModBehavior && iterations > 1) {
            iterations--;
        }
        int lastSeq = editor.floors.Count - 1;
        double totalAngle = 0d;
        for(int i = 0; i < iterations; i++) {
            scrFloor floor = selected[i];
            if(floor == null || floor.seqID == lastSeq)
                continue;
            double arc;
            try {
                arc = ADOBase.lm.CalculateSingleFloorAngleLength(floor);
            } catch {
                arc = floor.angleLength;
            }
            float speedFactor = i == 0 ? 1f : selected[0].speed / floor.speed;
            totalAngle += arc * speedFactor * Mathf.Rad2Deg;
            foreach(LevelEvent e in editor.events) {
                if(e == null || !e.active || e.floor != floor.seqID) continue;
                double extra = e.eventType switch {
                    LevelEventType.Pause => e.GetFloat("duration") * 180d,
                    LevelEventType.FreeRoam => e.GetInt("duration") * 180d,
                    _ => 0d,
                };
                totalAngle += extra * speedFactor;
            }
        }
        if(totalAngle == 0d) return null;
        StringBuilder sb = new();
        bool any = false;
        if(Conf.ShowFloorAngle) {
            Append(sb, ref any, AngleColor,
                totalAngle.ToString("#.####", CultureInfo.InvariantCulture) + "°");
        }
        if(Conf.ShowFloorBeats) {
            Append(sb, ref any, BeatsColor,
                (totalAngle / 180d).ToString("#.####", CultureInfo.InvariantCulture) + "♩");
        }
        if(Conf.ShowFloorCount) {
            Append(sb, ref any, CountColor,
                selected.Count.ToString(CultureInfo.InvariantCulture) + "#");
        }
        if(Conf.ShowFloorDuration) {
            double seconds = totalAngle / (selected[0].speed * editor.levelData.bpm * 3d);
            Append(sb, ref any, DurationColor,
                seconds.ToString("0.######", CultureInfo.InvariantCulture) + "s");
        }
        return sb.ToString();
    }
    private static void Append(StringBuilder sb, ref bool any, string color, string body) {
        if(any) sb.Append('\n');
        sb.Append("<color=").Append(color).Append('>').Append(body).Append("</color>");
        any = true;
    }
    private static void ClearReadout() {
        if(readoutTmp != null) TMPTextShadow.Remove(readoutTmp);
        if(readoutLabel != null) Object.DestroyImmediate(readoutLabel);
        readoutLabel = null;
        readoutTmp = null;
        readoutFloor = null;
    }
}
