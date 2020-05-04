namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class DespawnButton : BaseMenuButton {
        protected override string U_OverrideTooltipText() =>
            Options.disableDespawning
                ? Translation.Menu.Get("Tooltip:Enable despawning")
                : Translation.Menu.Get("Tooltip:Disable despawning");

        protected override bool IsVisible() => true;

        /// <summary>
        /// Button lights up on despawning enabled (easy mode).
        /// Button remains dark on despawning disabled (hard mode).
        /// </summary>
        protected override bool IsActive() => !Options.disableDespawning;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "TrafficDespawning",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override void OnClick(UIMouseEventParameter p) {
            // Immediately unclick the tool button, but toggle the option
            ModUI.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);

            // Toggle the despawning value
            OptionsGameplayTab.SetDisableDespawning(!Options.disableDespawning);

            // Update currently visible tooltip
            this.UpdateTooltip(refreshTooltip: true);
            this.UpdateButtonImage();
            // do not call base -- base.OnClick(p);
        }
    }
}
