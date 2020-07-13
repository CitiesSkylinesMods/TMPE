namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.Util;

    public class ParkingRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ParkingRestrictions;

        public override void SetupButtonSkin(HashSet<U.AtlasSpriteDef> spriteDefs) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.ButtonSkin {
                Prefix = "ParkingRestrictions",
                BackgroundPrefix = "RoundButton",
                BackgroundHovered = true,
                BackgroundActive = true,
                ForegroundActive = true,
            };
            spriteDefs.AddRange(this.Skin.CreateAtlasSpriteSet(new IntVector2(50)));
        }

        protected override string U_OverrideTooltipText() => Translation.Menu.Get("Tooltip:Parking restrictions");

        protected override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.parkingRestrictionsEnabled;
    }
}
