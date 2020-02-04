namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using System;
    using TrafficManager.UI.Textures;
    using UnityEngine;

    /// <summary>Base class for main menu panel buttons.</summary>
    public abstract class MenuButton : LinearSpriteButton {
        /// <summary>
        /// Defines tool types for TM:PE. Modes are exclusive, one can be active at a time.
        /// </summary>
        public enum ButtonFunction {
            LaneConnector,
            ClearTraffic,
            DespawnDisabled,
            DespawnEnabled,
            JunctionRestrictions,
            LaneArrows,
            ManualTrafficLights,
            PrioritySigns,
            SpeedLimits,
            TimedTrafficLights,
            ToggleTrafficLights,
            VehicleRestrictions,
            ParkingRestrictions,
        }

        /// <summary>Menu button gameobject name.</summary>
        private const string MENU_BUTTON = "TMPE_MenuButton";

        public override void HandleClick(UIMouseEventParameter p) { }

        protected override void OnClick(UIMouseEventParameter p) {
            OnClickInternal(p);
            foreach (MenuButton button in LoadingExtension.BaseUI.MainMenu.Buttons) {
                button.UpdateProperties();
            }
        }

        public abstract void OnClickInternal(UIMouseEventParameter p);

        protected abstract ButtonFunction Function { get; }

        public override bool CanActivate() {
            return true;
        }

        public override string ButtonName => MENU_BUTTON;

        public override string FunctionName => Function.ToString();

        public override string[] FunctionNames {
            get {
                Array functions = Enum.GetValues(typeof(ButtonFunction));
                var ret = new string[functions.Length];

                for (int i = 0; i < functions.Length; ++i) {
                    ret[i] = functions.GetValue(i).ToString();
                }

                return ret;
            }
        }

        public override Texture2D AtlasTexture => Textures.MainMenu.MainMenuButtons;

        public override int Width => 50;

        public override int Height => 50;
    }
}