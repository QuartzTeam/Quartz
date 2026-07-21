using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Quartz.Async;
using Quartz.Core;
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
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal static partial class PageSettings {
    private static string CurrentSettingsFontValue() {
        string name = MainCore.Conf.SettingsFontName;
        return string.IsNullOrEmpty(name) ? FontManager.SameAsOverlay : name;
    }
    private static void OnSettingsFontSelected(string name) {
        MainCore.Conf.SettingsFontName = name == FontManager.SameAsOverlay ? "" : name;
        MainCore.ConfMgr.RequestSave();
        FontManager.ApplyMenuFont();
    }
    private static IReadOnlyList<string> BuildSettingsFontValues() {
        var list = new List<string> { FontManager.SameAsOverlay };
        list.AddRange(FontManager.GetAvailableFonts());
        return list;
    }
    private static string DisplaySettingsFont(string name) =>
        name == FontManager.SameAsOverlay
            ? GenerateUI.Tr("FONT_SAME_AS_OVERLAY", "Same as overlay font")
            : name == FontManager.DefaultName
                ? GenerateUI.Tr("FONT_DEFAULT", "Default (Cookie Run Bold)")
                : name;
    private static void RefreshFontDropdowns() {
        if(fontDropdown != null) {
            fontDropdown.SetValues(BuildFontValues());
            fontDropdown.Set(FontManager.CurrentName, false);
        }
        if(settingsFontDropdown != null) {
            settingsFontDropdown.SetValues(BuildSettingsFontValues());
            settingsFontDropdown.Set(CurrentSettingsFontValue(), false);
        }
    }
    private static IReadOnlyList<string> BuildFontValues() {
        var list = new List<string>(FontManager.GetAvailableFonts()) { FontManager.AddSentinel };
        return list;
    }
    private static string DisplayFont(string name) =>
        name == FontManager.AddSentinel
            ? GenerateUI.Tr("FONT_ADD", "＋  Add custom font…")
            : name == FontManager.DefaultName
                ? GenerateUI.Tr("FONT_DEFAULT", "Default (Cookie Run Bold)")
                : name;
    private static void OnFontSelected(string name) {
        if(name == FontManager.AddSentinel) {
            fontDropdown.Set(FontManager.CurrentName, false);
            AddCustomFont();
            return;
        }
        SetFontStatus(null);
        FontManager.SetFont(name, true);
        RefreshFontManageRow();
    }
    private static void AddCustomFont() {
        string path;
        try {
            path = FileDialog.PickFile(
                null,
                "Font",
                ["ttf", "otf", "ttc"],
                GenerateUI.Tr("FONT_PICK_TITLE", "Select a font file")
            );
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PageSettings)}] font PickFile failed: {e}");
            return;
        }
        if(string.IsNullOrEmpty(path)) return;
        string name = FontManager.ImportFont(path);
        if(name == null) {
            SetFontStatus(GenerateUI.Tr("FONT_IMPORT_FAILED", "Couldn't import that file."));
            return;
        }
        fontDropdown.SetValues(BuildFontValues());
        fontDropdown.Set(name, true);
        SetFontStatus(string.Format(GenerateUI.Tr("FONT_ADDED", "Added '{0}'."), name));
    }
    private static void RenameCurrentFont() {
        string cur = FontManager.CurrentName;
        if(!FontManager.IsCustomFont(cur)) return;
        if(FontManager.RenameFont(cur, pendingFontName, out string error)) {
            fontDropdown.SetValues(BuildFontValues());
            fontDropdown.Set(FontManager.CurrentName, false);
            RefreshFontManageRow();
            SetFontStatus(string.Format(GenerateUI.Tr("FONT_RENAMED", "Renamed to '{0}'."), FontManager.CurrentName));
        } else {
            SetFontStatus(error);
        }
    }
    private static void DeleteCurrentFont() {
        string cur = FontManager.CurrentName;
        if(!FontManager.IsCustomFont(cur)) return;
        if(!fontDeleteArmed) {
            fontDeleteArmed = true;
            fontDeleteBtn.Label.text = GenerateUI.Tr("FONT_DELETE_CONFIRM", "Sure?");
            fontDeleteBtn.RestColor = static () => UIColors.SoftRed;
            fontDeleteBtn.Background.color = UIColors.SoftRed;
            return;
        }
        if(FontManager.DeleteFont(cur)) {
            fontDropdown.SetValues(BuildFontValues());
            fontDropdown.Set(FontManager.CurrentName, false);
            RefreshFontManageRow();
            SetFontStatus(string.Format(GenerateUI.Tr("FONT_DELETED", "Deleted '{0}'."), cur));
        }
    }
    private static void RefreshFontManageRow() {
        if(fontManageRow == null) return;
        bool custom = FontManager.IsCustomFont(FontManager.CurrentName);
        fontManageRow.SetActive(custom);
        fontDeleteArmed = false;
        if(fontDeleteBtn != null) {
            fontDeleteBtn.Label.text = GenerateUI.Tr("FONT_DELETE", "Delete");
            if(fontDeleteRestColor != null) {
                fontDeleteBtn.RestColor = fontDeleteRestColor;
                fontDeleteBtn.Background.color = fontDeleteRestColor();
            }
        }
        if(custom) {
            pendingFontName = FontManager.CurrentName;
            fontRenameInput?.Set(FontManager.CurrentName, false);
        }
        if(pageContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(pageContent);
    }
    private static void SetFontStatus(string message) {
        if(fontStatusText == null || fontStatusRow == null) return;
        fontStatusText.text = message ?? "";
        fontStatusRow.SetActive(!string.IsNullOrEmpty(message));
        if(pageContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(pageContent);
    }
    internal static void ScrollToUpdates() {
        if(scrollController == null || updatesAnchor == null || pageContent == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(pageContent);
        float top = -updatesAnchor.anchoredPosition.y - (updatesAnchor.rect.height * 0.5f);
        scrollController.ScrollTo(top - 6f);
    }
    internal static void RefreshUpdates() {
        if(updateStatusText == null || updateActionRow == null || updateButtonRow == null) return;
        UpdateStatus status = UpdateService.Status;
        UpdateInfo info = UpdateService.Available;
        bool available = status == UpdateStatus.Available && info != null;
        bool skipped = status == UpdateStatus.Skipped;
        updateStatusText.text = status switch {
            UpdateStatus.Checking => GenerateUI.Tr("UPDATE_CHECKING", "Checking for updates…"),
            UpdateStatus.UpToDate => GenerateUI.Tr("UPDATE_UP_TO_DATE", "You're up to date."),
            UpdateStatus.Available => GenerateUI.Tr("UPDATE_AVAILABLE", "Update available:"),
            UpdateStatus.Installing => GenerateUI.Tr("UPDATE_DOWNLOADING", "Downloading update…"),
            UpdateStatus.Installed => string.IsNullOrEmpty(UpdateService.Message)
                ? GenerateUI.Tr("UPDATE_INSTALLED", "Update installed — restart the game to apply.")
                : UpdateService.Message,
            UpdateStatus.Skipped => string.Format(
                GenerateUI.Tr("UPDATE_SKIPPED", "Skipped {0} — it won't be offered again."),
                UpdateService.SkippedTag
            ),
            UpdateStatus.Failed => UpdateService.Failure switch {
                UpdateFailure.Network => GenerateUI.Tr("UPDATE_FAILED_NETWORK", "Couldn't reach GitHub — check your connection."),
                UpdateFailure.NotFound => GenerateUI.Tr("UPDATE_FAILED_NOT_FOUND", "Update check failed — release feed not found."),
                UpdateFailure.RateLimited => GenerateUI.Tr("UPDATE_FAILED_RATE_LIMIT", "GitHub rate limit reached — try again later."),
                UpdateFailure.InstallError => GenerateUI.Tr("UPDATE_FAILED_INSTALL", "Install failed."),
                _ => GenerateUI.Tr("UPDATE_FAILED_CHECK", "Update check failed."),
            },
            _ => "",
        };
        float progress = UpdateService.Progress;
        bool showProgress = status == UpdateStatus.Installing && progress >= 0f;
        updateProgressRow.SetActive(showProgress);
        if(showProgress) {
            float p = Mathf.Clamp01(progress);
            updateProgressFill.anchorMax = new(Mathf.Max(p, 0.03f), 1f);
            updateProgressLabel.text = $"{Mathf.RoundToInt(p * 100f)}%";
        }
        updateActionRow.SetActive(available);
        updateButtonRow.SetActive(available || skipped);
        updateNotesButton?.Rect.gameObject.SetActive(available);
        updateSkipButton?.Rect.gameObject.SetActive(available);
        updateInstallButton?.Rect.gameObject.SetActive(available);
        updateUndoButton?.Rect.gameObject.SetActive(skipped);
        if(available && info != null) {
            string arrow = HasGlyph('→') ? "→" : ">";
            string simulated = UpdateService.DevSimulate
                ? $" {GenerateUI.Tr("UPDATE_SIMULATED", "(simulated)")}"
                : "";
            updateVersionText.text = $"v{Info.DisplayVersion}  {arrow}  {info.Tag}{simulated}";
        } else {
            updateVersionText.text = "";
        }
        if(updateCheckButton != null) updateCheckButton.SetBlocked(status is UpdateStatus.Checking or UpdateStatus.Installing);
    }
    private static bool HasGlyph(char c) {
        try {
            return FontManager.Current != null && FontManager.Current.HasCharacter(c, true, true);
        } catch {
            return false;
        }
    }
    internal static void OnTranslatorLoadEnd() {
        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        languageDropdown.SetValues(langs);
        languageDropdown.Set(
            string.IsNullOrWhiteSpace(MainCore.Conf.Language)
                ? Translator.FALLBACK_LANGUAGE
                : MainCore.Conf.Language,
            false
        );
    }
}
