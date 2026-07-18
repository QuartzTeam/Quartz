using Quartz.Core;
using Quartz.Features.EffectRemover;
using Quartz.Features.Interop;
using Quartz.Features.Judgement;
using Quartz.Features.OttoIcon;
using Quartz.Features.PlanetColors;
using Quartz.Features.Tweaks;
using Quartz.Features.UiHider;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageVisuals {
    private static void CreateEffectRemover(RectTransform content) {
        EffectRemover.EnsureConf();
        EffectRemoverSettings conf = EffectRemover.Conf;
        EffectRemoverSettings def = new();
        void Save() => EffectRemover.Save();
        var sec = GenerateUI.FlatSection(
            content.transform, "Effect Remover",
            v => {
                conf.On = v;
                EffectRemover.RefreshEditorSaveButtons();
                Save();
            },
            conf.On,
            "Enable Effect Remover", "effectremover_enable"
        );
        GenerateUI.DropDown(
            GenerateUI.Row(sec.Body),
            EffectRemoverSettings.ModeEnhanced,
            conf.Mode,
            new[] { EffectRemoverSettings.ModeSimple, EffectRemoverSettings.ModeEnhanced },
            m => MainCore.Tr.Get(
                m == EffectRemoverSettings.ModeSimple ? "FXRM_MODE_SIMPLE" : "FXRM_MODE_ENHANCED",
                m == EffectRemoverSettings.ModeSimple ? "Simple" : "Enhanced"),
            v => {
                conf.Mode = v;
                Save();
                EffectRemover.RefreshEditorSaveButtons();
                UICore.Rebuild();
            },
            "fxrm_mode",
            260f,
            "Mode"
        );
        if(conf.IsSimple) {
            CreateSimpleEffectRemover(sec.Body, conf, def);
        } else {
        GenerateUI.ToggleTip(
            sec.Body,
            def.EnableSave,
            conf.EnableSave,
            v => {
                conf.EnableSave = v;
                EffectRemover.RefreshEditorSaveButtons();
                Save();
            },
            "Allow Saving in Editor",
            "fxrm_enable_save",
            "The editor holds the stripped chart, so saving overwrites the file and the removed effects are gone for good. Off blocks the editor's save button while Enhanced is on."
        );
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_NON_DLC_EVENTS", "Non-DLC Events");
        RectTransform removeAllRow = null;
        RectTransform setZoomRow = null;
        RectTransform zoomSliderRow = null;
        RectTransform resetAnimRow = null;
        RectTransform resetColorRow = null;
        RectTransform tutorialPatternsRow = null;
        GenerateUI.CollapsibleSection decoTypesSection = null;
        void RefreshConditionalRows() {
            removeAllRow?.gameObject.SetActive(conf.Decorations);
            decoTypesSection?.Section.gameObject.SetActive(conf.Decorations);
            setZoomRow?.gameObject.SetActive(conf.Cameras);
            zoomSliderRow?.gameObject.SetActive(conf.Cameras && conf.SetCameraZoom);
            resetAnimRow?.gameObject.SetActive(conf.TrackAnimations);
            resetColorRow?.gameObject.SetActive(conf.TrackColors);
            tutorialPatternsRow?.gameObject.SetActive(conf.Backgrounds);
        }
        void SimpleToggle(Transform body, bool defVal, bool val, System.Action<bool> set, string label, string id) {
            GenerateUI.Toggle(
                GenerateUI.Row(body),
                defVal,
                val,
                v => {
                    set(v);
                    RefreshConditionalRows();
                    Save();
                },
                label,
                id
            );
        }
        SimpleToggle(sec.Body, def.Filters, conf.Filters, v => conf.Filters = v, "Filter", "fxrm_filters");
        SimpleToggle(sec.Body, def.AdvancedFilters, conf.AdvancedFilters, v => conf.AdvancedFilters = v, "Advanced Filter", "fxrm_advfilters");
        SimpleToggle(sec.Body, def.Decorations, conf.Decorations, v => conf.Decorations = v, "Decoration", "fxrm_decorations");
        SimpleToggle(sec.Body, def.Backgrounds, conf.Backgrounds, v => conf.Backgrounds = v, "Background", "fxrm_backgrounds");
        SimpleToggle(sec.Body, def.Cameras, conf.Cameras, v => conf.Cameras = v, "Camera", "fxrm_cameras");
        SimpleToggle(sec.Body, def.RepeatEvents, conf.RepeatEvents, v => conf.RepeatEvents = v, "Repeat Event", "fxrm_repeat");
        SimpleToggle(sec.Body, def.FrameRate, conf.FrameRate, v => conf.FrameRate = v, "Frame Rate", "fxrm_framerate");
        SimpleToggle(sec.Body, def.HitSounds, conf.HitSounds, v => conf.HitSounds = v, "HitSound", "fxrm_hitsounds");
        {
            var planet = GenerateUI.Collapsible(sec.Body, "Planet Events", startExpanded: false);
            UIToggle orbit = null, scale = null, radius = null;
            GenerateUI.Button(
                GenerateUI.Row(planet.Body),
                () => {
                    if(orbit == null || scale == null || radius == null) return;
                    bool value = !conf.PlanetOrbit && !conf.PlanetScale && !conf.PlanetRadius;
                    orbit.Set(value);
                    scale.Set(value);
                    radius.Set(value);
                },
                "Toggle All",
                "fxrm_planet_all"
            ).SetSecondary();
            orbit = GenerateUI.Toggle(
                GenerateUI.Row(planet.Body), def.PlanetOrbit, conf.PlanetOrbit,
                v => { conf.PlanetOrbit = v; Save(); }, "Planet Orbit", "fxrm_planet_orbit");
            scale = GenerateUI.Toggle(
                GenerateUI.Row(planet.Body), def.PlanetScale, conf.PlanetScale,
                v => { conf.PlanetScale = v; Save(); }, "Planet Scale", "fxrm_planet_scale");
            radius = GenerateUI.Toggle(
                GenerateUI.Row(planet.Body), def.PlanetRadius, conf.PlanetRadius,
                v => { conf.PlanetRadius = v; Save(); }, "Planet Radius", "fxrm_planet_radius");
        }
        {
            var track = GenerateUI.Collapsible(sec.Body, "Track Events", startExpanded: false);
            UIToggle anims = null, moves = null, positions = null, colors = null;
            GenerateUI.Button(
                GenerateUI.Row(track.Body),
                () => {
                    if(anims == null || moves == null || positions == null || colors == null) return;
                    bool value = !conf.TrackAnimations && !conf.TrackPositions
                        && !conf.TrackMoves && !conf.TrackColors;
                    anims.Set(value);
                    moves.Set(value);
                    positions.Set(value);
                    colors.Set(value);
                },
                "Toggle All",
                "fxrm_track_all"
            ).SetSecondary();
            anims = GenerateUI.Toggle(
                GenerateUI.Row(track.Body), def.TrackAnimations, conf.TrackAnimations,
                v => { conf.TrackAnimations = v; RefreshConditionalRows(); Save(); }, "Animate Track", "fxrm_track_anims");
            moves = GenerateUI.Toggle(
                GenerateUI.Row(track.Body), def.TrackMoves, conf.TrackMoves,
                v => { conf.TrackMoves = v; Save(); }, "Move Track", "fxrm_track_moves");
            positions = GenerateUI.Toggle(
                GenerateUI.Row(track.Body), def.TrackPositions, conf.TrackPositions,
                v => { conf.TrackPositions = v; Save(); }, "Position Track", "fxrm_track_positions");
            colors = GenerateUI.Toggle(
                GenerateUI.Row(track.Body), def.TrackColors, conf.TrackColors,
                v => { conf.TrackColors = v; RefreshConditionalRows(); Save(); }, "Track Color", "fxrm_track_colors");
        }
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_DLC_EVENTS", "DLC Events");
        SimpleToggle(sec.Body, def.HoldSounds, conf.HoldSounds, v => conf.HoldSounds = v, "HoldSound", "fxrm_holdsounds");
        SimpleToggle(sec.Body, def.HideIcons, conf.HideIcons, v => conf.HideIcons = v, "HideIcon & Judgements", "fxrm_hideicons");
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_MISC", "Misc");
        {
            var decoTypes = GenerateUI.Collapsible(sec.Body, "Decoration Types", startExpanded: false);
            decoTypesSection = decoTypes;
            UIToggle planetT = null, tileT = null, imageT = null, textT = null, particleT = null, hazardT = null;
            GenerateUI.Button(
                GenerateUI.Row(decoTypes.Body),
                () => {
                    if(planetT == null || tileT == null || imageT == null || textT == null || particleT == null || hazardT == null) return;
                    bool value = !conf.DecoPlanet && !conf.DecoTiles && !conf.DecoImage
                        && !conf.DecoText && !conf.Particles && !conf.DecoFailHitbox;
                    planetT.Set(value);
                    tileT.Set(value);
                    imageT.Set(value);
                    textT.Set(value);
                    particleT.Set(value);
                    hazardT.Set(value);
                },
                "Toggle All",
                "fxrm_deco_types_all"
            ).SetSecondary();
            planetT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoPlanet, conf.DecoPlanet,
                v => { conf.DecoPlanet = v; Save(); }, "Planet", "fxrm_deco_planet");
            tileT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoTiles, conf.DecoTiles,
                v => { conf.DecoTiles = v; Save(); }, "Tiles", "fxrm_deco_tiles");
            imageT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoImage, conf.DecoImage,
                v => { conf.DecoImage = v; Save(); }, "Image", "fxrm_deco_image");
            textT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoText, conf.DecoText,
                v => { conf.DecoText = v; Save(); }, "Text", "fxrm_deco_text");
            particleT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.Particles, conf.Particles,
                v => { conf.Particles = v; Save(); }, "Particles", "fxrm_particles");
            hazardT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoFailHitbox, conf.DecoFailHitbox,
                v => { conf.DecoFailHitbox = v; Save(); }, "Judgement Limit (Fail Hitbox)", "fxrm_deco_failhitbox");
            hazardT.Rect.AddToolTip(
                "DESC_FXRM_DECO_FAILHITBOX",
                "Removes decorations whose hitbox can fail your run (HitboxType: Kill), regardless of the type toggles above."
            );
        }
        removeAllRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            removeAllRow, def.RemoveAllDecorations, conf.RemoveAllDecorations,
            v => { conf.RemoveAllDecorations = v; Save(); },
            "Remove All Decorations",
            "fxrm_remove_all_deco"
        ).Rect.AddToolTip(
            "DESC_FXRM_REMOVE_ALL_DECO",
            "Off keeps decorations that judgement-conditional events reference (hit/miss feedback) and removes the rest."
        );
        SimpleToggle(sec.Body, def.LimitTrackOpacity, conf.LimitTrackOpacity,
            v => conf.LimitTrackOpacity = v,
            "Limit 'Track Opacity' Values to 100%", "fxrm_limit_opacity");
        setZoomRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            setZoomRow, def.SetCameraZoom, conf.SetCameraZoom,
            v => {
                conf.SetCameraZoom = v;
                RefreshConditionalRows();
                Save();
            },
            "Set Camera Zoom",
            "fxrm_set_zoom"
        );
        zoomSliderRow = GenerateUI.Row(sec.Body);
        UISlider zoom = GenerateUI.Slider(
            zoomSliderRow,
            def.CameraZoomScale, 100f, 1000f, conf.CameraZoomScale,
            v => Mathf.Clamp(Mathf.Round(v), 100f, 1000f), null, null,
            "Camera Zoom",
            "fxrm_zoom_scale"
        );
        zoom.Format = "0' %'";
        zoom.OnChanged = v => conf.CameraZoomScale = v;
        zoom.OnComplete = v => { conf.CameraZoomScale = v; Save(); };
        resetAnimRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            resetAnimRow, def.ResetTrackAnimation, conf.ResetTrackAnimation,
            v => { conf.ResetTrackAnimation = v; Save(); },
            "Set Track Animation to Default",
            "fxrm_reset_anim"
        );
        resetColorRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            resetColorRow, def.ResetTrackColor, conf.ResetTrackColor,
            v => { conf.ResetTrackColor = v; Save(); },
            "Set Track Color to Default",
            "fxrm_reset_color"
        );
        tutorialPatternsRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            tutorialPatternsRow, def.RemoveTutorialPatterns, conf.RemoveTutorialPatterns,
            v => { conf.RemoveTutorialPatterns = v; Save(); },
            "Turn off Tutorial Background Patterns",
            "fxrm_tutorial_patterns"
        ).Rect.AddToolTip(
            "DESC_FXRM_TUTORIAL_PATTERNS",
            "Also hides the default background's tiled pattern. Its pulsing shapes are always removed while Background is on."
        );
        RefreshConditionalRows();
        } 
    }
    private static void CreateSimpleEffectRemover(
        Transform parent, EffectRemoverSettings conf, EffectRemoverSettings def) {
        void Save() => EffectRemover.Save();
        GenerateUI.ToggleTip(
            parent, def.SimpleFilter, conf.SimpleFilter,
            v => { conf.SimpleFilter = v; Save(); },
            "Disable Filters", "fxrm_s_filter",
            "Turns off VFX filters (Grayscale, Arcade, etc.) at runtime without changing the chart.");
        GenerateUI.ToggleTip(
            parent, def.SimpleAdvancedFilter, conf.SimpleAdvancedFilter,
            v => { conf.SimpleAdvancedFilter = v; Save(); },
            "Disable Advanced Filter", "fxrm_s_advfilter",
            "Turns off Advanced Filter VFX at runtime without changing the chart.");
        GenerateUI.ToggleTip(
            parent, def.SimpleBloom, conf.SimpleBloom,
            v => { conf.SimpleBloom = v; Save(); },
            "Disable Bloom", "fxrm_s_bloom", "Skips the bloom effect.");
        GenerateUI.ToggleTip(
            parent, def.SimpleFlash, conf.SimpleFlash,
            v => { conf.SimpleFlash = v; Save(); },
            "Disable Flash", "fxrm_s_flash", "Neutralises screen-flash effects.");
        GenerateUI.ToggleTip(
            parent, def.SimpleHallOfMirrors, conf.SimpleHallOfMirrors,
            v => { conf.SimpleHallOfMirrors = v; Save(); },
            "Disable Hall of Mirrors", "fxrm_s_hom", "Skips the Hall of Mirrors effect.");
        GenerateUI.ToggleTip(
            parent, def.SimpleScreenShake, conf.SimpleScreenShake,
            v => { conf.SimpleScreenShake = v; Save(); },
            "Disable Screen Shake", "fxrm_s_shake", "Skips screen-shake effects.");
        GenerateUI.Slider(
            GenerateUI.Row(parent),
            def.SimpleMoveTrackMax, 5f, EffectRemoverSettings.MoveTrackUpperBound + 5f,
            conf.SimpleMoveTrackMax,
            f => Mathf.Round(f / 5f) * 5f,
            _ => { },
            v => { conf.SimpleMoveTrackMax = Mathf.RoundToInt(v); Save(); },
            "Max Tile Movements", "fxrm_s_movemax"
        ).Rect.AddToolTip("DESC_FXRM_S_MOVEMAX",
            "Caps how many tiles a single Move Track event can move (around the current tile). The maximum value means unlimited.");
    }
    private static void CreateOttoIcon(Transform content) {
        OttoIcon.EnsureConf();
        OttoIconSettings conf = OttoIcon.Conf;
        OttoIconSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Otto Icon",
            v => {
                conf.Enabled = v;
                if(v) OttoIcon.Refresh();
                else OttoIcon.Restore();
                OttoIcon.Save();
            },
            conf.Enabled,
            "Enable Otto Icon", "ottoicon_enable"
        );
        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetColor(),
            conf.GetColor(),
            c => { conf.SetColor(c); OttoIcon.Refresh(); },
            c => { conf.SetColor(c); OttoIcon.Refresh(); OttoIcon.Save(); },
            "Otto Color",
            "otto_color"
        );
        RectTransform highBpmColorRow = null;
        GenerateUI.ToggleTip(
            sec.Body,
            def.UseHighBpmColor,
            conf.UseHighBpmColor,
            v => {
                conf.UseHighBpmColor = v;
                highBpmColorRow?.gameObject.SetActive(v);
                OttoIcon.Refresh();
                OttoIcon.Save();
            },
            "Separate High BPM Color",
            "otto_highbpm_on",
            "On: Otto uses the color below while the level's top BPM is 300+ (where vanilla turns him red). Off: the normal color is always used."
        );
        highBpmColorRow = GenerateUI.Row(sec.Body);
        GenerateUI.ColorPicker(
            highBpmColorRow,
            def.GetHighBpmColor(),
            conf.GetHighBpmColor(),
            c => { conf.SetHighBpmColor(c); OttoIcon.Refresh(); },
            c => { conf.SetHighBpmColor(c); OttoIcon.Refresh(); OttoIcon.Save(); },
            "High BPM Color",
            "otto_highbpm_color"
        );
        highBpmColorRow.gameObject.SetActive(conf.UseHighBpmColor);
        UISlider offsetX = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.OffsetX, -100f, 100f, conf.OffsetX,
            v => Mathf.Round(v), null, null,
            "Offset X",
            "otto_offset_x"
        );
        offsetX.Format = "0";
        offsetX.OnChanged = v => { conf.OffsetX = v; OttoIcon.Refresh(); };
        offsetX.OnComplete = v => { conf.OffsetX = v; OttoIcon.Refresh(); OttoIcon.Save(); };
        UISlider offsetY = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.OffsetY, -100f, 100f, conf.OffsetY,
            v => Mathf.Round(v), null, null,
            "Offset Y",
            "otto_offset_y"
        );
        offsetY.Format = "0";
        offsetY.OnChanged = v => { conf.OffsetY = v; OttoIcon.Refresh(); };
        offsetY.OnComplete = v => { conf.OffsetY = v; OttoIcon.Refresh(); OttoIcon.Save(); };
    }
    private static void CreateUiHiding(Transform content) {
        UiHider.EnsureConf();
        UiHiderSettings conf = UiHider.Conf;
        UiHiderSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "UI Hiding",
            v => {
                conf.Enabled = v;
                if(v) UiHider.ApplyNow();
                else UiHider.Restore();
                UiHider.Save();
            },
            conf.Enabled,
            "Enable UI Hiding", "uihiding_enable"
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.RecordingMode,
            conf.RecordingMode,
            v => {
                conf.RecordingMode = v;
                UiHider.ApplyNow();
                UiHider.Save();
            },
            "Recording Mode",
            "uih_recmode",
            "Which profile is live right now: off = Playing, on = Recording."
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.UseShortcut,
            conf.UseShortcut,
            v => {
                conf.UseShortcut = v;
                UiHider.Save();
            },
            "Use Recording Mode Shortcut",
            "uih_useshortcut"
        );
        GenerateUI.KeyBind(
            GenerateUI.Row(sec.Body),
            (Keybind.KeyModifier)conf.ShortcutModifier,
            (KeyCode)conf.ShortcutKey,
            (mod, key) => {
                conf.ShortcutModifier = (int)mod;
                conf.ShortcutKey = (int)key;
                UiHider.Save();
            },
            "Recording Mode Shortcut",
            "uih_shortcut"
        );
        void ProfileSection(string title, UiHiderProfile profile, UiHiderProfile defProfile, string idPrefix) {
            var prof = GenerateUI.Collapsible(sec.Body, title, startExpanded: false);
            void Flag(string label, string id, bool defVal, bool val, Action<bool> set) {
                GenerateUI.Toggle(
                    GenerateUI.Row(prof.Body),
                    defVal,
                    val,
                    v => {
                        set(v);
                        UiHider.ApplyNow();
                        UiHider.Save();
                    },
                    label,
                    idPrefix + id
                );
            }
            Flag("Hide Everything (No HUD)", "_all", defProfile.HideEverything, profile.HideEverything, v => profile.HideEverything = v);
            Flag("Hide Judgement Text", "_judg", defProfile.HideJudgment, profile.HideJudgment, v => profile.HideJudgment = v);
            Flag("Hide Miss Indicators", "_miss", defProfile.HideMissIndicators, profile.HideMissIndicators, v => profile.HideMissIndicators = v);
            Flag("Hide Level Title", "_title", defProfile.HideTitle, profile.HideTitle, v => profile.HideTitle = v);
            Flag("Hide Otto / Autoplay Text", "_otto", defProfile.HideOtto, profile.HideOtto, v => profile.HideOtto = v);
            Flag("Hide Difficulty Icon", "_diff", defProfile.HideTimingTarget, profile.HideTimingTarget, v => profile.HideTimingTarget = v);
            Flag("Hide No Fail Icon", "_nofail", defProfile.HideNoFailIcon, profile.HideNoFailIcon, v => profile.HideNoFailIcon = v);
            Flag("Hide Beta Build Text", "_beta", defProfile.HideBeta, profile.HideBeta, v => profile.HideBeta = v);
            Flag("Hide Result Text", "_result", defProfile.HideResult, profile.HideResult, v => profile.HideResult = v);
            Flag("Hide Hit Error Meter", "_meter", defProfile.HideHitErrorMeter, profile.HideHitErrorMeter, v => profile.HideHitErrorMeter = v);
            Flag("Hide Last Floor Flash", "_flash", defProfile.HideLastFloorFlash, profile.HideLastFloorFlash, v => profile.HideLastFloorFlash = v);
        }
        ProfileSection("Playing Profile", conf.Playing, def.Playing, "uih_play");
        ProfileSection("Recording Profile", conf.Recording, def.Recording, "uih_rec");
    }
    private static void CreatePlanetColors(Transform content) {
        PlanetColors.EnsureConf();
        PlanetColorsSettings conf = PlanetColors.Conf;
        PlanetColorsSettings def = new();
        void Apply() => PlanetColors.Refresh();
        void Save() => PlanetColors.Save();
        var sec = GenerateUI.FlatSection(
            content, "Planet Colors",
            v => {
                conf.Enabled = v;
                if(v) PlanetColors.Refresh();
                else PlanetColors.Restore();
                Save();
            },
            conf.Enabled,
            "Enable Planet Colors", "planetcolors_enable"
        );
        RectTransform[] tailColorRows = new RectTransform[PlanetColorsSettings.Slots];
        void RefreshTailRows() {
            foreach(RectTransform row in tailColorRows) row?.gameObject.SetActive(conf.SeparateTailColor);
        }
        GenerateUI.ToggleTip(
            sec.Body,
            def.SeparateTailColor,
            conf.SeparateTailColor,
            v => {
                conf.SeparateTailColor = v;
                RefreshTailRows();
                Apply();
                Save();
            },
            "Separate Tail Color",
            "pcol_sep_tail",
            "Off: tails use the ball color (with their own opacity). On: each planet's tail gets its own color."
        );
        for(int i = 0; i < PlanetColorsSettings.Slots; i++) {
            int slot = i;
            string n = (slot + 1).ToString();
            GenerateUI.Localize(
                GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)),
                "HEADING_PLANET_" + n,
                $"Planet {n}"
            );
            GenerateUI.ColorPicker(
                GenerateUI.Row(sec.Body),
                new Color(def.BallR[slot], def.BallG[slot], def.BallB[slot]),
                new Color(conf.BallR[slot], conf.BallG[slot], conf.BallB[slot]),
                c => { conf.SetBallRgb(slot, c); Apply(); },
                c => { conf.SetBallRgb(slot, c); Apply(); Save(); },
                $"Planet {n} Color",
                $"pcol_ball{n}",
                showAlpha: false
            );
            UISlider ballOp = GenerateUI.Slider(
                GenerateUI.Row(sec.Body),
                def.BallOpacity[slot], 0f, 1f, conf.BallOpacity[slot],
                null, null, null,
                $"Planet {n} Ball Opacity",
                $"pcol_ballop{n}"
            );
            ballOp.Format = "0 %";
            ballOp.OnChanged = v => { conf.BallOpacity[slot] = v; Apply(); };
            ballOp.OnComplete = v => { conf.BallOpacity[slot] = v; Apply(); Save(); };
            tailColorRows[slot] = GenerateUI.Row(sec.Body);
            GenerateUI.ColorPicker(
                tailColorRows[slot],
                new Color(def.TailR[slot], def.TailG[slot], def.TailB[slot]),
                new Color(conf.TailR[slot], conf.TailG[slot], conf.TailB[slot]),
                c => { conf.SetTailRgb(slot, c); Apply(); },
                c => { conf.SetTailRgb(slot, c); Apply(); Save(); },
                $"Planet {n} Tail Color",
                $"pcol_tail{n}",
                showAlpha: false
            );
            UISlider tailOp = GenerateUI.Slider(
                GenerateUI.Row(sec.Body),
                def.TailOpacity[slot], 0f, 1f, conf.TailOpacity[slot],
                null, null, null,
                $"Planet {n} Tail Opacity",
                $"pcol_tailop{n}"
            );
            tailOp.Format = "0 %";
            tailOp.OnChanged = v => { conf.TailOpacity[slot] = v; Apply(); };
            tailOp.OnComplete = v => { conf.TailOpacity[slot] = v; Apply(); Save(); };
        }
        GenerateUI.Localize(
            GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)),
            "HEADING_RING",
            "Ring"
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.EnableRingRecolor,
            conf.EnableRingRecolor,
            v => { conf.EnableRingRecolor = v; Apply(); Save(); },
            "Recolor Ring",
            "pcol_ringon"
        ).Rect.AddToolTip(
            "DESC_PCOL_RING",
            "Paint the planet ring a custom colour. When off, the ring is hidden while planet colours are active."
        );
        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            new Color(def.RingR, def.RingG, def.RingB),
            new Color(conf.RingR, conf.RingG, conf.RingB),
            c => { conf.SetRingRgb(c); Apply(); },
            c => { conf.SetRingRgb(c); Apply(); Save(); },
            "Ring Color",
            "pcol_ringcol",
            showAlpha: false
        );
        UISlider ringOp = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.RingA, 0f, 1f, conf.RingA,
            null, null, null,
            "Ring Opacity",
            "pcol_ringop"
        );
        ringOp.Format = "0 %";
        ringOp.OnChanged = v => { conf.RingA = v; Apply(); };
        ringOp.OnComplete = v => { conf.RingA = v; Apply(); Save(); };
        RefreshTailRows();
    }
    private static void CreateVisualTweaks(Transform content) {
        Tweaks.EnsureConf();
        TweaksSettings conf = Tweaks.Conf;
        TweaksSettings def = new();
        var sec = GenerateUI.FlatSection(content, "Visual Tweaks");
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemoveAllCheckpoints,
            conf.RemoveAllCheckpoints,
            v => { conf.RemoveAllCheckpoints = v; Tweaks.RefreshCheckpointTweak(); Tweaks.Save(); },
            "Remove All Checkpoints",
            "tw_cp",
            "Strips checkpoint icons and behavior from the level — dying always restarts the run. Turning this off needs a level reload to bring icons back."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemoveBallCoreParticles,
            conf.RemoveBallCoreParticles,
            v => { conf.RemoveBallCoreParticles = v; Tweaks.RefreshBallCoreParticlesTweak(); Tweaks.Save(); },
            "Remove Ball Core Particles",
            "tw_bcp",
            "Removes the planets' core and spark particles."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.DisableTileHitGlow,
            conf.DisableTileHitGlow,
            v => { conf.DisableTileHitGlow = v; Tweaks.RefreshTileHitGlowTweak(); Tweaks.Save(); },
            "Disable Tile Hit Glow",
            "tw_glow",
            "Suppresses the glow flash tiles get when the planet lands on them."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemovePlanetGlow,
            conf.RemovePlanetGlow,
            v => { conf.RemovePlanetGlow = v; Tweaks.RefreshPlanetGlowTweak(); Tweaks.Save(); },
            "Remove Planet Glow",
            "tw_pglow",
            "Hides the glow sprite drawn around the planets."
        );
    }
    private static void CreateHideJudgements(Transform content) {
        JudgementPopupHider.EnsureConf();
        JudgementPopupHiderSettings conf = JudgementPopupHider.Conf;
        JudgementPopupHiderSettings def = new();
        (HitMargin Margin, string Label, string Id)[] entries = [
            (HitMargin.TooEarly, "Too Early", "jpop_tooearly"),
            (HitMargin.VeryEarly, "Very Early", "jpop_veryearly"),
            (HitMargin.EarlyPerfect, "Early Perfect", "jpop_earlyperfect"),
            (HitMargin.Perfect, "Perfect", "jpop_perfect"),
            (HitMargin.LatePerfect, "Late Perfect", "jpop_lateperfect"),
            (HitMargin.VeryLate, "Very Late", "jpop_verylate"),
            (HitMargin.TooLate, "Too Late", "jpop_toolate"),
            (HitMargin.Multipress, "Multipress", "jpop_multipress"),
            (HitMargin.FailMiss, "Miss", "jpop_miss"),
            (HitMargin.FailOverload, "Overload (No Fail)", "jpop_overload_nofail"),
            (HitMargin.Auto, "Auto", "jpop_auto"),
            (HitMargin.OverPress, "Overload (Fail)", "jpop_overload_fail"),
        ];
        List<RectTransform> maskRows = [];
        void RefreshMaskRows() {
            foreach(RectTransform row in maskRows) row?.gameObject.SetActive(conf.Enabled);
        }
        var sec = GenerateUI.FlatSection(
            content, "Hide Judgements",
            v => {
                conf.Enabled = v;
                RefreshMaskRows();
                JudgementPopupHider.Save();
            },
            conf.Enabled,
            "Enable Hide Judgements", "hidejudgements_enable"
        );
        void AddMaskToggle(int maskBit, string label, string id) {
            RectTransform row = GenerateUI.Row(sec.Body);
            maskRows.Add(row);
            GenerateUI.Toggle(
                row,
                (def.HiddenMask & maskBit) != 0,
                (conf.HiddenMask & maskBit) != 0,
                v => {
                    if(v) conf.HiddenMask |= maskBit;
                    else conf.HiddenMask &= ~maskBit;
                    JudgementPopupHider.Save();
                },
                label,
                id
            );
        }
        // Gate on Installed (assembly present), NOT Active. These X/+/- toggles stay meaningful
        // even while XPerfect is disabled: ShouldHide's carry treats "all grades hidden" as "hide
        // plain Perfect", so unchecking any grade shows Perfect and checking all hides it — full
        // control across enable/disable with no mode-flip (so no page-rebuild needed mid-game).
        // Vanilla users (Installed == false) get the single plain "Perfect" toggle instead.
        bool xperfect = XPerfectBridge.Installed;
        foreach(var entry in entries) {
            if(entry.Margin == HitMargin.Perfect && xperfect) {
                AddMaskToggle(1 << JudgementPopupHider.XPerfectPerfectBit, "X Perfect", "jpop_xperfect");
                AddMaskToggle(1 << JudgementPopupHider.PlusPerfectBit, "+ Perfect", "jpop_plusperfect");
                AddMaskToggle(1 << JudgementPopupHider.MinusPerfectBit, "- Perfect", "jpop_minusperfect");
            } else {
                AddMaskToggle(1 << (int)entry.Margin, entry.Label, entry.Id);
            }
        }
        RefreshMaskRows();
    }
}
