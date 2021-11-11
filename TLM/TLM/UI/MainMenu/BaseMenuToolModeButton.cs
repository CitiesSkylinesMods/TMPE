namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using TrafficManager.UI;

    /// <summary>
    /// Tool mode button base. Can store active state, and sets the tool mode on click.
    /// </summary>
    public abstract class BaseMenuToolModeButton : BaseMenuButton {
        protected abstract ToolMode ToolMode { get; }

        protected override bool IsActive() =>
            ToolMode.Equals(ModUI.GetTrafficManagerTool()?.GetToolMode());

        protected override void OnClick(UIMouseEventParameter p) {
            ModUI.GetTrafficManagerTool()
                 ?.SetToolMode(IsActive() ? ToolMode.None : ToolMode);
            base.OnClick(p);
        }
    }
}