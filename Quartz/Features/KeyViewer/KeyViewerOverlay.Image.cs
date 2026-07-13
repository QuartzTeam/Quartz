using Quartz.Core;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static readonly Dictionary<string, Texture2D> cssImages = new(StringComparer.Ordinal);
    private static readonly HashSet<string> cssImagePending = new(StringComparer.Ordinal);
    private static readonly object cssImageLock = new();
    private static Texture2D ResolveImage(string src) {
        if(string.IsNullOrWhiteSpace(src)) return null;
        string key = src.Trim();
        if(cssImages.TryGetValue(key, out Texture2D cached)) return cached; 
        try {
            if(key.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                int comma = key.IndexOf(',');
                int b64 = key.IndexOf("base64", StringComparison.OrdinalIgnoreCase);
                if(comma > 0 && b64 > 0 && b64 < comma)
                    return Cache(key, LoadTex(Convert.FromBase64String(key.Substring(comma + 1))));
                return Cache(key, null);
            }
            if(key.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                string path = ImageCachePath(key);
                if(File.Exists(path)) return Cache(key, LoadTex(File.ReadAllBytes(path)));
                StartImageDownload(key, path);
                return null; 
            }
            string file = key.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(key).LocalPath
                : key;
            if(IsLikelyLocalPath(file) && File.Exists(file)) return Cache(key, LoadTex(File.ReadAllBytes(file)));
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] CSS image load failed: " + ex.Message);
        }
        return Cache(key, null); 
    }
    private static bool IsLikelyLocalPath(string v) =>
        v.Length > 1 && (v[0] == '/' || v.StartsWith("\\\\", StringComparison.Ordinal)
            || (char.IsLetter(v[0]) && v[1] == ':'));
    private static Texture2D Cache(string key, Texture2D tex) {
        cssImages[key] = tex;
        return tex;
    }
    private static Texture2D LoadTex(byte[] bytes) {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        return tex.LoadImage(bytes) ? tex : null;
    }
    private static string ImageCachePath(string url) {
        string dir = Path.Combine(MainCore.Paths.RootPath, "CssImages");
        Directory.CreateDirectory(dir);
        string ext = Path.GetExtension(new Uri(url).AbsolutePath);
        if(ext.Length is < 2 or > 5) ext = ".png";
        return Path.Combine(dir, Hash(url) + ext);
    }
    private static void StartImageDownload(string url, string path) {
        lock(cssImageLock) { if(!cssImagePending.Add(url)) return; }
        StartCssDownload(url, path, "CSS image download failed",
            "QuartzCssImage", cssImageLock, cssImagePending, url);
    }
    private static void DisposeCssImageCache() {
        foreach(Texture2D tex in cssImages.Values)
            if(tex != null) UnityEngine.Object.Destroy(tex);
        cssImages.Clear();
    }
    private static void BuildKeyImage(Box box, DmNoteSpec spec) {
        if(!spec.HasImage || box.Fill == null) return;
        spec.IdleTex = ResolveImage(spec.InactiveImage);
        spec.ActiveTex = ResolveImage(spec.ActiveImage);
        if(spec.IdleTex == null && spec.ActiveTex == null) return;
        Mask mask = box.Fill.GetComponent<Mask>() ?? box.Fill.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        GameObject obj = new("KeyImage");
        obj.transform.SetParent(box.Fill.transform, false);
        RawImage ri = obj.AddComponent<RawImage>();
        ri.raycastTarget = false;
        obj.transform.SetAsFirstSibling(); 
        box.KeyImage = ri;
    }
    private static void BuildGraphImage(RectTransform parent, DmNoteSpec spec) {
        if(!spec.HasImage) return;
        Texture2D tex = ResolveImage(spec.InactiveImage) ?? ResolveImage(spec.ActiveImage);
        if(tex == null) return;
        GameObject obj = new("GraphImage");
        obj.transform.SetParent(parent, false);
        RawImage ri = obj.AddComponent<RawImage>();
        ri.raycastTarget = false;
        obj.transform.SetAsFirstSibling(); 
        string fit = spec.ImageFitDefault.Length > 0 ? spec.ImageFitDefault : "cover";
        ApplyImageFit(ri, tex, fit, spec.W, spec.H);
        ri.color = Color.white;
    }
    private static void ApplyImageState(Box box, DmNoteSpec spec, bool pressed) {
        if(box.KeyImage == null) return;
        bool usingActive = pressed && spec.ActiveTex != null;
        Texture2D tex = usingActive ? spec.ActiveTex : spec.IdleTex;
        if(tex == null) {
            box.KeyImage.enabled = false;
            return;
        }
        box.KeyImage.enabled = true;
        string fit = usingActive
            ? Pick(spec.ActiveImageFit, spec.ImageFitDefault)
            : Pick(spec.IdleImageFit, spec.ImageFitDefault);
        // Re-fitting rewrites the RectTransform + uvRect; only do it when the
        // texture or fit actually changed since the last press edge.
        if(!ReferenceEquals(tex, box.LastImageTex) || !string.Equals(fit, box.LastImageFit, StringComparison.Ordinal)) {
            ApplyImageFit(box.KeyImage, tex, fit, spec.W, spec.H);
            box.LastImageTex = tex;
            box.LastImageFit = fit;
        }
        bool dimmed = pressed && spec.ActiveTex == null && spec.IdleTex != null;
        box.KeyImage.color = dimmed ? new Color(0.62f, 0.62f, 0.62f, 1f) : Color.white;
    }
    private static string Pick(string specific, string fallback) =>
        specific.Length > 0 ? specific : fallback.Length > 0 ? fallback : "cover";
    private static void ApplyImageFit(RawImage ri, Texture2D tex, string fit, float rw, float rh) {
        ri.texture = tex;
        RectTransform rt = ri.rectTransform;
        float tw = Mathf.Max(tex.width, 1), th = Mathf.Max(tex.height, 1);
        switch(fit?.ToLowerInvariant()) {
            case "fill":
                Stretch(rt);
                ri.uvRect = new Rect(0f, 0f, 1f, 1f);
                break;
            case "contain": {
                float scale = Mathf.Min(rw / tw, rh / th);
                Center(rt, tw * scale, th * scale);
                ri.uvRect = new Rect(0f, 0f, 1f, 1f);
                break;
            }
            case "none":
                Center(rt, tw, th); 
                ri.uvRect = new Rect(0f, 0f, 1f, 1f);
                break;
            default: { 
                Stretch(rt);
                float ra = rw / rh, ta = tw / th;
                ri.uvRect = ta > ra
                    ? new Rect((1f - ra / ta) * 0.5f, 0f, ra / ta, 1f)
                    : new Rect(0f, (1f - ta / ra) * 0.5f, 1f, ta / ra);
                break;
            }
        }
    }
    private static void Stretch(RectTransform rt) {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
    }
    private static void Center(RectTransform rt, float w, float h) {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
        rt.localRotation = Quaternion.identity;
    }
}
