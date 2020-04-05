namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.U.Button;

    /// <summary>
    /// Base class for main menu panel buttons.
    /// </summary>
    public abstract class BaseMenuButton : BaseUButton {
        /// <summary>Menu button gameobject name.</summary>
        private const string MENU_BUTTON = "TMPE_MenuButton";

        /// <summary>
        /// When creating the main panel, texture atlas is created for all buttons. Here
        /// each button is given a chance to add their own required sprites to that atlas.
        /// </summary>
        /// <param name="atlasKeys">List to modify.</param>
        public abstract void SetupButtonSkin(HashSet<string> atlasKeys);

        public override void HandleClick(UIMouseEventParameter p) { }

        protected override void OnClick(UIMouseEventParameter p) {
            OnClickInternal(p);
            ModUI.Instance.MainMenu.UpdateButtons();
        }

        public abstract void OnClickInternal(UIMouseEventParameter p);

        /// <summary>Used to determine which actual button was clicked by the base class.</summary>
        protected abstract ButtonFunction Function { get; }

        public override bool CanActivate() {
            return true;
        }

        public override string ButtonName => MENU_BUTTON;
    }
}