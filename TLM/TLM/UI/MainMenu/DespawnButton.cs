namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using State;

    public class DespawnButton : MenuButton {
        public override bool Active => false;

        protected override ButtonFunction Function => Options.disableDespawning
                                                       ? ButtonFunction.DespawnDisabled
                                                       : ButtonFunction.DespawnEnabled;

        public override string Tooltip =>
            Options.disableDespawning
                ? Translation.Options.Get("Maintenance.Button:Enable despawning")
                : Translation.Options.Get("Maintenance.Checkbox:Disable despawning");

        public override bool Visible => true;

        public override void OnClickInternal(UIMouseEventParameter p) {
            UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
            OptionsGameplayTab.SetDisableDespawning(!Options.disableDespawning);
        }
    }
}
