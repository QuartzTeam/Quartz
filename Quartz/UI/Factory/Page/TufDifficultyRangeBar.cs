using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Easings;
using GTweens.Tweens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Quartz.UI.Factory.Page;

internal sealed class TufDifficultyRangeBar : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    // Quantum spectrum, low → high, one stop per QuantumNames entry (Qq, GQ0…GQ4,
    // UQ0…UQ4). Mirrors TUFHelper's QuantumGradient so the bar reads the same.
    private static readonly Color[] QuantumStops = {
        new Color32(255, 255, 255, 255), new Color32(241, 161, 5, 255), new Color32(235, 123, 41, 255),
        new Color32(227, 85, 74, 255), new Color32(192, 52, 94, 255), new Color32(214, 16, 136, 255),
        new Color32(113, 73, 164, 255), new Color32(63, 32, 103, 255), new Color32(47, 43, 54, 255),
        new Color32(126, 0, 0, 255), new Color32(255, 254, 254, 255)
    };

    // P → G → U spectrum, one-to-one with TUFHelper's PGUGradient stops (cyan blue,
    // bright green, orange, red, magenta, deep purple, black) so the bar reads the same.
    private static readonly Color[] PguStops = {
        new Color32(0, 153, 255, 255), new Color32(0, 255, 136, 255), new Color32(242, 167, 0, 255),
        new Color32(225, 79, 79, 255), new Color32(210, 0, 151, 255), new Color32(45, 29, 65, 255),
        new Color32(0, 0, 0, 255)
    };

    private RectTransform track;
    private RectTransform minHandle;
    private RectTransform maxHandle;
    private TMP_Text rangeLabel;
    private Texture2D gradientTex;
    private Image toggleBg;
    private CanvasGroup trackCg;
    private GTween trackFade;
    private IReadOnlyList<string> labels;
    private int lastIndex;
    private Action<int, int> commit;
    private Action disable;
    private bool toggleable;
    private bool active = true;
    private int minIndex;
    private int maxIndex;
    private bool dragging;
    private bool draggingMax;
    private int appliedMin = -1;
    private int appliedMax = -1;

    public static TufDifficultyRangeBar Create(RectTransform root, int min, int max, Action<int, int> onCommit) {
        AddHeadingLabel(root, "TUF_DIFFICULTY_FILTER", "Difficulty range");
        TMP_Text value = AddValueLabel(root);
        RectTransform track = AddTrack(root, "PGU Range");
        AddGradient(track, PguStops, out Texture2D texture);
        TufDifficultyRangeBar bar = Attach(track, value, TufDifficultyFilter.RankedNames, onCommit);
        bar.gradientTex = texture;
        bar.SetRange(min, max);
        return bar;
    }

    public static TufDifficultyRangeBar CreateQuantum(RectTransform root, Transform toggleParent,
        bool enabled, int min, int max,
        Action<int, int> onCommit, Action onDisable) {
        TMP_Text value = AddValueLabel(root);
        RectTransform track = AddTrack(root, "Quantum Range");
        // Gradient goes down before the handles so the handles render on top of it,
        // matching how the PGU bands sit behind their handles.
        AddGradient(track, QuantumStops, out Texture2D texture);
        TufDifficultyRangeBar bar = Attach(track, value, CompactNames(TufDifficultyFilter.QuantumNames), onCommit);
        bar.gradientTex = texture;
        bar.disable = onDisable;
        bar.toggleable = true;
        // Explicit on/off button where the heading label would sit — quantum is opt-in.
        bar.toggleBg = AddToggleButton(toggleParent, "TUF_QUANTUM", "Quantum", bar.ToggleActive);
        bar.SetQuantum(enabled, min, max);
        return bar;
    }

    private static void AddHeadingLabel(RectTransform root, string key, string text) {
        TMP_Text heading = AddText(root, text, 15f, TextAlignmentOptions.Left);
        heading.rectTransform.anchorMin = new(0f, 1f);
        heading.rectTransform.anchorMax = new(0.45f, 1f);
        heading.rectTransform.offsetMin = new(0f, -24f);
        heading.rectTransform.offsetMax = Vector2.zero;
        heading.gameObject.AddComponent<TextLocalization>().Init(key, text);
    }

    private static TMP_Text AddValueLabel(RectTransform root) {
        TMP_Text value = AddText(root, "", 15f, TextAlignmentOptions.Right);
        value.rectTransform.anchorMin = new(0.45f, 1f);
        value.rectTransform.anchorMax = Vector2.one;
        value.rectTransform.offsetMin = new(0f, -24f);
        value.rectTransform.offsetMax = Vector2.zero;
        value.color = new(1f, 1f, 1f, 0.62f);
        return value;
    }

    private static RectTransform AddTrack(RectTransform root, string name) {
        RectTransform track = MakeRect(name, root, new(0f, 0f), new(1f, 0f), new(8f, 8f), new(-8f, 28f));
        track.gameObject.AddComponent<EmptyGraphic>().raycastTarget = true;
        return track;
    }

    // A labelled on/off button that occupies the heading slot. Returns its background
    // so RefreshActive can tint it by state (bright when on, muted when off).
    private static Image AddToggleButton(Transform root, string key, string text, Action onToggle) {
        RectTransform rect = MakeRect("Toggle " + text, root, new(0f, 1f), new(0f, 1f), new(0f, -26f), new(104f, -2f));
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = 104f;
        Image bg = rect.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.ObjectBG;

        GameObject iconObj = new("Quantum Icon");
        iconObj.transform.SetParent(rect, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new(0f, 0.5f);
        iconRect.sizeDelta = new(19f, 19f);
        iconRect.anchoredPosition = new(15f, 0f);
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = MainCore.Spr.Get(UISprite.QuantumQ);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text label = AddText(rect, text, 14f, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new(28f, 0f);
        label.rectTransform.offsetMax = new(-8f, 0f);
        label.gameObject.AddComponent<TextLocalization>().Init(key, text);

        GenerateUI.AddButton(rect.gameObject, button => {
            if(button == PointerEventData.InputButton.Left) onToggle();
        });
        return bg;
    }

    private static TufDifficultyRangeBar Attach(RectTransform track, TMP_Text value,
        IReadOnlyList<string> labels, Action<int, int> onCommit) {
        RectTransform minHandle = AddHandle(track, "Minimum", Color.white);
        RectTransform maxHandle = AddHandle(track, "Maximum", Color.white);
        TufDifficultyRangeBar bar = track.gameObject.AddComponent<TufDifficultyRangeBar>();
        bar.track = track;
        bar.trackCg = track.gameObject.AddComponent<CanvasGroup>();
        bar.rangeLabel = value;
        bar.minHandle = minHandle;
        bar.maxHandle = maxHandle;
        bar.labels = labels;
        bar.lastIndex = labels.Count - 1;
        bar.commit = onCommit;
        return bar;
    }

    public void SetRange(int min, int max) {
        if(dragging) return;
        minIndex = Mathf.Clamp(min, 0, lastIndex);
        maxIndex = Mathf.Clamp(max, minIndex, lastIndex);
        Refresh();
    }

    public void SetQuantum(bool enabled, int min, int max) {
        if(dragging) return;
        active = enabled;
        minIndex = Mathf.Clamp(min, 0, lastIndex);
        maxIndex = Mathf.Clamp(max, minIndex, lastIndex);
        RefreshActive();
        Refresh();
    }

    public void OnPointerDown(PointerEventData eventData) {
        if(eventData.button != PointerEventData.InputButton.Left) return;
        int step = StepAt(eventData);
        dragging = true;
        draggingMax = PickHandle(step);
        MoveActive(step);
    }

    public void OnDrag(PointerEventData eventData) {
        if(!dragging) return;
        MoveActive(StepAt(eventData));
    }

    public void OnPointerUp(PointerEventData eventData) {
        if(!dragging) return;
        MoveActive(StepAt(eventData));
        dragging = false;
        commit?.Invoke(minIndex, maxIndex);
    }

    // Flip the whole quantum filter on/off. Turning on re-commits the current range;
    // turning off clears it (the range is restored from the service on the next sync).
    private void ToggleActive() {
        if(!toggleable) return;
        active = !active;
        RefreshActive();
        Refresh();
        if(active) commit?.Invoke(minIndex, maxIndex);
        else disable?.Invoke();
    }

    // Grab the handle nearest the click; on a tie (incl. both handles coincident),
    // a click to the right of the pair takes the max handle, otherwise the min.
    private bool PickHandle(int step) {
        int dMin = Mathf.Abs(step - minIndex);
        int dMax = Mathf.Abs(step - maxIndex);
        if(dMin != dMax) return dMax < dMin;
        return step > maxIndex;
    }

    // Move only the active handle. If it crosses the other, swap roles so the
    // handle under the cursor keeps following it (never sticks/collapses).
    private void MoveActive(int step) {
        step = Mathf.Clamp(step, 0, lastIndex);
        if(draggingMax) {
            maxIndex = step;
            if(maxIndex < minIndex) {
                (minIndex, maxIndex) = (maxIndex, minIndex);
                draggingMax = false;
            }
        } else {
            minIndex = step;
            if(minIndex > maxIndex) {
                (minIndex, maxIndex) = (maxIndex, minIndex);
                draggingMax = true;
            }
        }
        Refresh();
    }

    private int StepAt(PointerEventData eventData) {
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            track, eventData.position, eventData.pressEventCamera, out Vector2 local)) return minIndex;
        float normalized = Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, local.x);
        return Mathf.Clamp(Mathf.RoundToInt(normalized * lastIndex), 0, lastIndex);
    }

    // Re-entered on every pointer move while dragging (MoveActive) and on every
    // service tick (RefreshControls → SetRange/SetQuantum), almost always with an
    // unchanged range — skip the anchor writes and label concat unless it moved.
    private void Refresh() {
        if(minIndex == appliedMin && maxIndex == appliedMax) return;
        appliedMin = minIndex;
        appliedMax = maxIndex;
        SetHandle(minHandle, minIndex);
        SetHandle(maxHandle, maxIndex);
        rangeLabel.text = labels[minIndex] + "  —  " + labels[maxIndex];
    }

    // Off hides the whole row (track and value label); only the toggle button remains
    // and the page collapses the row's space entirely.
    // Turning on fades the track in so it arrives with the layout slide instead of popping.
    private void RefreshActive() {
        bool wasActive = track.gameObject.activeSelf;
        track.gameObject.SetActive(active);
        if(toggleable) rangeLabel.gameObject.SetActive(active);
        if(active && !wasActive && trackCg != null) {
            trackFade?.Kill();
            trackCg.alpha = 0f;
            trackFade = trackCg.GTAlpha(1f, 0.16f).SetEasing(Easing.OutSine);
            MainCore.TC.Play(trackFade);
        }
        if(toggleBg != null) toggleBg.color = active ? UIColors.ObjectActive : UIColors.ObjectBG;
    }

    private void SetHandle(RectTransform handle, int index) {
        float x = (float)index / lastIndex;
        handle.anchorMin = handle.anchorMax = new(x, 0.5f);
        handle.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy() {
        trackFade?.Kill();
        if(gradientTex != null) Destroy(gradientTex);
    }

    private static string[] CompactNames(IReadOnlyList<string> names) {
        string[] compact = new string[names.Count];
        for(int i = 0; i < names.Count; i++) {
            int space = names[i].IndexOf(' ');
            compact[i] = space < 0 ? names[i] : names[i][..space];
        }
        return compact;
    }

    private static RawImage AddGradient(RectTransform parent, Color[] stops, out Texture2D texture) {
        RectTransform rect = MakeRect("Gradient", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        texture = BuildGradientTexture(256, stops);
        image.texture = texture;
        return image;
    }

    private static Texture2D BuildGradientTexture(int width, Color[] stops) {
        Texture2D texture = new(width, 1, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        float span = 1f / (stops.Length - 1);
        for(int x = 0; x < width; x++) {
            float t = (float)x / (width - 1);
            int index = Mathf.Min(Mathf.FloorToInt(t / span), stops.Length - 2);
            float local = (t - index * span) / span;
            texture.SetPixel(x, 0, Color.Lerp(stops[index], stops[index + 1], local));
        }
        texture.Apply();
        return texture;
    }

    private static RectTransform AddHandle(RectTransform parent, string name, Color color) {
        RectTransform rect = MakeRect(name + " Handle", parent, new(0f, 0.5f), new(0f, 0.5f), Vector2.zero, Vector2.zero);
        rect.sizeDelta = new(18f, 30f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text AddText(Transform parent, string text, float size, TextAlignmentOptions alignment) {
        TextMeshProUGUI label = GenerateUI.AddText(parent, true);
        label.text = text;
        label.font = FontManager.Current;
        label.fontSize = size;
        label.alignment = alignment;
        label.richText = false;
        return label;
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }
}
