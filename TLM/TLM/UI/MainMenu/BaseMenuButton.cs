namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.U;

    /// <summary>
    /// Base class for main menu panel buttons.
    /// </summary>
    public abstract class BaseMenuButton : U.BaseUButton {
        /// <summary>Menu button gameobject name.</summary>
        private const string MENU_BUTTON = "TMPE_MenuButton";

        /// <summary>
        /// When creating the main panel, texture atlas is created for all buttons. Here
        /// each button is given a chance to add their own required sprites to that atlas.
        /// </summary>
        /// <param name="futureAtlas">This will be populated with required sprite names/paths.</param>
        public abstract void SetupButtonSkin(AtlasBuilder futureAtlas);

        public override void HandleClick(UIMouseEventParameter p) { }

        /// <summary>Handles click. NOTE: When overriding, call base.OnClick() last!</summary>
        /// <param name="p">Event.</param>
        protected override void OnClick(UIMouseEventParameter p) {
            ModUI.Instance.MainMenu.UpdateButtons();
        }

        public override bool CanActivate() {
            return true;
        }
    }
}