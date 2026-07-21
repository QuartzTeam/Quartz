using Quartz.Core;
using Quartz.UI.Generator;
using Quartz.Update;
using UnityEngine;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal static class PageDeveloper {
    private static TextMeshProUGUI statusText;
    private static bool hooked;
    public static void Create(RectTransform parent) {
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "DEVELOPER", "Developer");
        var updaterSec = GenerateUI.Collapsible(content.transform, "Updater", startExpanded: true);
        GenerateUI.Toggle(
            GenerateUI.Row(updaterSec.Body),
            false,
            UpdateService.DevSimulate,
            v => UpdateService.SetDevSimulate(v),
            "Simulate Update Available",
            "dev_sim_update"
        );
        GenerateUI.Button(
            GenerateUI.Row(updaterSec.Body),
            () => UpdateService.Check(),
            "Run Update Check",
            "dev_check"
        );
        GenerateUI.Button(
            GenerateUI.Row(updaterSec.Body),
            () => {
                MainCore.Conf.SkippedVersion = "";
                MainCore.ConfMgr.RequestSave();
                RefreshStatus();
            },
            "Clear Skipped Version",
            "dev_clear_skip"
        );
        GenerateUI.Button(
            GenerateUI.Row(updaterSec.Body),
            RefreshStatus,
            "Refresh Status",
            "dev_refresh"
        );
        var statusSec = GenerateUI.Collapsible(content.transform, "Status", startExpanded: true);
        statusText = GenerateUI.AddText(GenerateUI.Row(statusSec.Body, 320f));
        statusText.alignment = TextAlignmentOptions.TopLeft;
        TextCompat.Wrap(statusText);
        if(!hooked) {
            UpdateService.OnChanged += RefreshStatus;
            hooked = true;
        }
        RefreshStatus();
    }
    internal static void RefreshStatus() {
        if(statusText == null) return;
        UpdateInfo available = UpdateService.Available;
        string skipped = string.IsNullOrEmpty(MainCore.Conf.SkippedVersion)
            ? "none"
            : MainCore.Conf.SkippedVersion;
        statusText.text = string.Join("\n", new[] {
            $"Version:         v{Info.DisplayVersion}",
            $"Channel:         {Info.ChannelKind}",
            $"Mod enabled:     {MainCore.IsModEnabled}",
            $"Update channel:  {MainCore.Conf.GetUpdateChannel()}",
            $"Update status:   {UpdateService.Status}",
            $"Failure:         {UpdateService.Failure}",
            $"Progress:        {(UpdateService.Progress < 0f ? "n/a" : UpdateService.Progress.ToString("P0"))}",
            $"Available:       {(available == null ? "none" : available.Tag)}",
            $"Skipped version: {skipped}",
            $"Simulate update: {UpdateService.DevSimulate}",
            $"Repo:            {Info.RepoOwner}/{Info.RepoName}",
            $"Last message:    {UpdateService.Message}",
        });
    }
}
