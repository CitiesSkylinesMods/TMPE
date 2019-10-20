namespace TrafficManager.U.Controls {
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.U.Events;
    using UnityEngine;
    using UnityEngine.UI;

    public class UPanel : Graphic {
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
        public static UPanel Create([NotNull] GameObject parentObject,
                                    UWindow parentWindow,
                                    string panelName) {
            var panelObject = new GameObject(panelName);

            var panel = panelObject.AddComponent<UPanel>();
            panel.rectTransform_ = panelObject.AddComponent<RectTransform>();
            panel.layoutElement_ = panelObject.AddComponent<LayoutElement>();
            panel.parentWindow_ = parentWindow;

            panelObject.transform.SetParent(parentObject.transform, false);

            return panel;
        }

        /// <summary>
        /// See https://docs.unity3d.com/Manual/script-LayoutElement.html for preferred, minimum and
        /// flexible sizes
        /// </summary>
        /// <param name="val">Value to set</param>
        /// <returns>This</returns>
        public UPanel PreferredHeight(float val) {
            this.layoutElement_.minHeight = val;
            this.layoutElement_.preferredHeight = val;
            return this;
        }

        public void EnableDrag() {
            var component = this.gameObject.AddComponent<UDragHandler>();
            component.DragWindow = this.parentWindow_;
            Log.Info("Drag Enabled");
        }

        protected override void Start() {
            // 100% translucent (invisible)
            this.color = new Color(0f, 1f, 0f, 0.3f);
        }
    }
}