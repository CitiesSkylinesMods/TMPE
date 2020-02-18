namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class DespawnButton : BaseMenuButton {
        public override bool IsActive() => false;

        protected override ButtonFunction Function =>
            new ButtonFunction("Despawn", !Options.disableDespawning);

        public override string GetTooltip() =>
            Options.disableDespawning
                ? Translation.Menu.Get("Tooltip:Enable despawning")
                : Translation.Menu.Get("Tooltip:Disable despawning");

        public override bool IsVisible() => true;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        public override void OnClickInternal(UIMouseEventParameter p) {
            ModUI.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
            OptionsGameplayTab.SetDisableDespawning(!Options.disableDespawning);
        }
    }
}
