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
                button.UpdateProperties();
            }
        }

        public abstract void OnClickInternal(UIMouseEventParameter p);

        protected abstract ButtonFunction Function { get; }

        public override bool CanActivate() {
            return true;
        }

        public override string ButtonName => MENU_BUTTON;

        public override string FunctionName => Function.Name;

        // public override string[] FunctionNames {
        //     get {
        //         Array functions = Enum.GetValues(typeof(ButtonFunction.ButtonFunctionEnum));
        //         var ret = new string[functions.Length];
        //
        //         for (int i = 0; i < functions.Length; ++i) {
        //             ret[i] = functions.GetValue(i).ToString();
        //         }
        //
        //         return ret;
        //     }
        // }

        // public override Texture2D AtlasTexture => UI.Textures.MainMenu.MainMenuButtons;

        public override int GetWidth() => 50;

        public override int GetHeight() => 50;
    }
}