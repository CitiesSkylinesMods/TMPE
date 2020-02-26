namespace TrafficManager.U {
    using ColossalFramework.UI;
    using UnityEngine;

    /// <summary>
    /// Code for generic management of UI controls and gameobjects.
    /// </summary>
    public static class UIUtil {
        /// <summary>
        /// Delete all gameobjects under CO.UI UIView, which have `name`, and set name for the object.
        /// </summary>
        /// <param name="obj">Object to become unique.</param>
        /// <param name="name">Object name.</param>
        public static void MakeUniqueAndSetName(GameObject obj, string name) {
            var uiView = UIView.GetAView();

            GameObject found = GameObject.Find(name);
            if (found != null) {
                UnityEngine.Object.Destroy(found);
            }

            obj.name = name;
        }
    }
}