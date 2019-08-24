using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;

namespace UXLibrary.Keyboard {
    using System;
    using ColossalFramework.Globalization;

    /// <summary>
    /// General input key handling functions, checking for empty, converting to string etc.
    /// </summary>
    public class Keybind {
        public static bool IsEmpty(InputKey sample) {
            var noKey = SavedInputKey.Encode(KeyCode.None, false, false, false);
            return sample == SavedInputKey.Empty || sample == noKey;
        }

        /// <summary>
        /// Returns shortcut as a string in user's language. Modify for special handling.
        /// </summary>
        /// <param name="k">The key</param>
        /// <returns>The shortcut, example: "Ctrl + Alt + H"</returns>
        // Copied from SavedInputKey disassembly
        public static string ToLocalizedString(SavedInputKey k, Func<string, string> translateFun) {
            // return k.ToLocalizedString("KEYNAME");
            const string LOCALE_ID = "KEYNAME";
            string str = string.Empty;
            if (k.Control) {
                str = str + Locale.Get(LOCALE_ID, KeyCode.LeftControl.ToString()) + " + ";
            }

            if (k.Alt) {
                str = str + Locale.Get(LOCALE_ID, KeyCode.LeftAlt.ToString()) + " + ";
            }

            if (k.Shift) {
                str = str + Locale.Get(LOCALE_ID, KeyCode.LeftShift.ToString()) + " + ";
            }

            // For some keys return shorter names, for example "Primary Mouse Button" to be replaced
            // with "Click"
            string keyString = MaybeOverrideKeyName(
                k.Key,
                UnityReportingWrongKeysHack(k.Key),
                translateFun);

            return str + (Locale.Exists(LOCALE_ID, keyString)
                              ? Locale.Get(LOCALE_ID, keyString)
                              : keyString);
        }

        private static string MaybeOverrideKeyName(KeyCode k,
                                                   string otherwiseDefault,
                                                   Func<string, string> translateFun) {
            switch (k) {
                case KeyCode.Mouse0: return translateFun("UXLib.Keybind.Click");
                case KeyCode.Mouse1: return translateFun("UXLib.Keybind.RightClick");
                case KeyCode.Mouse2: return translateFun("UXLib.Keybind.MiddleClick");
            }
            return otherwiseDefault;
        }

        // Copied from SavedInputKey disassembly
        private static string UnityReportingWrongKeysHack(KeyCode code) {
            if (!IsCommandKey(code)) {
                return code.ToString();
            }

            switch (Application.platform) {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer: {
                    return KeyCode.LeftCommand.ToString();
                }
                default: {
                    return KeyCode.LeftWindows.ToString();
                }
            }
        }

        // Copied from SavedInputKey disassembly
        private static bool IsCommandKey(KeyCode code) {
            if (code != KeyCode.LeftCommand && code != KeyCode.RightCommand &&
                (code != KeyCode.LeftCommand && code != KeyCode.RightCommand) &&
                code != KeyCode.LeftWindows) {
                return code == KeyCode.RightWindows;
            }

            return true;
        }

        public static bool IsModifierKey(KeyCode code) {
            return code == KeyCode.LeftControl || code == KeyCode.RightControl ||
                   code == KeyCode.LeftShift || code == KeyCode.RightShift ||
                   code == KeyCode.LeftAlt || code == KeyCode.RightAlt;
        }

        public static bool IsControlDown() {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        public static bool IsShiftDown() {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        public static bool IsAltDown() {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        public static bool IsUnbindableMouseButton(UIMouseButton code) {
            return code == UIMouseButton.Left || code == UIMouseButton.Right;
        }

        public static KeyCode ButtonToKeycode(UIMouseButton button) {
            if (button == UIMouseButton.Left) {
                return KeyCode.Mouse0;
            }

            if (button == UIMouseButton.Right) {
                return KeyCode.Mouse1;
            }

            if (button == UIMouseButton.Middle) {
                return KeyCode.Mouse2;
            }

            if (button == UIMouseButton.Special0) {
                return KeyCode.Mouse3;
            }

            if (button == UIMouseButton.Special1) {
                return KeyCode.Mouse4;
            }

            if (button == UIMouseButton.Special2) {
                return KeyCode.Mouse5;
            }

            if (button == UIMouseButton.Special3) {
                return KeyCode.Mouse6;
            }

            return KeyCode.None;
        }
    }
}