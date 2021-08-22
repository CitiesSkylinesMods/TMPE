namespace TrafficManager.UI.MainMenu {
    using System;
    using JetBrains.Annotations;

    public partial class MainMenuWindow {
        public struct MenuButtonDef {
            public ToolMode Mode;
            public Type ButtonType;
            [NotNull]
            public Func<bool> IsEnabledFunc;
        }
    }
}