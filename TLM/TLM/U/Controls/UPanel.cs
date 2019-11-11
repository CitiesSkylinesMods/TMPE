namespace TrafficManager.U.Controls {
    using System;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.U.Events;
    using UnityEngine;
    using UnityEngine.UI;

    public class UPanel
        : Graphic {
        private LayoutElement layoutElement_;
        private RectTransform rectTransform_;
        private UWindow parentWindow_;

        /// <summary>
        /// Place a panel on the form
        /// </summary>
        /// <param name="parentObject">Used for nesting controls, can be NULL to nest under the
        ///     canvas itself.</param>
        /// <param name="parentWindow">If drag is enabled, this will receive drag events</param>
        /// <param name="panelName">Control name in Unity Scene Tree</param>
        /// <returns>The new canvas panel component</returns>
        public static GameObject Create([NotNull] UWindow parentWindow,
                                        GameObject parentObject,
                                        string panelName) {
            var panelObject = new GameObject(panelName);

            var panel = panelObject.AddComponent<UPanel>();
            panel.rectTransform_ = panelObject.AddComponent<RectTransform>();
            panel.layoutElement_ = panelObject.AddComponent<LayoutElement>();
            panel.parentWindow_ = parentWindow;

            panelObject.transform.SetParent(parentObject.transform, false);
            
            panelObject.AddComponent<UConstrained>();

            return panelObject;
        }

        public void EnableDrag() {
            var component = this.gameObject.AddComponent<UDragHandler>();
            component.DragWindow = this.parentWindow_;
            Log.Info("Drag Enabled");
        }

        protected override void Start() {
            // Should be 100% translucent (invisible)
            this.color = new Color(0f, 1f, 0f, 1f); // for debug: green
        }
    }
}