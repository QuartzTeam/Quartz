using HarmonyLib;
using Quartz.Compat.Game;
namespace Quartz.Features.Calibration;
internal static class CalibrationFloatOffset {
    [HarmonyPatch(typeof(scrConductor), "get_calibration_i")]
    private static class GetCalibrationIPatch {
        private static bool Prefix(ref float __result) {
            if(!Calibration.FloatOffsetEnabled) return true;
            __result = Calibration.GetOffsetMs() / 1000f;
            return false;
        }
    }
    [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.UpdateSetting))]
    private static class UpdateSettingPatch {
        private static bool Prefix(ref PauseSettingButton ___offsetButton, PauseSettingButton setting, SettingsMenu.Interaction action) {
            if(!Calibration.FloatOffsetEnabled) return true;
            if(setting.name != "inputOffset"
                || action is SettingsMenu.Interaction.ActivateInfo or SettingsMenu.Interaction.Activate) return true;
            ___offsetButton = setting;
            if(action == SettingsMenu.Interaction.Refresh) {
                setting.CachedValue = null;
                setting.initialValue = Calibration.GetOffsetMs();
            } else {
                float offset = Calibration.GetOffsetMs();
                float increment = 10f;
                if(RDInput.holdingShift) increment /= 10f;
                if(RDInput.holdingControl) increment /= 100f;
                if(action == SettingsMenu.Interaction.Increment) {
                    offset += increment;
                    setting.PlayArrowAnimation(true);
                } else if(action == SettingsMenu.Interaction.Decrement) {
                    offset -= increment;
                    setting.PlayArrowAnimation(false);
                }
                scrController.instance.pauseMenu.PlayMenuSfx(SfxSound.MenuSquelch, 1.5f);
                Calibration.SetOffsetMs(offset);
            }
            SetOffsetLabel(setting);
            return false;
        }
    }
    [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.Show))]
    private static class ShowPatch {
        private static void Prefix(PauseSettingButton ___offsetButton) {
            if(!___offsetButton) return;
            if(Calibration.FloatOffsetEnabled) SetOffsetLabel(___offsetButton);
            else ___offsetButton.valueLabel.text = scrConductor.currentPreset.inputOffset + GameApi.GameString("editor.unit." + ___offsetButton.unit);
        }
    }
    private static void SetOffsetLabel(PauseSettingButton setting) =>
        setting.valueLabel.text = Calibration.FormatMs(Calibration.GetOffsetMs()) + GameApi.GameString("editor.unit." + setting.unit);
}
