using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.KeyViewer;
internal sealed class RawRain {
    public int Group;
    public float Order;
    public float StartTime;
    public float EndTime = -1f;
    public float AnchorX;
    public float Width;
    public float BaseY;
    public float TrackHeight;
    public float Speed;
    public float FadePx;
    public bool Reverse;
    public Color Color;
    public Color ColorTop;
    public Color ColorBottom;
    public float GlowSize;
    public Color GlowTop;
    public Color GlowBottom;
    public Color ShadowColor;
    public float ShadowX, ShadowY;
    public Color BorderColor;
    public float BorderWidth;
    public int BorderSide;
    public float CornerRadius;
    public bool Dotted;
    public float DotLength;
    public float GapLength;
    public void Reset() {
        Group = 0;
        Order = 0f;
        StartTime = 0f;
        EndTime = -1f;
        AnchorX = 0f;
        Width = 0f;
        BaseY = 0f;
        TrackHeight = 0f;
        Speed = 0f;
        FadePx = 0f;
        Reverse = false;
        Color = default;
        ColorTop = default;
        ColorBottom = default;
        GlowSize = 0f;
        GlowTop = default;
        GlowBottom = default;
        ShadowColor = default;
        ShadowX = 0f;
        ShadowY = 0f;
        BorderColor = default;
        BorderWidth = 0f;
        BorderSide = 0;
        CornerRadius = 0f;
        Dotted = false;
        DotLength = 0f;
        GapLength = 0f;
    }
}
internal sealed class RainGraphic : MaskableGraphic {
    private List<RawRain>[] groups;
    private float now;
    private static Texture2D roundTex;
    private static readonly Vector2 SolidUV = new(0.5f, 0.5f);
    public override Texture mainTexture {
        get {
            if(roundTex == null) roundTex = Resource.ProceduralTexture.Circle(48);
            return roundTex;
        }
    }
    public void SetSource(List<RawRain>[] source) {
        groups = source;
        SetVerticesDirty();
    }
    public void SetFrame(float frameTime) {
        now = frameTime;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        if(groups == null) return;
        Rect layer = rectTransform.rect;
        for(int g = 0; g < groups.Length; g++) {
            List<RawRain> active = groups[g];
            for(int i = 0; i < active.Count; i++) AddDrop(vh, layer, active[i]);
        }
    }
    private void AddDrop(VertexHelper vh, Rect layer, RawRain raw) {
        float lead = (now - raw.StartTime) * raw.Speed;
        float trail = raw.EndTime < 0f ? 0f : Mathf.Max(0f, (now - raw.EndTime) * raw.Speed);
        float dNear = trail;
        float dFar = Mathf.Min(lead, raw.TrackHeight);
        float height = dFar - dNear;
        if(height <= 0.5f || raw.Width <= 0.5f) return;
        float dropY = raw.Reverse ? raw.BaseY + raw.TrackHeight - dFar : raw.BaseY + dNear;
        float xMin = layer.xMin + raw.AnchorX - (raw.Width * 0.5f);
        float xMax = xMin + raw.Width;
        float yMin = layer.yMax + dropY;
        float yMax = yMin + height;
        if(raw.Dotted && raw.DotLength > 0.5f) {
            AddDottedDrop(vh, raw, dNear, dFar, xMin, xMax, yMin, height);
            return;
        }
        Color cMin = ColorForY(raw, dNear, dFar, yMin, yMin, height);
        Color cMax = ColorForY(raw, dNear, dFar, yMax, yMin, height);
        if(raw.ShadowColor.a > 0.001f) {
            EmitBody(vh, raw, dNear, dFar,
                xMin + raw.ShadowX, xMax + raw.ShadowX, yMin + raw.ShadowY, yMax + raw.ShadowY,
                yMin + raw.ShadowY, height, raw.ShadowColor, true);
        }
        EmitBody(vh, raw, dNear, dFar, xMin, xMax, yMin, yMax, yMin, height, default, false);
        EmitBorder(vh, raw, dNear, dFar, xMin, xMax, yMin, yMax, height);
        AddGlow(vh, raw, xMin, yMin, xMax, yMax, cMin, cMax);
    }
    private static void EmitBody(VertexHelper vh, RawRain raw, float dNear, float dFar,
        float xMin, float xMax, float yMin, float yMax, float yOrigin, float height,
        Color tint, bool tinted) {
        float r = Mathf.Min(raw.CornerRadius, Mathf.Min((xMax - xMin) * 0.5f, (yMax - yMin) * 0.5f));
        if(r <= 0.5f) {
            EmitSpan(vh, raw, dNear, dFar, xMin, xMax, yMin, yMax, yOrigin, height, tint, tinted);
            return;
        }
        float yBot = yMin + r;
        float yTop = yMax - r;
        Color c0 = BodyColor(raw, dNear, dFar, yMin, yOrigin, height, tint, tinted);
        Color c1 = BodyColor(raw, dNear, dFar, yBot, yOrigin, height, tint, tinted);
        Color c2 = BodyColor(raw, dNear, dFar, yTop, yOrigin, height, tint, tinted);
        Color c3 = BodyColor(raw, dNear, dFar, yMax, yOrigin, height, tint, tinted);
        AddQuadUV(vh, xMin, yMin, xMin + r, yBot, c0, c1, 0f, 0f, 0.5f, 0.5f);
        if(xMax - xMin > 2f * r) AddQuad(vh, xMin + r, yMin, xMax - r, yBot, c0, c1);
        AddQuadUV(vh, xMax - r, yMin, xMax, yBot, c0, c1, 0.5f, 0f, 1f, 0.5f);
        if(yTop > yBot)
            EmitSpan(vh, raw, dNear, dFar, xMin, xMax, yBot, yTop, yOrigin, height, tint, tinted);
        AddQuadUV(vh, xMin, yTop, xMin + r, yMax, c2, c3, 0f, 0.5f, 0.5f, 1f);
        if(xMax - xMin > 2f * r) AddQuad(vh, xMin + r, yTop, xMax - r, yMax, c2, c3);
        AddQuadUV(vh, xMax - r, yTop, xMax, yMax, c2, c3, 0.5f, 0.5f, 1f, 1f);
    }
    private static void EmitSpan(VertexHelper vh, RawRain raw, float dNear, float dFar,
        float xMin, float xMax, float yMin, float yMax, float yOrigin, float height,
        Color tint, bool tinted) {
        Color cMin = BodyColor(raw, dNear, dFar, yMin, yOrigin, height, tint, tinted);
        Color cMax = BodyColor(raw, dNear, dFar, yMax, yOrigin, height, tint, tinted);
        if(raw.FadePx > 0.5f && raw.TrackHeight > 0.5f) {
            float fadeStartD = raw.TrackHeight - raw.FadePx;
            float span = dFar - dNear;
            if(span > 0.0001f) {
                float tB = raw.Reverse
                    ? (fadeStartD - dFar) / (dNear - dFar)
                    : (fadeStartD - dNear) / span;
                float yMid = yOrigin + (tB * height);
                if(yMid > yMin + 0.01f && yMid < yMax - 0.01f) {
                    Color cMid = BodyColor(raw, dNear, dFar, yMid, yOrigin, height, tint, tinted);
                    AddQuad(vh, xMin, yMin, xMax, yMid, cMin, cMid);
                    AddQuad(vh, xMin, yMid, xMax, yMax, cMid, cMax);
                    return;
                }
            }
        }
        AddQuad(vh, xMin, yMin, xMax, yMax, cMin, cMax);
    }
    private static void EmitBorder(VertexHelper vh, RawRain raw, float dNear, float dFar,
        float xMin, float xMax, float yMin, float yMax, float height) {
        float bw = raw.BorderWidth;
        if(bw <= 0.01f || raw.BorderColor.a <= 0.001f) return;
        bw = Mathf.Min(bw, Mathf.Min((xMax - xMin) * 0.5f, (yMax - yMin) * 0.5f));
        float r = Mathf.Min(raw.CornerRadius, Mathf.Min((xMax - xMin) * 0.5f, (yMax - yMin) * 0.5f));
        float inset = Mathf.Max(r, 0f);
        Color bottom = BorderColor(raw, dNear, dFar, yMin, yMin, height);
        Color top = BorderColor(raw, dNear, dFar, yMax, yMin, height);
        bool vertical = raw.BorderSide != 2;
        bool horizontal = raw.BorderSide != 1;
        if(vertical) {
            float y0 = yMin + inset, y1 = yMax - inset;
            if(y1 > y0) {
                Color c0 = BorderColor(raw, dNear, dFar, y0, yMin, height);
                Color c1 = BorderColor(raw, dNear, dFar, y1, yMin, height);
                AddQuad(vh, xMin, y0, xMin + bw, y1, c0, c1);
                AddQuad(vh, xMax - bw, y0, xMax, y1, c0, c1);
            }
        }
        if(horizontal) {
            float x0 = xMin + inset, x1 = xMax - inset;
            if(x1 > x0) {
                Color bottomTopEdge = BorderColor(raw, dNear, dFar, yMin + bw, yMin, height);
                Color topBottomEdge = BorderColor(raw, dNear, dFar, yMax - bw, yMin, height);
                AddQuad(vh, x0, yMin, x1, yMin + bw, bottom, bottomTopEdge);
                AddQuad(vh, x0, yMax - bw, x1, yMax, topBottomEdge, top);
            }
        }
    }
    private static Color BodyColor(RawRain raw, float dNear, float dFar, float y, float yMin,
        float height, Color tint, bool tinted) {
        Color c = ColorForY(raw, dNear, dFar, y, yMin, height);
        if(!tinted) return c;
        return new Color(tint.r, tint.g, tint.b, tint.a * c.a);
    }
    private static Color BorderColor(RawRain raw, float dNear, float dFar, float y, float yMin, float height) {
        float t = height <= 0.0001f ? 0f : (y - yMin) / height;
        float d = raw.Reverse ? Mathf.Lerp(dFar, dNear, t) : Mathf.Lerp(dNear, dFar, t);
        float alpha = (raw.FadePx > 0.5f && raw.TrackHeight > 0.5f)
            ? AlphaAtD(d, raw.TrackHeight - raw.FadePx, raw.TrackHeight, raw.FadePx)
            : 1f;
        return new Color(raw.BorderColor.r, raw.BorderColor.g, raw.BorderColor.b, raw.BorderColor.a * alpha);
    }
    private static void AddDottedDrop(VertexHelper vh, RawRain raw, float dNear, float dFar, float xMin, float xMax, float yMin, float height) {
        float span = dFar - dNear;
        if(span <= 0.0001f) return;
        float period = Mathf.Max(1f, raw.DotLength + raw.GapLength);
        float dotLength = raw.DotLength;
        int kStart = Mathf.FloorToInt(dNear / period);
        int kEnd = Mathf.CeilToInt(dFar / period);
        const int MaxSegments = 256;
        if(kEnd - kStart > MaxSegments) {
            float scale = (kEnd - kStart) / (float)MaxSegments;
            period *= scale;
            dotLength *= scale;
            kStart = Mathf.FloorToInt(dNear / period);
            kEnd = Mathf.CeilToInt(dFar / period);
        }
        for(int k = kStart; k <= kEnd; k++) {
            float segStart = Mathf.Max(dNear, k * period);
            float segEnd = Mathf.Min(dFar, k * period + dotLength);
            if(segEnd <= segStart) continue;
            float tA = raw.Reverse ? (dFar - segStart) / span : (segStart - dNear) / span;
            float tB = raw.Reverse ? (dFar - segEnd) / span : (segEnd - dNear) / span;
            float yA = yMin + tA * height;
            float yB = yMin + tB * height;
            float ySegMin = Mathf.Min(yA, yB);
            float ySegMax = Mathf.Max(yA, yB);
            Color cA = ColorForY(raw, dNear, dFar, ySegMin, yMin, height);
            Color cB = ColorForY(raw, dNear, dFar, ySegMax, yMin, height);
            if(raw.ShadowColor.a > 0.001f) {
                AddQuad(vh, xMin + raw.ShadowX, ySegMin + raw.ShadowY, xMax + raw.ShadowX, ySegMax + raw.ShadowY,
                    Tint(raw.ShadowColor, cA.a), Tint(raw.ShadowColor, cB.a));
            }
            AddQuad(vh, xMin, ySegMin, xMax, ySegMax, cA, cB);
            AddGlow(vh, raw, xMin, ySegMin, xMax, ySegMax, cA, cB);
        }
    }
    private static void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color bottom, Color top) {
        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.uv0 = SolidUV;
        v.position = new Vector3(xMin, yMin, 0f); v.color = bottom; vh.AddVert(v);
        v.position = new Vector3(xMax, yMin, 0f); v.color = bottom; vh.AddVert(v);
        v.position = new Vector3(xMax, yMax, 0f); v.color = top; vh.AddVert(v);
        v.position = new Vector3(xMin, yMax, 0f); v.color = top; vh.AddVert(v);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
    private static void AddQuadUV(VertexHelper vh, float xMin, float yMin, float xMax, float yMax,
        Color bottom, Color top, float u0, float v0, float u1, float v1) {
        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.position = new Vector3(xMin, yMin, 0f); v.color = bottom; v.uv0 = new Vector2(u0, v0); vh.AddVert(v);
        v.position = new Vector3(xMax, yMin, 0f); v.color = bottom; v.uv0 = new Vector2(u1, v0); vh.AddVert(v);
        v.position = new Vector3(xMax, yMax, 0f); v.color = top; v.uv0 = new Vector2(u1, v1); vh.AddVert(v);
        v.position = new Vector3(xMin, yMax, 0f); v.color = top; v.uv0 = new Vector2(u0, v1); vh.AddVert(v);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
    private static void AddQuad4(VertexHelper vh, float xMin, float yMin, float xMax, float yMax,
        Color bl, Color br, Color tr, Color tl) {
        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.uv0 = SolidUV;
        v.position = new Vector3(xMin, yMin, 0f); v.color = bl; vh.AddVert(v);
        v.position = new Vector3(xMax, yMin, 0f); v.color = br; vh.AddVert(v);
        v.position = new Vector3(xMax, yMax, 0f); v.color = tr; vh.AddVert(v);
        v.position = new Vector3(xMin, yMax, 0f); v.color = tl; vh.AddVert(v);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
    private static void AddGlow(VertexHelper vh, RawRain raw, float xMin, float yMin, float xMax, float yMax, Color cMin, Color cMax) {
        float g = raw.GlowSize;
        if(g <= 0.5f) return;
        Color glowBottom = new(raw.GlowBottom.r, raw.GlowBottom.g, raw.GlowBottom.b, raw.GlowBottom.a * cMin.a);
        Color glowTop = new(raw.GlowTop.r, raw.GlowTop.g, raw.GlowTop.b, raw.GlowTop.a * cMax.a);
        Color zeroBottom = new(glowBottom.r, glowBottom.g, glowBottom.b, 0f);
        Color zeroTop = new(glowTop.r, glowTop.g, glowTop.b, 0f);
        AddQuad4(vh, xMin - g, yMin, xMin, yMax, zeroBottom, glowBottom, glowTop, zeroTop);
        AddQuad4(vh, xMax, yMin, xMax + g, yMax, glowBottom, zeroBottom, zeroTop, glowTop);
        AddQuad4(vh, xMin, yMax, xMax, yMax + g, glowTop, glowTop, zeroTop, zeroTop);
        AddQuad4(vh, xMin, yMin - g, xMax, yMin, zeroBottom, zeroBottom, glowBottom, glowBottom);
        AddQuad4(vh, xMin - g, yMin - g, xMin, yMin, zeroBottom, zeroBottom, glowBottom, zeroBottom);
        AddQuad4(vh, xMax, yMin - g, xMax + g, yMin, zeroBottom, zeroBottom, zeroBottom, glowBottom);
        AddQuad4(vh, xMin - g, yMax, xMin, yMax + g, zeroTop, glowTop, zeroTop, zeroTop);
        AddQuad4(vh, xMax, yMax, xMax + g, yMax + g, glowTop, zeroTop, zeroTop, zeroTop);
    }
    private static Color ColorForY(RawRain raw, float dNear, float dFar, float y, float yMin, float height) {
        float t = height <= 0.0001f ? 0f : (y - yMin) / height;
        float d = raw.Reverse
            ? Mathf.Lerp(dFar, dNear, t)
            : Mathf.Lerp(dNear, dFar, t);
        float alpha = (raw.FadePx > 0.5f && raw.TrackHeight > 0.5f)
            ? AlphaAtD(d, raw.TrackHeight - raw.FadePx, raw.TrackHeight, raw.FadePx)
            : 1f;
        return ColorAtD(raw, d, alpha);
    }
    private static Color ColorAtD(RawRain raw, float d, float alphaMul) {
        float t = raw.TrackHeight <= 0.0001f ? 0f : Mathf.Clamp01(d / raw.TrackHeight);
        Color c = Color.Lerp(raw.ColorBottom, raw.ColorTop, t);
        c.a *= alphaMul;
        return c;
    }
    private static float AlphaAtD(float d, float fadeStartD, float trackH, float fade) {
        if(d <= fadeStartD) return 1f;
        if(d >= trackH) return 0f;
        return (trackH - d) / fade;
    }
    private static Color Tint(Color tint, float alpha) =>
        new(tint.r, tint.g, tint.b, tint.a * alpha);
}
internal sealed class RainManager : MonoBehaviour {
    private RainGraphic graphic;
    private readonly List<RawRain>[] groups = [new(64), new(64), new(64)];
    private readonly Queue<RawRain> pending = new(64);
    private readonly Stack<RawRain> pool = new(64);
    private const int PoolCap = 256;
    public RawRain Rent() {
        if(pool.Count == 0) return new RawRain();
        RawRain raw = pool.Pop();
        raw.Reset();
        return raw;
    }
    public void SetLayer(RectTransform value) {
        pending.Clear();
        for(int i = 0; i < groups.Length; i++) groups[i].Clear();
        if(graphic != null) {
            Destroy(graphic.gameObject);
            graphic = null;
        }
        if(value == null) return;
        GameObject obj = new("RainDrops");
        obj.transform.SetParent(value, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        graphic = obj.AddComponent<RainGraphic>();
        graphic.raycastTarget = false;
        graphic.color = Color.white;
        graphic.SetSource(groups);
    }
    public void Enqueue(RawRain raw) {
        if(raw != null) pending.Enqueue(raw);
    }
    public void Clear() {
        pending.Clear();
        for(int i = 0; i < groups.Length; i++) groups[i].Clear();
        if(graphic != null) graphic.SetVerticesDirty();
    }
    private void Update() {
        if(graphic == null) {
            pending.Clear();
            return;
        }
        bool dirty = pending.Count > 0;
        while(pending.Count > 0) {
            RawRain raw = pending.Dequeue();
            List<RawRain> group = groups[Mathf.Clamp(raw.Group, 1, 3) - 1];
            int at = group.Count;
            for(int i = 0; i < group.Count; i++) {
                if(group[i].Order > raw.Order) {
                    at = i;
                    break;
                }
            }
            group.Insert(at, raw);
        }
        float now = KvClock.Now;
        for(int g = 0; g < groups.Length; g++) {
            List<RawRain> active = groups[g];
            int write = 0;
            for(int read = 0; read < active.Count; read++) {
                RawRain raw = active[read];
                float trail = raw.EndTime < 0f ? 0f : Mathf.Max(0f, (now - raw.EndTime) * raw.Speed);
                if(trail <= raw.TrackHeight + 8f) {
                    if(write != read) active[write] = raw;
                    write++;
                    if(raw.EndTime >= 0f || (now - raw.StartTime) * raw.Speed < raw.TrackHeight) dirty = true;
                    continue;
                }
                dirty = true;
                if(pool.Count < PoolCap) pool.Push(raw);
            }
            if(write < active.Count) active.RemoveRange(write, active.Count - write);
        }
        if(dirty) graphic.SetFrame(now);
    }
}
