namespace TrafficManager.U {
    using JetBrains.Annotations;
    using UnityEngine;

    /// <summary>
    /// A free floating UI label which follows the mouse and displays current tool mode.
    /// </summary>
    public class UFloatingTooltip : ULabel {
        public override void Awake() {
            base.Awake();
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(64, 64, 64, 240);
        }

        public void SetTooltip([CanBeNull]
                               string t = null) {
            if (string.IsNullOrEmpty(t)) {
                this.text = string.Empty;
                this.Hide();
            } else {
                this.text = t;
                this.Show();
            }
        }

        private void UpdateTooltipPosition() {
            Vector2 pos = UIScaler.MousePosition;
            pos += new Vector2(16f, 24f); // offset slightly below and to the right of mouse
            this.absolutePosition = pos;
        }

        public override void Update() {
            base.Update();
            if (this.isVisible) {
                this.UpdateTooltipPosition();
            }
        }
    }
}