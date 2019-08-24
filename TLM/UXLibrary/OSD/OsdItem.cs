using ColossalFramework.UI;
using UnityEngine;

namespace UXLibrary.OSD {
    /// <summary>
    /// An object in OSD panel, text label, or keyboard shortcut, etc.
    /// </summary>
    public abstract class OsdItem {
        /// <summary>
        /// Add required objects to the parent panel, return new horizontal offset
        /// </summary>
        /// <param name="parent">Where to add</param>
        /// <param name="position">Horizontal (X) offset</param>
        /// <returns>New offset after items were added</returns>
        public abstract Vector2 AddTo(UIPanel parent, Vector2 position);
    }
}