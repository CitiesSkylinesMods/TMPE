using ColossalFramework.UI;
using System;
using TrafficManager.UI.Texture;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
    public abstract class MenuButton : LinearSpriteButton {
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
            ParkingRestrictions
        }

        private const string MENU_BUTTON = "TMPE_MenuButton";

        public override void HandleClick(UIMouseEventParameter p) { }

        protected override void OnClick(UIMouseEventParameter p) {
            OnClickInternal(p);
            foreach (MenuButton button in LoadingExtension.BaseUI.MainMenu.Buttons) {
                button.UpdateProperties();
            }
        }

        public abstract void OnClickInternal(UIMouseEventParameter p);

        public abstract ButtonFunction Function { get; }

        public override bool CanActivate() {
            return true;
        }

        public override string ButtonName => MENU_BUTTON;

        public override string FunctionName => Function.ToString();

        public override string[] FunctionNames {
            get {
                var functions = Enum.GetValues(typeof(ButtonFunction));
                string[] ret = new string[functions.Length];
                for (int i = 0; i < functions.Length; ++i) {
                    ret[i] = functions.GetValue(i).ToString();
                }
                return ret;
            }
        }

        public override Texture2D AtlasTexture => TextureResources.MainMenuButtonsTexture2D;

        public override int Width => 50;

        public override int Height => 50;
    }
}