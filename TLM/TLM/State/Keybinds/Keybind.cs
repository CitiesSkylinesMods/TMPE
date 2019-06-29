using ColossalFramework;
using UnityEngine;

namespace TrafficManager.State.Keybinds {
    /// <summary>
    /// General input key handling functions, checking for empty, converting to string etc.
    /// </summary>
    public class Keybind {
        public static bool IsEmpty(InputKey sample) {
            var noKey = SavedInputKey.Encode(KeyCode.None, false, false, false);
            return sample == SavedInputKey.Empty
                   || sample == noKey;
        }

        /// <summary>
        /// Returns shortcut as a string in user's language. Modify for special handling.
        /// </summary>
        /// <param name="k">The key</param>
        /// <returns>The shortcut, example: "Ctrl + Alt + H"</returns>
        public static string Str(SavedInputKey k) {
            return k.ToLocalizedString("KEYNAME");
        }
    }
}