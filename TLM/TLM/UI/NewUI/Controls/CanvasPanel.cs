namespace TrafficManager.UI.NewUI.Controls {
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.UI;

    public class CanvasPanel : Graphic {
        private static LayoutElement layoutElement_;
        private static RectTransform rectTransform_;

        /// <summary>
        /// Place a panel on the form
        /// </summary>
        /// <param name="parent">Used for nesting controls, can be NULL to nest under the
        ///     canvas itself.</param>
        /// <param name="panelName">Control name in Unity Scene Tree</param>
        /// <returns>The new canvas panel component</returns>
        public static CanvasPanel Create([NotNull]
                                         GameObject parent,
                                         string panelName) {
            var panelObject = new GameObject(panelName);

            rectTransform_ = panelObject.AddComponent<RectTransform>();
            layoutElement_ = panelObject.AddComponent<LayoutElement>();

            var panel = panelObject.AddComponent<CanvasPanel>();

            panelObject.transform.SetParent(
                parent.transform,
                false);

            return panel;
        }

        /// <summary>
        /// See https://docs.unity3d.com/Manual/script-LayoutElement.html for preferred, minimum and
        /// flexible sizes
        /// </summary>
        /// <param name="val">Value to set</param>
        /// <returns>This</returns>
        public CanvasPanel PreferredHeight(float val) {
            layoutElement_.minHeight = val;
            layoutElement_.preferredHeight = val;
            return this;
        }

        protected override void Start() {
            // 100% translucent (invisible)
            this.color = new Color(0f, 1f, 0f, 0.3f);
        }
    }

}