namespace TrafficManager.UI.MainMenu.OSD {
    using ColossalFramework.UI;

    public abstract class OsdItem {
        /// <summary>Called by the OnscreenDisplay to add the contents of this item.</summary>
        /// <param name="builder">UI builder used to populate the panel.</param>
        public abstract void Build(UIComponent parent,
                                   U.UBuilder builder);
    }
}