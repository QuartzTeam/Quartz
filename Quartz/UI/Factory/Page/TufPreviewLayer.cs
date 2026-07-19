using GTweens.Easings;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.Tween;
using UnityEngine;
using UnityEngine.UI;

namespace Quartz.UI.Factory.Page;

// Shared blurred-thumbnail layer for the TUF browser and pack views. Owns the preview
// slots for one view: builds the masked cover-image layer on a card, requests the
// texture from TufPreviewCache, and fades each in as it arrives. Keys are opaque
// strings ("512" for a level, "pack-RCAXIAv9" for a pack), so browser and pack views
// share the same cache entries and disk files.
internal sealed class TufPreviewGroup {
    private readonly Dictionary<string, Slot> slots = new(StringComparer.Ordinal);
    private volatile bool dirty;

    public TufPreviewGroup() => TufPreviewCache.Changed += OnReady;

    private void OnReady() => dirty = true;

    // Builds the preview layer behind `card`, which must already have its rounded bg
    // Image. No-op when the source has no usable thumbnail. Call right after the bg and
    // before the card's other children, so the preview renders behind them.
    public void Attach(RectTransform card, string key, TufPreviewSource source) {
        if(!source.HasThumbnail) return;
        // The card's own rounded bg is the mask stencil, clipping the preview to the
        // rounded corners. Only cards that get a preview pay for the mask.
        Mask mask = card.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        RectTransform host = Fill("Preview", card);
        CanvasGroup group = host.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        TufCoverImage image = host.gameObject.AddComponent<TufCoverImage>();
        image.raycastTarget = false;
        image.color = Color.white;
        // A dark scrim over the thumbnail keeps the title and metadata legible.
        Image scrim = Fill("Scrim", host).gameObject.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.5f);
        scrim.raycastTarget = false;
        slots[key] = new Slot { Image = image, Group = group };
        if(TufPreviewCache.TryGet(key, out Texture2D ready) && ready != null) Apply(key, ready);
        else TufPreviewCache.Request(key, source);
    }

    // Called from the view's Update; applies any textures that arrived since last tick.
    public void Tick() {
        if(!dirty) return;
        dirty = false;
        foreach(KeyValuePair<string, Slot> pair in slots) {
            if(pair.Value.Applied || pair.Value.Image == null) continue;
            if(TufPreviewCache.TryGet(pair.Key, out Texture2D tex) && tex != null) Apply(pair.Key, tex);
        }
    }

    private void Apply(string key, Texture2D texture) {
        if(!slots.TryGetValue(key, out Slot slot) || slot.Applied) return;
        if(slot.Image == null || slot.Group == null) return;
        // TufCoverImage recomputes its uvRect on every dimension change, so a rect that
        // is mid-relayout when this runs self-corrects instead of baking a bad crop.
        slot.Image.SetCover(texture);
        slot.Applied = true;
        slot.Fade?.Kill();
        slot.Fade = slot.Group.GTAlpha(1f, 0.28f).SetEasing(Easing.OutSine);
        MainCore.TC.Play(slot.Fade);
    }

    // Called before a full list rebuild tears down the cards.
    public void ClearSlots() {
        foreach(Slot slot in slots.Values) slot.Fade?.Kill();
        slots.Clear();
    }

    // Called from the view's OnDestroy.
    public void Dispose() {
        TufPreviewCache.Changed -= OnReady;
        ClearSlots();
    }

    private static RectTransform Fill(string name, Transform parent) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private sealed class Slot {
        public TufCoverImage Image;
        public CanvasGroup Group;
        public GTween Fade;
        public bool Applied;
    }
}

// A RawImage that keeps a scale-to-cover uvRect for its texture, recomputing it on
// every rect change. Computing the uvRect once at apply time smeared thumbnails into
// horizontal streaks when a card's rect was briefly mid-relayout (a fetch-more rebuild
// while scrolling): a rect measured narrower than it is tall collapsed the uvRect to a
// one-texel column. Tracking the live rect makes that transient a non-event — it is
// skipped, and the final layout pass fires one more dimension change with the real size.
internal sealed class TufCoverImage : RawImage {
    private Texture cover;

    public void SetCover(Texture texture) {
        cover = texture;
        this.texture = texture;
        Recompute();
    }

    protected override void OnRectTransformDimensionsChange() {
        base.OnRectTransformDimensionsChange();
        Recompute();
    }

    private void Recompute() {
        if(cover == null) return;
        Rect r = rectTransform.rect;
        // Cards are always much wider than tall; a rect measured otherwise (or not yet
        // sized) is a layout transient, so wait for the next dimension change.
        if(r.width < 1f || r.height < 1f || r.width < r.height) return;
        float tw = Mathf.Max(1, cover.width), th = Mathf.Max(1, cover.height);
        float ra = r.width / r.height, ta = tw / th;
        uvRect = ta > ra
            ? new Rect((1f - ra / ta) * 0.5f, 0f, ra / ta, 1f)
            : new Rect(0f, (1f - ta / ra) * 0.5f, 1f, ta / ra);
    }
}
