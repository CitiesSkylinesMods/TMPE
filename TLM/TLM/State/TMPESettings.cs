namespace TrafficManager.State;
using ColossalFramework.UI;
using CSUtil.Commons;
using System;
using System.Reflection;
using TrafficManager.Lifecycle;
using TrafficManager.UI;
using TrafficManager.UI.Helpers;
using TrafficManager.Util;

public static class TMPESettings {
    public static void MakeSettings(UIHelper helper) {
        Log.Info("TMPESettings.MakeSettings() - Adding UI to mod options tabs");

        try {
            ExtUITabstrip tabStrip = ExtUITabstrip.Create(helper);
            GeneralTab.MakeSettings_General(tabStrip);
            GameplayTab.MakeSettings_Gameplay(tabStrip);
            PoliciesTab.MakeSettings_VehicleRestrictions(tabStrip);
            OverlaysTab.MakeSettings_Overlays(tabStrip);
            MaintenanceTab.MakeSettings_Maintenance(tabStrip);
            KeybindsTab.MakeSettings_Keybinds(tabStrip);
            tabStrip.Invalidate();
        } catch (Exception ex) {
            ex.LogException();
        }
    }

    /// <summary>
    /// If the game is not loaded and warn is true, will display a warning about options being
    /// local to each savegame.
    /// </summary>
    /// <param name="warn">Whether to display a warning popup</param>
    /// <returns>The game is loaded</returns>
    internal static bool IsGameLoaded(bool warn = true) {
        if (TMPELifecycle.InGameOrEditor()) {
            return true;
        }

        if (warn) {
            Prompt.Warning(
                "Nope!",
                Translation.Options.Get("Dialog.Text:Settings are stored in savegame")
                + " https://github.com/CitiesSkylinesMods/TMPE/wiki/Settings");
        }

        return false;
    }


    /// <summary>
    /// Inform the main Options window of the C:S about language change. This should rebuild the
    /// options tab for TM:PE.
    /// </summary>
    public static void RebuildOptions() {
        // Inform the Main Options Panel about locale change, recreate the categories
        MethodInfo onChangedHandler = typeof(OptionsMainPanel)
            .GetMethod(
                "OnLocaleChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
        if (onChangedHandler == null) {
            Log.Error("Cannot rebuild options panel, OnLocaleChanged handler is null");
            return;
        }

        Log._Debug("Informing the main OptionsPanel about the locale change...");
        onChangedHandler.Invoke(
            UIView.library.Get<OptionsMainPanel>("OptionsPanel"),
            new object[] { });
    }
}
