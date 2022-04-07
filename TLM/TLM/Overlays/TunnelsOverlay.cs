namespace TrafficManager.Overlays {
    using System;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl.OverlayManagerData;
    using static InfoManager;

    /// <summary>
    /// Renders tunnels in overground views. This enables
    /// rendering and interaction of some underground overlays
    /// (at discretion of the overlay).
    /// </summary>
    /// <remarks>
    /// Cannot be used in Info contexts. Cannot be interactive.
    /// </remarks>
    public class TunnelsOverlay : ManagedOverlayBase, IManagedOverlay
    {
        public TunnelsOverlay()
            : base(Overlays.Tunnels, EntityType.None)
            { }

        [Obsolete("Should be replaced with a patch")]
        public override void OnFrameChanged(ref OverlayState state) {

            if (InfoManager.instance.CurrentMode == InfoMode.None)
                TransportManager.instance.TunnelsVisible = true;
        }
    }
}
