namespace TrafficManager.UI.MainMenu {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.U.Button;
    using UnityEngine;

    /// <summary>
    /// Base class for main menu panel buttons.
    /// </summary>
    public abstract class BaseMenuButton : BaseUButton {
        /// <summary>Menu button gameobject name.</summary>
        private const string MENU_BUTTON = "TMPE_MenuButton";

        public override void HandleClick(UIMouseEventParameter p) { }

        protected override void OnClick(UIMouseEventParameter p) {
            OnClickInternal(p);
            foreach (BaseMenuButton button in LoadingExtension.ModUi.MainMenu.Buttons) {
                button.UpdateButtonImageAndTooltip();
            }
        }

        public abstract void OnClickInternal(UIMouseEventParameter p);

        protected abstract ButtonFunction Function { get; }

        public override bool CanActivate() {
            return true;
        }

        public override string ButtonName => MENU_BUTTON;
    }
}