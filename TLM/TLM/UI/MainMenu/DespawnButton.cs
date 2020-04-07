namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class DespawnButton : BaseMenuButton {
        public override string GetTooltip() =>
            Options.disableDespawning
                ? Translation.Menu.Get("Tooltip:Enable despawning")
                : Translation.Menu.Get("Tooltip:Disable despawning");

        public override bool IsVisible() => true;

        public override bool IsActive() => Options.disableDespawning;

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

        public override void OnClickInternal(UIMouseEventParameter p) {
            // Immediately unclick the tool button, but toggle the option
            ModUI.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
            OptionsGameplayTab.SetDisableDespawning(!Options.disableDespawning);
        }
    }
}
