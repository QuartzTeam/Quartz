using Quartz.Core;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Quartz.Compat.Game;
namespace Quartz.UI.Editor;
internal static class KvTabs {
    private static float TrackHeight => 30f * KvPalette.Scale;
    private static float TrackPad => 3f * KvPalette.Scale;
    private static float PillGap => 5f * KvPalette.Scale;
    private static float PillHeight => 24f * KvPalette.Scale;
    private static float LabelSize => 13f * KvPalette.Scale;
    private static float LabelPadX => 8f * KvPalette.Scale;
    internal static void Build<T>(
        RectTransform root, IReadOnlyList<T> values, Func<T, string> name, Func<T, string> key,
        T value, Action<T> onChanged
    ) {
        RectTransform track = GenerateUI.Row(root, TrackHeight);
        GameObject trackObj = track.gameObject;
        Image bg = trackObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(KvPalette.Radius);
        bg.type = Image.Type.Sliced;
        bg.color = KvPalette.TabTrack;
        bg.raycastTarget = false;
        GameObject viewObj = new("TabViewport");
        viewObj.transform.SetParent(track, false);
        RectTransform viewport = viewObj.AddComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(TrackPad, TrackPad);
        viewport.offsetMax = new Vector2(-TrackPad, -TrackPad);
        viewObj.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewObj.AddComponent<RectMask2D>();
        GameObject tabsObj = new("Tabs");
        tabsObj.transform.SetParent(viewport, false);
        RectTransform tabs = tabsObj.AddComponent<RectTransform>();
        tabs.anchorMin = new Vector2(0f, 0f);
        tabs.anchorMax = new Vector2(0f, 1f);
        tabs.pivot = new Vector2(0f, 0.5f);
        HorizontalLayoutGroup layout = tabsObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = PillGap;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = tabsObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        ScrollRect scroll = viewObj.AddComponent<ScrollRect>();
        scroll.content = tabs;
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 0f;
        scroll.inertia = false;
        viewObj.AddComponent<KvToolbar.StripWheel>().Init(viewport, tabs);
        List<(T Value, Image Bg, TextMeshProUGUI Label)> pills = [];
        List<(LayoutElement Le, TextMeshProUGUI Label, float Pad)> measured = [];
        RectTransform selectedPill = null;
        T current = value;
        void Refresh(bool animate) {
            foreach((T optValue, Image pillBg, TextMeshProUGUI label) in pills) {
                bool on = EqualityComparer<T>.Default.Equals(optValue, current);
                Color bgTo = on ? KvPalette.TabActive : KvPalette.TabTrack;
                Color labelTo = on ? KvPalette.TextWhite : KvPalette.TabIdleText;
                if(animate) {
                    MainCore.TC.Play(pillBg.GTColor(bgTo, 0.13f).SetEasing(Easing.OutSine));
                    MainCore.TC.Play(label.GTColor(labelTo, 0.13f).SetEasing(Easing.OutSine));
                } else {
                    pillBg.color = bgTo;
                    label.color = labelTo;
                }
            }
        }
        foreach(T optValue in values) {
            T captured = optValue;
            string text = name(optValue);
            GameObject obj = new("Tab_" + text.Replace(" ", ""));
            obj.transform.SetParent(tabs, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minHeight = PillHeight;
            le.preferredHeight = PillHeight;
            le.flexibleWidth = 0f;
            Image pillBg = obj.AddComponent<Image>();
            pillBg.sprite = MainCore.Spr.GetFilled(KvPalette.RadiusSmall);
            pillBg.type = Image.Type.Sliced;
            TextMeshProUGUI label = GenerateUI.AddText(rect, true);
            label.fontSize = LabelSize;
            label.alignment = TextAlignmentOptions.Center;
            TextCompat.NoWrap(label);
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            if(key != null) GenerateUI.Localize(label, key(optValue), text);
            else label.text = text;
            float width = label.GetPreferredValues(label.text).x + LabelPadX * 2f;
            le.preferredWidth = width;
            le.minWidth = width;
            measured.Add((le, label, LabelPadX * 2f));
            pills.Add((optValue, pillBg, label));
            if(EqualityComparer<T>.Default.Equals(optValue, current)) selectedPill = rect;
            GenerateUI.AddButton(obj, btn => {
                if(btn != InputButton.Left) return;
                if(EqualityComparer<T>.Default.Equals(current, captured)) return;
                current = captured;
                Refresh(true);
                onChanged?.Invoke(captured);
            }, false);
            EventTrigger trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
            KvToolbar.ForwardDrag(trigger, scroll);
            GTween hoverSeq = null;
            void HoverTo(Color bgTo, Color labelTo) {
                hoverSeq?.Kill();
                hoverSeq = GTweenSequenceBuilder.New()
                    .Join(pillBg.GTColor(bgTo, 0.12f).SetEasing(Easing.OutSine))
                    .Join(label.GTColor(labelTo, 0.12f).SetEasing(Easing.OutSine))
                    .Build();
                MainCore.TC.Play(hoverSeq);
            }
            UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => {
                if(EqualityComparer<T>.Default.Equals(current, captured)) return;
                HoverTo(KvPalette.SurfaceHover, KvPalette.TextWhite);
            }, trigger);
            UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => {
                if(EqualityComparer<T>.Default.Equals(current, captured)) return;
                HoverTo(KvPalette.TabTrack, KvPalette.TabIdleText);
            }, trigger);
        }
        Refresh(false);
        KvTabRemeasure.Attach(tabs, measured);
        if(selectedPill != null) {
            RevealSelected reveal = viewObj.AddComponent<RevealSelected>();
            reveal.Content = tabs;
            reveal.Viewport = viewport;
            reveal.Pill = selectedPill;
        }
    }
    private sealed class RevealSelected : MonoBehaviour {
        internal RectTransform Content, Viewport, Pill;
        private float lastWidth = -1f;
        private int stableFrames, frames;
        private const int MaxFrames = 120;
        private const int StableThreshold = 2;
        private void LateUpdate() {
            if(Content == null || Viewport == null || Pill == null) {
                Destroy(this);
                return;
            }
            float width = Content.rect.width;
            if(Mathf.Abs(width - lastWidth) < 0.5f) stableFrames++;
            else stableFrames = 0;
            lastWidth = width;
            if(++frames < MaxFrames && stableFrames < StableThreshold) return;
            float viewWidth = Viewport.rect.width;
            float overflow = width - viewWidth;
            if(overflow > 0f) {
                float pillMax = Pill.localPosition.x + Pill.rect.xMax;
                float shift = Mathf.Clamp(Mathf.Min(0f, viewWidth - pillMax), -overflow, 0f);
                Content.anchoredPosition = new Vector2(shift, Content.anchoredPosition.y);
            }
            Destroy(this);
        }
    }
}
