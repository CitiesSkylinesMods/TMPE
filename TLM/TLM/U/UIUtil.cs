namespace TrafficManager.U {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
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
        public static bool ClampToScreen(UIComponent window, [NotNull] UIComponent alwaysVisible) {
            Rect origRect = new Rect(
                position: alwaysVisible.absolutePosition,
                size: alwaysVisible.size);

            Rect clampedRect = new Rect(origRect);
            Vector2 resolution = UIScaler.BaseResolution;

            VectorUtil.ClampRectToScreen(
                rect: ref clampedRect,
                resolution: resolution);

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

        /// <summary>
        /// Replace special markup with UI shortcut color tag (orange). And place closing tag.
        /// NOTE: You can use Translation.`DictionaryName`.ColorizeKeybind, to translate a string AND
        ///     colorize at the same time.
        /// </summary>
        /// <param name="s">String with keybind wrapped in [[Ctrl]] double square brackets.</param>
        /// <returns>Updated string.</returns>
        public static string ColorizeKeybind([NotNull] string s) {
            return s.Replace("[[", UConst.GetKeyboardShortcutColorTagOpener())
                    .Replace("]]", UConst.KEYBOARD_SHORTCUT_CLOSING_TAG);
        }

        /// <summary>
        /// Replace special markup with UI shortcut color tag (orange).
        /// Replace wrapped text from 'replacements' of matching index.
        /// And place closing tag.
        /// </summary>
        /// <param name="sourceText">String with keybind wrapped in [[Ctrl]] double square brackets.</param>
        /// <param name="replacements">Replacements array, each tag match will take own replacement</param>
        /// <returns>Updated string.</returns>
        public static string ColorizeDynamicKeybinds([NotNull] string sourceText, string[] replacements) {
            int i = 0;
            return new Regex(@"\[\[(.*?)\]\]")
                .Replace(
                    sourceText,
                    new MatchEvaluator(
                        match => i < replacements.Length
                                     ? $"{UConst.GetKeyboardShortcutColorTagOpener()}{replacements[i++]}{UConst.KEYBOARD_SHORTCUT_CLOSING_TAG}"
                                     : match.Value));
        }
    }
}