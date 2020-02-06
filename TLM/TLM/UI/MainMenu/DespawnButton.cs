namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using TrafficManager.State;

    public class DespawnButton : BaseMenuButton {
        public override bool Active => false;

        protected override ButtonFunction Function => Options.disableDespawning
                                                       ? ButtonFunction.DespawnDisabled
                                                       : ButtonFunction.DespawnEnabled;

        public override string Tooltip =>
            Options.disableDespawning
                ? Translation.Menu.Get("Tooltip:Enable despawning")
                : Translation.Menu.Get("Tooltip:Disable despawning");

        public override bool Visible => true;

        public override void OnClickInternal(UIMouseEventParameter p) {
            ModUI.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
            OptionsGameplayTab.SetDisableDespawning(!Options.disableDespawning);
        }
    }
}
