namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using CSUtil.Commons;

    public abstract class MenuToolModeButton : MenuButton {
        protected abstract ToolMode ToolMode { get; }

        public override bool Active =>
            ToolMode.Equals(UIBase.GetTrafficManagerTool(false)?.GetToolMode());

        public override void OnClickInternal(UIMouseEventParameter p) {
            UIBase.GetTrafficManagerTool().SetToolMode(Active ? ToolMode.None : ToolMode);
            Log._Debug("KIAN DEBUG LOG!!!!!! HOT RELOAD VERSION 2 2 2 2 2 2");
        }
    }
}