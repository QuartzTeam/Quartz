using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal sealed class TufSettingsView : MonoBehaviour {
    private TufService service;
    private TMP_Text folderValue;
    private TMP_Text status;
    private RectTransform linkedNoteRow;
    private string notice = "";
    private bool pendingRefresh;
    private void OnEnable() {
        if(!pendingRefresh) return;
        pendingRefresh = false;
        Refresh();
    }
    public void Bind(TufService service) {
        this.service = service;
        service.Changed += Refresh;
        Refresh();
    }
    public void SetLabels(TMP_Text folderValue, TMP_Text status, RectTransform linkedNoteRow) {
        this.folderValue = folderValue;
        this.status = status;
        this.linkedNoteRow = linkedNoteRow;
    }
    public void ShowNotice(string message) {
        notice = message ?? "";
        Refresh();
    }
    public void ClearNotice() => notice = "";
    public void Refresh() {
        if(service == null) return;
        if(!gameObject.activeInHierarchy) {
            pendingRefresh = true;
            return;
        }
        linkedNoteRow?.gameObject.SetActive(service.LinkTufHelperLite
            && !string.IsNullOrEmpty(service.CustomLevelsRoot));
        if(folderValue != null) folderValue.text = service.ActiveRootPath;
        if(status == null) return;
        if(notice.Length > 0) {
            status.gameObject.SetActive(true);
            status.color = new(1f, 0.72f, 0.35f, 0.9f);
            status.text = notice;
            return;
        }
        switch(service.MoveState) {
            case TufMoveState.Moving:
                status.gameObject.SetActive(true);
                status.color = new(1f, 1f, 1f, 0.6f);
                status.text = string.Format(
                    MainCore.Tr.Get("TUF_MOVE_PROGRESS", "Moving levels to the new folder… {0}/{1}"),
                    service.MoveDone, service.MoveTotal);
                break;
            case TufMoveState.Failed:
                status.gameObject.SetActive(true);
                status.color = new(1f, 0.72f, 0.35f, 0.9f);
                status.text = service.MoveError;
                break;
            case TufMoveState.Done:
                status.gameObject.SetActive(service.MoveTotal > 0);
                status.color = new(0.62f, 0.92f, 0.72f, 0.9f);
                status.text = string.Format(
                    MainCore.Tr.Get("TUF_MOVE_DONE", "Moved {0} level(s) to the new folder."), service.MoveTotal);
                break;
            default:
                status.gameObject.SetActive(false);
                break;
        }
    }
    private void OnDestroy() {
        if(service != null) service.Changed -= Refresh;
    }
}
internal static class TufSettingsUI {
    public static void Create(RectTransform parent) {
        RectTransform content = PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "TUF_SETTINGS", "Settings");
        TufService service = TufService.Instance;
        if(service == null) return;
        TufSettingsView view = parent.gameObject.AddComponent<TufSettingsView>();
        bool helperInstalled = TufHelperLiteLink.Installed;
        if(helperInstalled)
            GenerateUI.ToggleTip(
                content.transform,
                false,
                service.LinkTufHelperLite,
                service.SetLinkTufHelperLite,
                "Link to TUFHelperLite Directories",
                "tuf_link_helper",
                "Saves downloaded levels into TUFHelperLite's Downloads folder (as tuf-<id>) so both mods share one level library. Inside the game folder, UMMMods is checked first, then Mods."
            );
        GenerateUI.ToggleTip(
            content.transform,
            true,
            service.ShowPreviews,
            service.SetShowPreviews,
            "Level Previews",
            "tuf_previews",
            "Show a blurred preview image behind each level in the browser, taken from its YouTube video. Turn off to skip the thumbnail downloads."
        );
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)),
            "TUF_LIBRARY", "Level Library");
        RectTransform pathRow = GenerateUI.Row(content.transform, 34f);
        TMP_Text pathText = GenerateUI.AddMutedText(pathRow, 15f);
        pathText.text = service.ActiveRootPath;
        UIButton openFolderBtn = GenerateUI.Button(
            GenerateUI.Row(content.transform),
            () => {
                try {
                    string path = service.ActiveRootPath;
                    System.IO.Directory.CreateDirectory(path);
                    FileDialog.Reveal(path);
                } catch(System.Exception e) {
                    MainCore.Log.Err($"[TufSettingsUI] Reveal failed: {e}");
                }
            },
            "Open Levels Folder",
            "tuf_open_folder"
        ).SetSecondary();
        openFolderBtn.Rect.AddToolTip(
            "DESC_TUF_OPEN_FOLDER",
            "Opens the folder where downloaded TUF levels are saved."
        );
        UIButton pickBtn = GenerateUI.Button(
            GenerateUI.Row(content.transform),
            () => PickFolder(service, view),
            "Change Levels Folder",
            "tuf_pick_folder"
        ).SetSecondary();
        pickBtn.Rect.AddToolTip(
            "DESC_TUF_PICK_FOLDER",
            "Choose an empty folder — on a roomier drive, for instance — to keep downloaded levels in. Levels you already have are moved there."
        );
        UIButton resetBtn = GenerateUI.Button(
            GenerateUI.Row(content.transform),
            () => {
                view.ClearNotice();
                if(!service.ClearCustomLevelsRoot(out string reason)) view.ShowNotice(RejectionMessage(reason));
                else view.Refresh();
            },
            "Use the Default Folder",
            "tuf_reset_folder"
        ).SetSecondary();
        resetBtn.Rect.AddToolTip(
            "DESC_TUF_RESET_FOLDER",
            "Moves levels back into Quartz's own folder inside the mod directory."
        );
        RectTransform linkedNoteRow = null;
        if(helperInstalled) {
            linkedNoteRow = GenerateUI.Row(content.transform, 40f);
            GenerateUI.AddLocalizedMutedText(
                linkedNoteRow,
                "TUF_LIBRARY_LINK_OVERRIDE",
                "The TUFHelperLite link above is on, so levels install there and this folder is not in use.",
                15f
            ).color = new Color(1f, 0.72f, 0.35f, 0.9f);
        }
        RectTransform statusRow = GenerateUI.Row(content.transform, 44f);
        TMP_Text statusText = GenerateUI.AddMutedText(statusRow, 15f);
        statusText.text = "";
        view.SetLabels(pathText, statusText, linkedNoteRow);
        view.Bind(service);
    }
    private static void PickFolder(TufService service, TufSettingsView view) {
        string picked;
        try {
            picked = FileDialog.PickFolder(
                service.ActiveRootPath, null, null, MainCore.Tr.Get("TUF_PICK_FOLDER_TITLE", "Choose a levels folder"));
        } catch(System.Exception e) {
            MainCore.Log.Err($"[TufSettingsUI] folder picker failed: {e}");
            return;
        }
        if(string.IsNullOrEmpty(picked)) return;
        view.ClearNotice();
        if(!service.SetCustomLevelsRoot(picked, out string reason)) {
            MainCore.Log.Wrn($"[TUF] rejected levels folder '{picked}': {reason}");
            view.ShowNotice(RejectionMessage(reason));
            return;
        }
        view.Refresh();
    }
    private static string RejectionMessage(string reason) => reason switch {
        "linked" => MainCore.Tr.Get("TUF_FOLDER_LINKED",
            "Turn off the TUFHelperLite link first — while it is on, that mod's folder is where levels install."),
        "not-empty" => MainCore.Tr.Get("TUF_FOLDER_NOT_EMPTY",
            "Pick an empty folder. Quartz manages everything inside the levels folder, so it will not take over one that already holds your files."),
        "volume-root" => MainCore.Tr.Get("TUF_FOLDER_VOLUME_ROOT",
            "Pick a folder on the drive, not the drive itself."),
        "nested" => MainCore.Tr.Get("TUF_FOLDER_NESTED",
            "That folder is inside Quartz's own levels folder."),
        "busy" => MainCore.Tr.Get("TUF_FOLDER_BUSY", "Levels are still moving; wait for that to finish."),
        "symlink" => MainCore.Tr.Get("TUF_FOLDER_SYMLINK", "That folder is a link. Pick a real folder."),
        _ => MainCore.Tr.Get("TUF_FOLDER_UNUSABLE", "That folder cannot be used for levels."),
    };
}
