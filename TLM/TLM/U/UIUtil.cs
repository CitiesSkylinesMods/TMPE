namespace TrafficManager.U {
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UnityEngine;

    /// <summary>
    /// Code for generic management of UI controls and gameobjects.
    /// </summary>
    public static class UIUtil {
        /// <summary>
        /// Delete all gameobjects under CO.UI UIView, which have `name`, and set name for the object.
        /// </summary>
        /// <param name="toMakeUnique">Object to become unique.</param>
        /// <param name="name">Object name.</param>
        public static void MakeUniqueAndSetName(GameObject toMakeUnique, string name) {
            toMakeUnique.name = $"{name}_temporary_placeholder";

            IEnumerable<GameObject> objects
                = Resources.FindObjectsOfTypeAll<GameObject>()
                           .Where(obj => obj.name == name);

            foreach (GameObject found in objects) {
                found.name = $"{name}_destroyed";
                // found.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(found.gameObject);
            }

            toMakeUnique.name = name;
        }

        /// <summary>
        /// Checks <paramref name="alwaysVisible"/> and if it is anywhere outside the screen, brings
        /// it back into the screen, the <paramref name="window"/> is moved by that delta instead.
        /// This is to be called after the resize, and also after the move.
        /// </summary>
        /// <param name="window">Parent to be moved.</param>
        /// <param name="alwaysVisible">Object to be clamped to screen.</param>
        /// <returns>True if the position changed.</returns>
        public static bool ClampToScreen(UIComponent window, UIComponent alwaysVisible) {
            Rect origRect = new Rect(
                position: alwaysVisible.absolutePosition,
                size: alwaysVisible.size);

            Rect clampedRect = new Rect(origRect);

            VectorUtil.ClampRectToScreen(
                rect: ref clampedRect,
                resolution: new Vector2(UIScaler.GUI_WIDTH, UIScaler.GUI_HEIGHT));

            float xMotion = clampedRect.x - origRect.x;
            float yMotion = clampedRect.y - origRect.y;

            // Nothing to do, return here
            if (Mathf.Approximately(xMotion, 0f) && Mathf.Approximately(yMotion, 0f)) {
                return false;
            }

            // Move the parent window by the difference
            Vector3 pos = window.absolutePosition;
            pos += new Vector3(xMotion, yMotion, 0f);
            window.absolutePosition = pos;

            // Log._Debug(
            //     $"Clamping origRect={origRect} to new={clampedRect} "
            //     + $"moving by {xMotion};{yMotion} newpos={pos}");
            return true;
        }
    }
}