using System;
using System.Collections.Generic;
using ColossalFramework;
using JetBrains.Annotations;
using UnityEngine;
using UXLibrary.Client;
using UXLibrary.OSD;

namespace UXLibrary {
    /// <summary>
    /// A root object which contains everything that UX Library needs to function.
    /// Create one <see cref="UxLibrary"/> class object once for your own mod.
    ///
    /// It should be OK to create <see cref="UxLibrary"/> in place every time you need to reset the OSD panel,
    /// but there is time cost to it.
    /// </summary>
    [UsedImplicitly]
    public class UxLibrary {
        internal const string UXMOD_PANEL_NAME = "UXLib_OSD_Panel";
        private const string SETTINGS_FILE = "UXLib_Settings";

        /// <summary>
        /// Client mod using the <see cref="UXLibrary"/> gives their custom string for use as config
        /// filename prefix. Setting this to empty disables the keybinds config.
        /// The config filename becomes "UXMod_${prefix}_Keybinds.cgs" in
        /// "AppData/Local/Colossal Order/Cities_Skylines"
        /// </summary>
        internal readonly string OwnerName;

        internal readonly ConfigFile KeybindsConf;
        internal readonly ConfigFile UxmodConf;

        internal readonly Action<string> Log;
        internal Func<string, string> TranslateFun { get; }

        /// <summary>
        /// The scene graph is searched for the UXMod panel, if necessary we can also create and
        /// manage it if no other mod did.
        /// The panel variable can be in 2 states:
        /// 1. Panel is found and is owned by someone else (GameObject)
        /// 2. Panel is created and owned by us (OSDPanel class)
        /// </summary>
        private readonly Either<GameObject, OnScreenDisplayPanel> panel_;

        internal SavedBool OsdPanelVisible;
        internal SavedInt OsdPanelX;
        internal SavedInt OsdPanelY;

        /// <summary>
        /// Construct a <see cref="UXLibrary"/> with name of the owner mod and with function to log messages.
        /// </summary>
        /// <param name="ownerName">Name of the mod that is using the library</param>
        /// <param name="logFun"></param>
        public UxLibrary(string ownerName, Action<string> logFun, Func<string, string> translateFun) {
            OwnerName = ownerName;
            Log = logFun;
            TranslateFun = translateFun;

            KeybindsConf = new ConfigFile(this, $"{OwnerName}_Keybinds");

            UxmodConf = new ConfigFile(this, SETTINGS_FILE);
            OsdPanelVisible = new SavedBool("OsdPanelVisible", UxmodConf.ConfName, true);
            OsdPanelX = new SavedInt("OsdPanelX", UxmodConf.ConfName, 0);
            OsdPanelY = new SavedInt("OsdPanelY", UxmodConf.ConfName, 0);

            // Look for the panel by name
            // Store if found, create own if not found
            GameObject o = GameObject.Find(UXMOD_PANEL_NAME);
            panel_ = o != null
                         ? new Either<GameObject, OnScreenDisplayPanel>(o)
                         : new Either<GameObject, OnScreenDisplayPanel>(
                             new OnScreenDisplayPanel(this));
        }

        /// <summary>
        /// Set up a new text and actions to be displayed on the OSD panel
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public OsdConfigurator Osd() {
            return new OsdConfigurator(this);
        }

        /// <summary>
        /// Called from OSD configurator to deliver the command to the panel.
        /// The command is handled by the copy of UXLibrary which created the panel.
        /// </summary>
        internal void SendUpdateOsdMessage(List<List<object>> commands) {
            if (panel_.IsLeft) {
                // Foreign-owned panel, maybe created by some other mod
                panel_.Left.SendMessage(
                    "UpdateOsd",
                    commands,
                    SendMessageOptions.DontRequireReceiver);
            } else {
                // This panel is owned by this mod
                panel_.Right.Reprogram(commands);
            }
        }
    }
}
