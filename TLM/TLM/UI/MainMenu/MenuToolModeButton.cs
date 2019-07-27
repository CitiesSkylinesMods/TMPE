﻿namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;

    public abstract class MenuToolModeButton : MenuButton {
        protected abstract ToolMode ToolMode { get; }

        public override bool Active =>
            ToolMode.Equals(UIBase.GetTrafficManagerTool(false)?.GetToolMode());

        public override void OnClickInternal(UIMouseEventParameter p) {
            UIBase.GetTrafficManagerTool().SetToolMode(Active ? ToolMode.None : ToolMode);
        }
    }
}