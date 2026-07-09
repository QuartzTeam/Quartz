using System.Globalization;
using Quartz.Core;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

using TMPro;

namespace Quartz.Features.KeyViewer;

// Key viewer overlay — a port of v1's "simple" key viewer: a fixed grid of
// key boxes (10/12/16/20-key styles) with per-key press counters plus KPS and
// Total stat boxes. Layout constants and defaults come from v1's
// SimplePresets (50px keys, 4px gap, 54px row pitch, 8-column grid).
// Draggable in Reorganize mode like the other HUD elements.
//
// v1 features not ported yet: rain / ghost rain, foot keys, label overrides,
// key rebinding UI, key-limiter sync.
//
// Split across partials:
//   Lifecycle   Initialize/Rebuild/Apply + KeyLimiter sync + import/reset/dispose
//   Layout      Slot geometry per style (BuildLayout/GridSize/BuildFoot)
//   Boxes       AddKey/AddStat/AddFootKey + NewBoxVisual/NewText + label resolution
//   DmNote      BuildDmNote + per-frame UpdateDmNote + delayed-note scheduling
//   Rain        SpawnRain/SpawnDmRain
//   Input       KeyHeld + NumpadNavTwin + focus-gated input-state machine
//   Counts      Alloc-free integer→TMP counter writes
//   Events      KeyPressChangedEventArgs + event + ApplyBoxColors
//   Update      Updater MonoBehaviour (Update + LateUpdate)
//   Css         CSS animation / glow / gradient / image rendering
//   DmNoteLayout  Per-box visuals (one box per DmNoteSpec)
//   DmNoteParsing Preset JSON → DmNoteSpec[]
//   Graph       KPS-graph element
//   Image       Per-state background images
public static partial class KeyViewerOverlay {
    public static SettingsFile<KeyViewerSettings> ConfMgr { get; private set; }
    public static KeyViewerSettings Conf => ConfMgr?.Data;

    // v1 SimplePresets constants.
    private const float KeyW = 50f;
    private const float KeyH = 50f;
    private const float KeyGap = 4f;
    private const float RowGap = 54f;
    private const float KeyRadius = 8f;
    private const float BorderWidth = 2f;
    private const float KeyFontSize = 18f;
    private const float CounterFontSize = 14f;
    private const float StatFontSize = 16f;
    // Stat-box height on the styles whose KPS/Total row has no paired key row
    // (styles 2 and 4): shorter than a full KeyH key box.
    private const float CompactStatH = 30f;
    // Foot keys: smaller boxes on their own row(s) below the main grid (v1
    // FootKeyviewerStyle: 30px boxes, font 13, no rain or counter).
    private const float FootKeyW = 30f;
    private const float FootKeyH = 30f;
    private const float FootKeyGap = 6f;
    private const float FootRowPitch = 40f;
    private const float FootGapAbove = 12f;
    private const float FootFontSize = 13f;
    // Second-row slot order for the 16/20-key styles (v1 BackSeq16).
    private static readonly int[] BackSeq16 = [12, 13, 9, 8, 10, 11, 14, 15];

    private static GameObject canvasObj;
    private static GraphicRaycaster raycaster;
    private static RectTransform root;
    private static GameObject dragObj;
    // Foot keys are a separate, independently-draggable element (own root +
    // reorganize handle), so they never move or resize the main grid.
    private static RectTransform footRoot;
    private static GameObject footDragObj;
    private static readonly List<Box> boxes = [];
    private static int builtStyle = -1;
    private static string builtMode;
    private static RainManager rainManager;
    private static float dmCanvasHeight = 250f;
    private static float dmCanvasWidth = 800f;
    private static float dmTrackHeight = 200f;
    private static float dmNoteSpeed = 1000f;
    private static bool dmNoteReverse;
    private static float dmFadePx = 60f;
    private static bool dmDelayedNoteEnabled;
    private static float dmShortNoteThresholdMs = 50f;
    private static float dmShortNoteMinLengthPx = 30f;
    private static float dmKeyDisplayDelayMs;

    // KPS = presses in the last second, same as v1's press log.
    private static readonly Queue<float> pressLog = new(64);
    private static int kpsMax;
    private static int kpsSum;
    private static int kpsSamples;
    private static float nextKpsSample;
    private static int totalCount;
    private static bool countsDirty;
    private static float nextCountsSave;
    private static bool inputWasActive;
    private static bool inputPrimed;

    private sealed class Box {
        public KeyCode Key;
        public KeyCode GhostKey = KeyCode.None;
        public bool IsFoot;
        public bool IsKps;
        public bool IsKpsAvg;
        public bool IsKpsMax;
        public bool IsTotal;
        public string Name;
        public Image Border;
        public Image Fill;
        // Optional soft sprite behind the box for a CSS box-shadow halo.
        public Image Glow;
        // CSS extras: a masked gradient fill child, the :before/:after layers,
        // and the per-state background image.
        public RawImage FillGrad;
        public RawImage BeforeLayer;
        public RawImage AfterLayer;
        public RawImage KeyImage;
        // Last text the per-glyph gradient coloured, so the mesh is only forced
        // to rebuild when the string actually changes. Nulled wherever the mesh
        // colours get wiped (text write, press flip, font swap) so CssTick
        // re-applies a static gradient exactly once.
        public string GradLabelText;
        public string GradValueText;
        // Last gradient object the per-glyph pass applied (per text); a static
        // gradient re-uploads only when this or the text tracker changes.
        public CssAnimGradient GradLabelApplied;
        public CssAnimGradient GradValueApplied;
        // transition: timestamp the state flip started (<0 = settled).
        public float TransStart = -1f;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Value;
        // Simple-mode KPS/Total stat boxes: when StatTogether, the caption and
        // value render centred together in the Value text ("KPS  0") and Label
        // is hidden; otherwise the caption sits left and the value right. The
        // caption is pre-baked as "caption + two spaces" chars for the
        // alloc-free counter write.
        public char[] StatCaptionChars;
        public bool StatTogether;
        // DM inline-stat prefix ("KPS" + two spaces) chars, same purpose.
        public char[] DmStatPrefix;
        public bool Pressed;
        public bool GhostPressed;
        public bool RawPressed;
        public bool DisplayTargetPressed;
        public float DisplayTargetTime;
        public bool DelayedNotePending;
        public bool DelayedReleasedBeforeStart;
        public float DelayedDownTime;
        public float DelayedStartTime;
        public float DelayedReleaseTime;
        public int Count;
        public int LastShown = int.MinValue;
        // Flat per-key slot (0-19 main, 20-35 foot) for per-key colour/font
        // lookups; -1 for the stat boxes.
        public int Slot = -1;
        // Per-key press timestamps for the optional per-key KPS counter; only
        // filled for the key boxes (not stats).
        public readonly Queue<float> KpsLog = new();

        // Rain spawn parameters: color group (1 = front row, 2 = back row,
        // 3 = the 20-key style's third row, 0 = no rain) and the box span.
        public int RainGroup;
        public float CenterX;
        public float BoxW;
        // Horizontal rain alignment within the box: -1 = left edge, 0 = center,
        // +1 = right edge. Only matters when the rain is narrower than the box
        // (a wide key); single keys leave it 0 (centered).
        public float RainAlign;
        public RawRain LastRain;
        public RawRain LastGhostRain;
        public DmNoteSpec Dm;

        public bool IsStat => IsKps || IsKpsAvg || IsKpsMax || IsTotal;
    }

    private sealed class DmNoteSpec {
        public string KeyName;
        public string CountKey;
        public KeyCode KeyCode;
        public KeyCode GhostKeyCode;
        public float X, Y, W, H;
        // DmNote stacking order (the position's zIndex, default = the element's
        // index within its own preset array, matching OverlayScene's
        // `pos.zIndex ?? index`). Drives box sibling order and note draw order.
        public float ZIndex;
        public string DisplayText;
        public bool CounterEnabled;
        public bool InlineStatCounter;
        public bool NoteEnabled;
        public bool IsStat;
        public bool IsKps;
        public bool IsKpsAvg;
        public bool IsKpsMax;
        public bool IsTotal;
        public string CounterAlign;
        public string CounterAlignMode;
        public float CounterGap;
        public int FontSize;
        public int CounterFontSize;
        public Color Bg, ActiveBg, Outline, ActiveOutline, Text, ActiveText;
        public Color CounterText, ActiveCounterText, Rain, GhostRain;
        public Color CounterStroke, ActiveCounterStroke;
        public Color RainTop, RainBottom, GhostRainTop, GhostRainBottom;
        // DmNote's per-note glow (noteGlowEnabled/Size/Opacity/Color): a soft
        // halo around the falling rain, independent colour + opacity from the
        // rain itself, defaulting to the rain's own colour when unset.
        public bool RainGlowOn;
        public float RainGlowSize;
        public Color RainGlowTop, RainGlowBottom, GhostRainGlowTop, GhostRainGlowBottom;
        public float BorderRadius = KeyRadius;
        public float BoxBorderWidth = KeyViewerOverlay.BorderWidth;
        public bool CounterOutside;
        public bool NoteAutoYCorrection = true;
        public string NoteAlignment = "center";
        public float NoteW;
        public float NoteOffsetX;
        public float NoteOffsetY;
        public float TrackX;
        public float TrackBottomY;

        // Custom-CSS layer (KeyViewerStylesheet). ClassName is the preset key's
        // assigned CSS class (".blue"); the rest are filled by ApplyCssToSpec
        // when a stylesheet is active and override the preset values above.
        public string ClassName = "";
        public bool Bold;
        public bool CounterBold;
        public float ActiveOffsetX, ActiveOffsetY;
        public float CounterStrokeWidth;
        // Text glow (CSS text-shadow) per state, on the label and the counter.
        public CssGlow LabelGlow, ActiveLabelGlow, CounterGlow, ActiveCounterGlow;
        // Box-shadow halo per state (color + blur drive a soft sprite behind).
        public CssGlow BoxGlow, ActiveBoxGlow;
        // Animated gradients (CSS linear-gradient + animation) for the label /
        // counter text and the box fill. Text/counter gradients paint per glyph
        // via TMP vertex colours; the fill gradient drives a masked child image.
        public CssAnimGradient LabelGradient, ActiveLabelGradient;
        public CssAnimGradient CounterGradient, ActiveCounterGradient;
        public CssAnimGradient FillGradient, ActiveFillGradient;

        // transform: scale()/rotate() per state (translate folds into the offsets
        // below). transition: tween duration. @font-face / font-family resolved.
        public Vector2 IdleOffset, ActiveOffset;
        public Vector2 IdleScale = Vector2.one, ActiveScale = Vector2.one;
        public float IdleRot, ActiveRot;
        public float TransitionSec;
        public TMP_FontAsset CssFont;
        // filter: brightness()/contrast() fold into a colour multiply; saturate()
        // is applied to the resolved colours. 1 / white = identity.
        public Color IdleFilter = Color.white, ActiveFilter = Color.white;
        // backdrop-filter: blur() — approximated as a frosted fill (no true
        // scene blur is possible from a ScreenSpaceOverlay canvas).
        public float IdleBackdrop, ActiveBackdrop;
        // :before / :after pseudo layers per state.
        public CssLayerRt IdleBefore, ActiveBefore, IdleAfter, ActiveAfter;

        // KPS-graph element (DM Note GraphPanel). When IsGraph the box renders a
        // line/bar chart of the stat history instead of a key/counter. Defaults
        // mirror DmNote's GraphPanel.
        public bool IsGraph;
        public string GraphType = "line";      // "line" | "bar"
        public string GraphStat = "kps";        // which stat to plot
        public float GraphSpeed = 1000f;         // window in ms (clamped 500..5000)
        public Color GraphColor = new(0.525f, 0.937f, 0.678f, 1f);  // #86EFAC
        public bool GraphShowAvg = true;
        public bool GraphAnim = true;
        public Color GraphBg = new(17f / 255f, 17f / 255f, 20f / 255f, 0.9f);
        public Color GraphBorder = new(1f, 1f, 1f, 0.1f);
        public float GraphBorderWidth = 3f;
        public float GraphBorderRadius = 8f;
        // DM Note's "Inline Styles Priority": when true the preset's inline
        // colours win and --graph-* CSS is ignored.
        public bool GraphInlineStyles;

        public bool HasPseudo =>
            IdleBefore != null || ActiveBefore != null || IdleAfter != null || ActiveAfter != null;

        // Background images (DM Note inactiveImage/activeImage + object-fit). Held
        // as raw source strings (data URI / URL / file path); resolved to textures
        // in BuildKeyImage. Fit precedence mirrors useKeyElementStyles.
        public string InactiveImage = "", ActiveImage = "";
        public string IdleImageFit = "", ActiveImageFit = "", ImageFitDefault = "";
        public Texture2D IdleTex, ActiveTex;
        public bool HasImage => InactiveImage.Length > 0 || ActiveImage.Length > 0;

        // Whether ApplyCssState has per-press work: glow, offset, transform,
        // filter, backdrop or pseudo layers. Gradients tick separately.
        public bool NeedsCssState =>
            BoxGlow.On || ActiveBoxGlow.On || LabelGlow.On || ActiveLabelGlow.On
            || CounterGlow.On || ActiveCounterGlow.On || CounterStrokeWidth > 0.01f
            || ActiveOffsetX != 0f || ActiveOffsetY != 0f
            || IdleOffset != Vector2.zero || ActiveOffset != Vector2.zero
            || IdleScale != Vector2.one || ActiveScale != Vector2.one
            || IdleRot != 0f || ActiveRot != 0f
            || IdleFilter != Color.white || ActiveFilter != Color.white
            || IdleBackdrop > 0f || ActiveBackdrop > 0f
            || FillGradient != null || ActiveFillGradient != null
            || HasImage || HasPseudo;
    }

    // A resolved glow (Unity colour + blur) ready to feed TMPTextShadow or the
    // box-halo sprite. Default On=false.
    internal readonly struct CssGlow {
        public readonly bool On;
        public readonly float X, Y, Blur;
        public readonly Color Color;
        public CssGlow(float x, float y, float blur, Color color) {
            On = true; X = x; Y = y; Blur = blur; Color = color;
        }
    }

    // A gradient resolved to Unity colours plus its scroll period and axis angle.
    // Text/counter gradients are sampled per glyph; the fill gradient is baked to
    // a cached texture. Period <= 0 = static.
    internal sealed class CssAnimGradient {
        public Color[] Stops;
        public float Period;     // seconds for a full scroll; <=0 = static
        public float AngleDeg;   // CSS angle (0 = up, 90 = right, 180 = down)
    }

    // A resolved :before / :after pseudo layer. Rendered as a child Image behind
    // (Z<0) or over (Z>=0) the box, optionally with a scrolling gradient texture.
    internal sealed class CssLayerRt {
        public Color[] GradStops;   // null = solid Bg
        public float GradPeriod;
        public float GradAngle;
        public Color Bg = new(0f, 0f, 0f, 0f);
        public float Radius = -1f;  // <0 = inherit the box radius
        public float InsetT, InsetR, InsetB, InsetL;
        public float Blur;
        public int Z;
        public bool HasGradient => GradStops != null && GradStops.Length > 0;
    }

    public static void EnsureConf() {
        if(ConfMgr != null) return;

        ConfMgr = new SettingsFile<KeyViewerSettings>(
            Path.Combine(MainCore.Paths.RootPath, "KeyViewer.json")
        );
        ConfMgr.Load();
    }

    public static void Save() => ConfMgr?.RequestSave();
}