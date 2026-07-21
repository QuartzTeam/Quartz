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
public static partial class KeyViewerOverlay {
    public static SettingsFile<KeyViewerSettings> ConfMgr { get; private set; }
    public static KeyViewerSettings Conf => ConfMgr?.Data;
    private const float KeyW = 50f;
    private const float KeyH = 50f;
    private const float KeyGap = 4f;
    private const float RowGap = 54f;
    private const float KeyRadius = 8f;
    private const float BorderWidth = 2f;
    private const float KeyFontSize = 18f;
    private const float CounterFontSize = 14f;
    private const float StatFontSize = 16f;
    private const float CompactStatH = 30f;
    private const float FootKeyW = 30f;
    private const float FootKeyH = 30f;
    private const float FootKeyGap = 6f;
    private const float FootRowPitch = 40f;
    private const float FootGapAbove = 12f;
    private const float FootFontSize = 13f;
    private static readonly int[] BackSeq16 = [12, 13, 9, 8, 10, 11, 14, 15];
    private static GameObject canvasObj;
    private static GraphicRaycaster raycaster;
    private static RectTransform root;
    private static GameObject dragObj;
    private static readonly List<Box> boxes = [];
    private static readonly List<Box> counterBounces = [];
    private static bool built;
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
    private static float dmMinLitSeconds;
    private static readonly Dictionary<KeyCode, List<Box>> keyMap = new();
    private static readonly List<Box> pollBoxes = [];
    private static int uncoveredBindings;
    private static readonly List<KvInputQueue.Ev> drainBuffer = [];
    private static bool hookWasActive;
    private static bool resyncRequested;
    private static readonly Queue<float> pressLog = new(64);
    private static int kpsMax;
    private static int kpsSum;
    private static int kpsSamples;
    private static float nextKpsSample;
    private static int totalCount;
    private static bool countsDirty;
    private static float nextCountsSave;
    internal const float LayoutRebuildDebounceSeconds = 0.2f;
    private static bool layoutRebuildPending;
    private static float layoutRebuildAt;
    private static bool gameStateKnown;
    private static bool wasInGame;
    private static bool inputWasActive;
    private static bool inputPrimed;
    private sealed class Box {
        public KeyCode Key;
        public KeyCode GhostKey = KeyCode.None;
        public bool IsFoot;
        public Layout.KvElement Source;
        public bool CountInTotal = true;
        public bool PerKeyKps;
        public bool IsKps;
        public bool IsKpsAvg;
        public bool IsKpsMax;
        public bool IsTotal;
        public string Name;
        public Image Border;
        public float AppliedBorderStroke = -1f;
        public Image Fill;
        public Image Glow;
        public RawImage FillGrad;
        public RawImage BeforeLayer;
        public RawImage AfterLayer;
        public RawImage KeyImage;
        public Texture2D LastImageTex;
        public string LastImageFit;
        public string GradLabelText;
        public string GradValueText;
        public CssAnimGradient GradLabelApplied;
        public CssAnimGradient GradValueApplied;
        public float TransStart = -1f;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Value;
        public Material CounterStrokeMat;
        public char[] StatCaptionChars;
        public bool StatTogether;
        public char[] DmStatPrefix;
        public bool Pressed;
        public bool GhostPressed;
        public bool RawPressed;
        public float LitUntil;
        public bool HookCovered;
        public bool GhostHookCovered;
        public bool DisplayTargetPressed;
        public float DisplayTargetTime;
        public bool DelayedNotePending;
        public bool DelayedReleasedBeforeStart;
        public float DelayedDownTime;
        public float DelayedStartTime;
        public float DelayedReleaseTime;
        public int Count;
        public int LastShown = int.MinValue;
        public int Slot = -1;
        public readonly Queue<float> KpsLog = new();
        public int RainGroup;
        public float CenterX;
        public float BoxW;
        public float RainAlign;
        public RawRain LastRain;
        public RawRain LastGhostRain;
        public float BounceStart;
        public bool Bouncing;
        public Vector2 BounceBasePos;
        public DmNoteSpec Dm;
        public bool IsStat => IsKps || IsKpsAvg || IsKpsMax || IsTotal;
    }
    internal sealed class DmNoteSpec {
        public string KeyName;
        public string CountKey;
        public Layout.KvElement Source;
        public bool CountInTotal = true;
        public bool PerKeyKps;
        public KeyCode KeyCode;
        public KeyCode GhostKeyCode;
        public float X, Y, W, H;
        public float ZIndex;
        public string DisplayText;
        public bool LabelEnabled = true;
        public string PressedDisplayText = "";
        public bool CounterEnabled;
        public bool CounterShowWhilePressed = true;
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
        public bool RainGlowOn;
        public float RainGlowSize;
        public Color RainGlowTop, RainGlowBottom, GhostRainGlowTop, GhostRainGlowBottom;
        public bool RainShadowOn;
        public Color RainShadowColor;
        public float RainShadowX, RainShadowY;
        public Color NoteBorderColor;
        public float NoteBorderWidth;
        public int NoteBorderSide;
        public float NoteRadius;
        public TMPro.FontStyles LabelFontStyles, CounterFontStyles;
        public bool CounterAnimEnabled = true;
        public Vector4 CounterAnimBezier = new(0.25f, 0.46f, 0.45f, 0.94f);
        public float CounterAnimScale = 1.1f;
        public float CounterAnimDurationMs = 300f;
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
        public string ClassName = "";
        public bool Bold;
        public bool CounterBold;
        public float ActiveOffsetX, ActiveOffsetY;
        public float CounterStrokeWidth;
        public CssGlow LabelGlow, ActiveLabelGlow, CounterGlow, ActiveCounterGlow;
        public CssGlow BoxGlow, ActiveBoxGlow;
        public CssAnimGradient LabelGradient, ActiveLabelGradient;
        public CssAnimGradient CounterGradient, ActiveCounterGradient;
        public CssAnimGradient FillGradient, ActiveFillGradient;
        public Vector2 IdleOffset, ActiveOffset;
        public Vector2 IdleScale = Vector2.one, ActiveScale = Vector2.one;
        public float IdleRot, ActiveRot;
        public float TransitionSec;
        public TMP_FontAsset CssFont;
        public Color IdleFilter = Color.white, ActiveFilter = Color.white;
        public float IdleBackdrop, ActiveBackdrop;
        public CssLayerRt IdleBefore, ActiveBefore, IdleAfter, ActiveAfter;
        public bool IsGraph;
        public string GraphType = "line";
        public string GraphStat = "kps";
        public float GraphSpeed = 1000f;
        public Color GraphColor = new(0.525f, 0.937f, 0.678f, 1f);
        public bool GraphShowAvg = true;
        public bool GraphAnim = true;
        public Color GraphBg = new(17f / 255f, 17f / 255f, 20f / 255f, 0.9f);
        public Color GraphBorder = new(1f, 1f, 1f, 0.1f);
        public float GraphBorderWidth = 3f;
        public float GraphBorderRadius = 8f;
        public bool GraphInlineStyles;
        public bool HasStateTransform =>
            IdleOffset != Vector2.zero || ActiveOffset != Vector2.zero
            || IdleScale != Vector2.one || ActiveScale != Vector2.one
            || IdleRot != 0f || ActiveRot != 0f;
        public bool HasPseudo =>
            IdleBefore != null || ActiveBefore != null || IdleAfter != null || ActiveAfter != null;
        public string InactiveImage = "", ActiveImage = "";
        public string IdleImageFit = "", ActiveImageFit = "", ImageFitDefault = "";
        public Texture2D IdleTex, ActiveTex;
        public bool HasImage => InactiveImage.Length > 0 || ActiveImage.Length > 0;
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
    internal readonly struct CssGlow {
        public readonly bool On;
        public readonly float X, Y, Blur;
        public readonly Color Color;
        public CssGlow(float x, float y, float blur, Color color) {
            On = true; X = x; Y = y; Blur = blur; Color = color;
        }
    }
    internal sealed class CssAnimGradient {
        public Color[] Stops;
        public float Period;
        public float AngleDeg;
    }
    internal sealed class CssLayerRt {
        public Color[] GradStops;
        public float GradPeriod;
        public float GradAngle;
        public Color Bg = new(0f, 0f, 0f, 0f);
        public float Radius = -1f;
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
        Layout.KvMigration.RunOnce(Conf);
    }
    public static void Save() => ConfMgr?.RequestSave();
}
