using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Quartz.Async;
using Quartz.Core;
using Quartz.Core.Service;
using Quartz.IO;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using Quartz.Utility;
using Quartz.Update;
using UnityEngine;
using UnityEngine.UI;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static partial class PageSettings {
    private static UIDropDown<string> languageDropdown;
    private static UIDropDown<string> fontDropdown;
    private static GameObject fontManageRow;
    private static UIInput fontRenameInput;
    private static UIButton fontDeleteBtn;
    private static Func<Color> fontDeleteRestColor;
    private static bool fontDeleteArmed;
    private static GameObject fontStatusRow;
    private static TextMeshProUGUI fontStatusText;
    private static UIDropDown<string> settingsFontDropdown;
    private static string pendingFontName = "";
    private static UIButton updateCheckButton;
    private static TextMeshProUGUI updateStatusText;
    private static GameObject updateActionRow;
    private static GameObject updateButtonRow;
    private static TextMeshProUGUI updateVersionText;
    private static UIButton updateNotesButton;
    private static UIButton updateSkipButton;
    private static UIButton updateInstallButton;
    private static UIButton updateUndoButton;
    private static GameObject updateProgressRow;
    private static RectTransform updateProgressFill;
    private static TextMeshProUGUI updateProgressLabel;
    private static bool updateHooked;
    private static UIScrollController scrollController;
    private static RectTransform pageContent;
    private static RectTransform updatesAnchor;
    public static void Create(RectTransform parent) {
        FontManager.OnFontCatalogChanged -= RefreshFontDropdowns;
        FontManager.OnFontCatalogChanged += RefreshFontDropdowns;
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent, out scrollController);
        pageContent = content;
        CoreSettings defSet = new();
        var langLabelRow = GenerateUI.Row(content.transform);
        var langText = GenerateUI.AddTextH1(langLabelRow);
        var langTextTr = langText.gameObject.AddComponent<TextLocalization>().Init("LANGUAGE", "Language");
        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        var langRow = GenerateUI.Row(content.transform);
        languageDropdown = GenerateUI.DropDown(
            langRow,
            null,
            MainCore.Tr.Language,
            langs,
            lang => {
                if(lang == Translator.FALLBACK_LANGUAGE) return "DEFAULT";
                string native = MainCore.Tr.GetForLanguage("0NATIVELANG", lang, lang);
                return $"{native} ({lang})";
            },
            value => {
                MainCore.Tr.Language = value;
                MainCore.Conf.Language = value;
                MainCore.ConfMgr.RequestSave();
                TextLocalization.RefreshAll();
            },
            "language_dropdown"
        );
        UIButton langBtn = GenerateUI.Button(
            langRow,
            () => { },
            "Reload",
            "language_reload"
        );
        langBtn.OnClick = async () => {
            languageDropdown.SetExpanded(false);
            languageDropdown.SetBlocked(true);
            langBtn.SetBlocked(true);
            langBtn.Label.text = "...";
            _ = Task.Run(async () => {
                await LangUpdateService.FetchAsync(MainCore.Paths.LangPath);
                await MainCore.Tr.Load(MainCore.Paths.LangPath);
                MainThread.Enqueue(() => {
                    languageDropdown.SetBlocked(false);
                    langBtn.SetBlocked(false);
                    TextLocalization.RefreshAll();
                    RefreshUpdates();
                });
            });
        };
        {
            var br = langBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.sizeDelta = new(114f, 50f);
            br.offsetMax = Vector2.zero;
        }
        langBtn.Label.gameObject.AddComponent<TextLocalization>().Init("RELOAD", "Reload");
        var overlayerText = GenerateUI.AddTextH1(GenerateUI.Row(content.transform));
        var overlayerTextTr = overlayerText.gameObject.AddComponent<TextLocalization>().Init("OVERLAYER", "Quartz");
        var startupRow = GenerateUI.Row(content.transform);
        UIToggle startupToggle = GenerateUI.Toggle(
            startupRow,
            defSet.ShowOnStartup,
            MainCore.Conf.ShowOnStartup,
            toggle => {
                MainCore.Conf.ShowOnStartup = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Show Quartz Settings at Startup",
            "show_on_startup"
        );
        var startupToggleTr = startupToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_OVERLAYER_PANEL_AT_STARTUP", "Show Quartz Settings at Startup");
        var blockInputsRow = GenerateUI.Row(content.transform);
        UIToggle blockInputsToggle = GenerateUI.Toggle(
            blockInputsRow,
            defSet.BlockInputsWhileMenuOpen,
            MainCore.Conf.BlockInputsWhileMenuOpen,
            toggle => {
                MainCore.Conf.BlockInputsWhileMenuOpen = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Block game inputs while menu is open",
            "block_inputs_while_menu_open"
        );
        var keybindRow = GenerateUI.Row(content.transform);
        var keybindLabel = GenerateUI.KeyBind(
            keybindRow,
            (Keybind.KeyModifier)MainCore.Conf.ToggleModifier,
            (KeyCode)MainCore.Conf.ToggleKey,
            (mod, key) => {
                MainCore.Conf.ToggleModifier = (int)mod;
                MainCore.Conf.ToggleKey = (int)key;
                MainCore.ConfMgr.RequestSave();
            },
            "Toggle Menu Keybind",
            "toggle_keybind"
        );
        var keybindTr = keybindLabel.gameObject.AddComponent<TextLocalization>().Init("TOGGLE_KEYBIND", "Toggle Menu Keybind");
        var tooltipRow = GenerateUI.Row(content.transform);
        UIToggle tooltipToggle = GenerateUI.Toggle(
            tooltipRow,
            defSet.Tooltip,
            MainCore.Conf.Tooltip,
            toggle => {
                Tooltip.Hide();
                MainCore.Conf.Tooltip = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Show Tooltip",
            "show_tooltip"
        );
        tooltipToggle.Rect.AddToolTip(
            "DESC_SHOW_TOOLTIP",
            "Shows a description bubble when you hover over a setting. Turning this off hides all tooltips, including this one."
        );
        var tooltipToggleTr = tooltipToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_TOOLTIP", "Show Tooltip");
        var middleClickRow = GenerateUI.Row(content.transform);
        UIToggle middleClickToggle = GenerateUI.Toggle(
            middleClickRow,
            defSet.MiddleClickToDefault,
            MainCore.Conf.MiddleClickToDefault,
            toggle => {
                MainCore.Conf.MiddleClickToDefault = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Middle-click to set as default",
            "middle_click_default"
        );
        middleClickToggle.Rect.AddToolTip(
            "DESC_MIDDLE_CLICK_TO_SET_AS_DEFAULT",
            "Setting that restores an item to its default value when you middle-click on it.\nYou can identify it by a small dot at the top-left of the item"
        );
        var middleClickToggleTr = middleClickToggle.Label.gameObject.AddComponent<TextLocalization>().Init("MIDDLE_CLICK_TO_SET_AS_DEFAULT", "Middle-click to set as default");
        static float uiScaleFilter(float v) {
            v = Mathf.Round(v * 100f) / 100f;
            return Mathf.Clamp(v, 0.8f, 1.6f);
        }
        var uiScaleRow = GenerateUI.Row(content.transform);
        UISlider uiScale = GenerateUI.Slider(
            uiScaleRow,
            1f,
            0.8f,
            1.6f,
            MainCore.Conf.UIScale,
            uiScaleFilter,
            null,
            null,
            "UI Scale",
            "ui_scale"
        );
        uiScale.Format = "0.00x";
        uiScale.OnChanged = value => MainCore.Conf.UIScale = value;
        GTween scaleSeq = null;
        uiScale.OnComplete = value => {
            MainCore.Conf.UIScale = value;
            MainCore.ConfMgr.RequestSave();
            scaleSeq?.Kill();
            float scaleStart = UICore.PanelScale;
            Vector2 targetSize = UICore.Panel.sizeDelta * (scaleStart / value);
            targetSize = new Vector2(
                Mathf.Clamp(targetSize.x, ResizeHandle.MIN_WIDTH / value, Screen.width / value),
                Mathf.Clamp(targetSize.y, ResizeHandle.MIN_HEIGHT / value, Screen.height / value)
            );
            UICore.LastPanelSize = targetSize;
            MainCore.Conf.PanelWidth = targetSize.x;
            MainCore.Conf.PanelHeight = targetSize.y;
            scaleSeq = GTweenSequenceBuilder.New()
                .Append(
                    GTweenExtensions.Tween(
                        () => scaleStart,
                        x => UICore.PanelScale = x,
                        value,
                        0.4f
                    ).SetEasing(Easing.OutExpo)
                )
                .Join(
                    UICore.Panel.GTSizeDelta(targetSize, 0.4f)
                        .SetEasing(Easing.OutExpo)
                )
                .Build();
            MainCore.TC.Play(scaleSeq);
        };
        var uiScaleTr = uiScale.Label.gameObject.AddComponent<TextLocalization>().Init("UI_SCALE", "UI Scale");
        var scrollRow = GenerateUI.Row(content.transform);
        UISlider scrollSpeed = GenerateUI.Slider(
            scrollRow,
            80f,
            20f,
            300f,
            MainCore.Conf.ScrollSpeed,
            Mathf.Round,
            v => MainCore.Conf.ScrollSpeed = v,
            v => { MainCore.Conf.ScrollSpeed = v; MainCore.ConfMgr.RequestSave(); },
            "Scroll Speed",
            "scroll_speed"
        );
        scrollSpeed.Format = "0 px";
        var scrollTr = scrollSpeed.Label.gameObject.AddComponent<TextLocalization>().Init("SCROLL_SPEED", "Scroll Speed");
        var opacityRow = GenerateUI.Row(content.transform);
        UISlider opacity = GenerateUI.Slider(
            opacityRow,
            100f,
            20f,
            100f,
            MainCore.Conf.PanelOpacity * 100f,
            Mathf.Round,
            v => UICore.SetPanelOpacity(v / 100f, false),
            v => UICore.SetPanelOpacity(v / 100f, true),
            "Window Opacity",
            "window_opacity"
        );
        opacity.Format = "0'%'";
        opacity.Rect.AddToolTip(
            "DESC_WINDOW_OPACITY",
            "Transparency of the settings window."
        );
        var opacityTr = opacity.Label.gameObject.AddComponent<TextLocalization>().Init("WINDOW_OPACITY", "Window Opacity");
        var outlineRow = GenerateUI.Row(content.transform);
        UISlider outlineWidth = GenerateUI.Slider(
            outlineRow,
            6.25f,
            0f,
            15f,
            MainCore.Conf.OutlineWidth,
            v => Mathf.Round(v * 4f) / 4f,
            v => { MainCore.Conf.OutlineWidth = v; UICore.SetOutlineWidth(v, false); },
            v => { MainCore.Conf.OutlineWidth = v; UICore.SetOutlineWidth(v, true); MainCore.ConfMgr.RequestSave(); },
            "Outline Width",
            "outline_width"
        );
        outlineWidth.Format = "0.## px";
        outlineWidth.Rect.AddToolTip(
            "DESC_OUTLINE_WIDTH",
            "Thickness of the settings window's white outlines — the border ring, the submenu column, the top rule and the bottom pane edge."
        );
        var outlineTr = outlineWidth.Label.gameObject.AddComponent<TextLocalization>().Init("OUTLINE_WIDTH", "Outline Width");
        var accentRow = GenerateUI.Row(content.transform);
        UIColorPicker accentPicker = GenerateUI.ColorPicker(
            accentRow,
            new Color(1f, 0.6f, 0.6f, 1f),
            MainCore.Conf.GetAccentColor(),
            c => UICore.SetAccentColor(c, false),
            c => UICore.SetAccentColor(c, true),
            "Accent Color",
            "accent_color",
            false
        );
        accentPicker.Rect.AddToolTip(
            "DESC_ACCENT_COLOR",
            "Recolors the whole Quartz UI. Middle-click to reset."
        );
        var accentTr = accentPicker.Label.gameObject.AddComponent<TextLocalization>().Init("ACCENT_COLOR", "Accent Color");
        var updatesLabelRow = GenerateUI.Row(content.transform);
        var updatesText = GenerateUI.AddTextH1(updatesLabelRow);
        var updatesTextTr = updatesText.gameObject.AddComponent<TextLocalization>().Init("UPDATES", "Updates");
        updatesAnchor = updatesLabelRow;
        ReleaseChannel[] channels = [
            ReleaseChannel.Stable,
            ReleaseChannel.ReleaseCandidate,
            ReleaseChannel.Beta,
            ReleaseChannel.Alpha,
        ];
        var channelRow = GenerateUI.Row(content.transform);
        UIDropDown<ReleaseChannel> channelDropdown = GenerateUI.DropDown(
            channelRow,
            ReleaseChannel.Stable,
            MainCore.Conf.GetUpdateChannel(),
            channels,
            ch => ch switch {
                ReleaseChannel.Stable => MainCore.Tr.Get("UPDATE_CHANNEL_STABLE", "Stable"),
                ReleaseChannel.ReleaseCandidate => MainCore.Tr.Get("UPDATE_CHANNEL_RC", "Release Candidate"),
                ReleaseChannel.Beta => MainCore.Tr.Get("UPDATE_CHANNEL_BETA", "Beta"),
                ReleaseChannel.Alpha => MainCore.Tr.Get("UPDATE_CHANNEL_ALPHA", "Alpha"),
                _ => ch.ToString(),
            },
            ch => {
                MainCore.Conf.UpdateChannel = (int)ch;
                MainCore.ConfMgr.RequestSave();
            },
            "update_channel"
        );
        channelDropdown.Rect.AddToolTip(
            "DESC_UPDATE_CHANNEL",
            "Which builds to receive when updating. Alpha includes every build; each step up is more stable, with Stable being only final releases."
        );
        var updateCheckRow = GenerateUI.Row(content.transform);
        updateCheckButton = GenerateUI.Button(
            updateCheckRow,
            () => UpdateService.Check(),
            "Check for Updates",
            "update_check"
        );
        updateCheckButton.Label.gameObject.AddComponent<TextLocalization>().Init("CHECK_FOR_UPDATES", "Check for Updates");
        var updateStatusRow = GenerateUI.Row(content.transform);
        updateStatusText = GenerateUI.AddText(updateStatusRow, noPad: true);
        updateStatusText.text = "";
        {
            var progressRect = GenerateUI.Row(content.transform, 32f);
            updateProgressRow = progressRect.gameObject;
            GameObject track = new("ProgressTrack");
            track.transform.SetParent(progressRect, false);
            RectTransform trackRect = track.AddComponent<RectTransform>();
            trackRect.anchorMin = new(0f, 0.5f);
            trackRect.anchorMax = new(1f, 0.5f);
            trackRect.pivot = new(0f, 0.5f);
            trackRect.offsetMin = new(0f, -7f);
            trackRect.offsetMax = new(-250f, 7f);
            Image trackImg = track.AddComponent<Image>();
            trackImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            trackImg.type = Image.Type.Sliced;
            trackImg.color = UIColors.ObjectBG;
            trackImg.raycastTarget = false;
            GameObject fill = new("ProgressFill");
            fill.transform.SetParent(track.transform, false);
            updateProgressFill = fill.AddComponent<RectTransform>();
            updateProgressFill.anchorMin = Vector2.zero;
            updateProgressFill.anchorMax = new(0f, 1f);
            updateProgressFill.offsetMin = Vector2.zero;
            updateProgressFill.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = UIColors.ObjectActive;
            fillImg.raycastTarget = false;
            GameObject pctObj = new("ProgressPercent");
            pctObj.transform.SetParent(progressRect, false);
            RectTransform pctRect = pctObj.AddComponent<RectTransform>();
            pctRect.anchorMin = new(1f, 0f);
            pctRect.anchorMax = new(1f, 1f);
            pctRect.pivot = new(0f, 0.5f);
            pctRect.anchoredPosition = new(-238f, 0f);
            pctRect.sizeDelta = new(90f, 0f);
            updateProgressLabel = pctObj.AddComponent<TextMeshProUGUI>();
            updateProgressLabel.font = FontManager.Current;
            updateProgressLabel.fontSize = 18f;
            updateProgressLabel.color = Color.white;
            updateProgressLabel.alignment = TextAlignmentOptions.Left;
            updateProgressLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
            updateProgressLabel.raycastTarget = false;
            updateProgressRow.SetActive(false);
        }
        var updateActionRect = GenerateUI.Row(content.transform);
        updateActionRow = updateActionRect.gameObject;
        GenerateUI.ButtonRow(updateActionRect, 0f);
        updateVersionText = GenerateUI.AddText(updateActionRect, true);
        updateVersionText.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement versionLe = updateVersionText.gameObject.AddComponent<LayoutElement>();
        versionLe.flexibleWidth = 1f;
        var updateButtonRect = GenerateUI.Row(content.transform);
        updateButtonRow = updateButtonRect.gameObject;
        GenerateUI.ButtonRow(updateButtonRect);
        updateNotesButton = GenerateUI.Button(
            updateButtonRect,
            () => {
                string url = UpdateService.Available?.Url;
                if(!string.IsNullOrEmpty(url)) Application.OpenURL(url);
            },
            "Notes",
            "update_notes"
        ).SetSecondary();
        GenerateUI.FixWidth(updateNotesButton, 100f);
        updateNotesButton.Label.gameObject.AddComponent<TextLocalization>().Init("UPDATE_NOTES", "Notes");
        updateNotesButton.Rect.AddToolTip(
            "DESC_UPDATE_NOTES",
            "Opens this release's notes on GitHub."
        );
        updateSkipButton = GenerateUI.Button(
            updateButtonRect,
            () => UpdateService.Skip(UpdateService.Available),
            "Skip",
            "update_skip"
        ).SetSecondary();
        GenerateUI.FixWidth(updateSkipButton, 100f);
        updateSkipButton.Label.gameObject.AddComponent<TextLocalization>().Init("UPDATE_SKIP", "Skip");
        updateSkipButton.Rect.AddToolTip(
            "DESC_UPDATE_SKIP",
            "Hides this version. You'll still be offered the next release."
        );
        updateInstallButton = GenerateUI.Button(
            updateButtonRect,
            () => UpdateService.Install(UpdateService.Available),
            "Install",
            "update_install"
        );
        GenerateUI.FixWidth(updateInstallButton, 130f);
        updateInstallButton.Label.gameObject.AddComponent<TextLocalization>().Init("UPDATE_INSTALL", "Install");
        updateUndoButton = GenerateUI.Button(
            updateButtonRect,
            () => UpdateService.UndoSkip(),
            "Undo",
            "update_undo"
        ).SetSecondary();
        GenerateUI.FixWidth(updateUndoButton, 100f);
        updateUndoButton.Label.gameObject.AddComponent<TextLocalization>().Init("UPDATE_UNDO", "Undo");
        if(!updateHooked) {
            UpdateService.OnChanged += RefreshUpdates;
            MainCore.Tr.OnLanguageChanged += _ => RefreshUpdates();
            updateHooked = true;
        }
        RefreshUpdates();
        var fontLabelRow = GenerateUI.Row(content.transform);
        var fontText = GenerateUI.AddTextH1(fontLabelRow);
        var fontTextTr = fontText.gameObject.AddComponent<TextLocalization>().Init("FONT", "Font");
        GameObject fontGroup = new("FontGroup");
        fontGroup.transform.SetParent(content.transform, false);
        fontGroup.AddComponent<RectTransform>();
        GenerateUI.FitVertical(fontGroup, 8f);
        var fontRow = GenerateUI.Row(fontGroup.transform);
        fontDropdown = GenerateUI.DropDown(
            fontRow,
            FontManager.DefaultName,
            FontManager.CurrentName,
            BuildFontValues(),
            DisplayFont,
            OnFontSelected,
            "font_dropdown"
        );
        fontDropdown.ItemFont = FontManager.GetFont;
        var manageRow = GenerateUI.Row(fontGroup.transform);
        fontManageRow = manageRow.gameObject;
        fontRenameInput = GenerateUI.Input(
            manageRow,
            null,
            FontManager.CurrentName,
            v => pendingFontName = v,
            "Font Name",
            MainCore.Spr.Get(UISprite.Text128),
            "font_rename"
        );
        fontRenameInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("FONT_NAME", "Font Name");
        fontRenameInput.InputField.characterLimit = 40;
        fontDeleteBtn = GenerateUI.Button(
            manageRow,
            () => DeleteCurrentFont(),
            "Delete",
            "font_delete"
        ).SetSecondary();
        fontDeleteRestColor = fontDeleteBtn.RestColor;
        {
            var br = fontDeleteBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.sizeDelta = new(104f, 50f);
            br.anchoredPosition = Vector2.zero;
        }
        fontDeleteBtn.Label.gameObject.AddComponent<TextLocalization>().Init("FONT_DELETE", "Delete");
        UIButton fontRenameBtn = GenerateUI.Button(
            manageRow,
            RenameCurrentFont,
            "Rename",
            "font_rename_btn"
        );
        {
            var br = fontRenameBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.sizeDelta = new(104f, 50f);
            br.anchoredPosition = new(-112f, 0f);
        }
        fontRenameBtn.Label.gameObject.AddComponent<TextLocalization>().Init("FONT_RENAME", "Rename");
        var fontStatusRowRect = GenerateUI.Row(fontGroup.transform, 28f);
        fontStatusRow = fontStatusRowRect.gameObject;
        fontStatusText = GenerateUI.AddMutedText(fontStatusRowRect, 16f, 0.5f, true);
        fontStatusText.text = "";
        fontStatusRow.SetActive(false);
        RefreshFontManageRow();
        var settingsFontLabelRow = GenerateUI.Row(content.transform);
        var settingsFontText = GenerateUI.AddTextH1(settingsFontLabelRow);
        var settingsFontTextTr = settingsFontText.gameObject.AddComponent<TextLocalization>().Init("SETTINGS_FONT", "Settings Window Font");
        var settingsFontRow = GenerateUI.Row(content.transform);
        settingsFontDropdown = GenerateUI.DropDown(
            settingsFontRow,
            FontManager.SameAsOverlay,
            CurrentSettingsFontValue(),
            BuildSettingsFontValues(),
            DisplaySettingsFont,
            OnSettingsFontSelected,
            "settings_font_dropdown"
        );
        settingsFontDropdown.ItemFont = FontManager.GetFont;
        settingsFontDropdown.Rect.AddToolTip(
            "DESC_SETTINGS_FONT",
            "Font for this mod's own settings window. \"Same as overlay font\" follows the Font picker above."
        );
    }
}
