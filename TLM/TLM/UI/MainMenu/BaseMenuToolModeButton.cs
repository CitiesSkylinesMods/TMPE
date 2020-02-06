namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using TrafficManager.UI;

    /// <summary>
    /// Tool mode button base. Can store active state, and sets the tool mode on click.
    /// </summary>
    public abstract class BaseMenuToolModeButton : BaseMenuButton {
        protected abstract ToolMode ToolMode { get; }

        public override bool Active =>
            ToolMode.Equals(ModUI.GetTrafficManagerTool(false)?.GetToolMode());

        public override void OnClickInternal(UIMouseEventParameter p) {
            ModUI.GetTrafficManagerTool().SetToolMode(Active ? ToolMode.None : ToolMode);
        }
    }
}