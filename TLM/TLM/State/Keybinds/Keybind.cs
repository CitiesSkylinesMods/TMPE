namespace TrafficManager.State.Keybinds {
    using ColossalFramework.UI;
    using ColossalFramework;
    using TrafficManager.UI;
    using UnityEngine;

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
        public static string ToLocalizedString(SavedInputKey k) {
            if (k.value == SavedInputKey.Empty) {
                return Translation.Options.Get("Keybind:None");
            }

            switch (k.Key) {
                case KeyCode.Mouse0:
                    return Translation.Options.Get("Shortcut:Click");
                case KeyCode.Mouse1:
                    return Translation.Options.Get("Shortcut:RightClick");
                case KeyCode.Mouse2:
                    return Translation.Options.Get("Shortcut:MiddleClick");
            }

            return k.ToLocalizedString("KEYNAME");
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