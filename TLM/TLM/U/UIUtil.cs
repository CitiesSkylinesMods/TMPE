namespace TrafficManager.U {
    using System.Collections.Generic;
    using System.Linq;
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
    }
}