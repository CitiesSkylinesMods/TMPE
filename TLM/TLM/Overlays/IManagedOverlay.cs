namespace TrafficManager.Overlays {
    using System;
    using TrafficManager.API.Attributes;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl.OverlayManagerData;

    public interface IManagedOverlay {

        /// <summary>
        /// Indicates if the overlay can be used. This
        /// is primarily to allow feature-reliant overlays
        /// to check their prerequisite mod option.
        /// </summary>
        bool CanBeUsed { get; }

        NetInfo.LaneType? LaneTypes { get; }

        VehicleInfo.VehicleType? VehicleTypes { get; }

        Overlays Overlay { get; }

        EntityType Targets { get; }

        void Reset();

        /// <summary>
        /// User pressed/released modifier key.
        /// </summary>
        /// <param name="settings">Overlay settings.</param>
        /// <param name="data">Overlay data.</param>
        [Cold("Keyboard interaction")]
        void OnModifierChanged(ref OverlayState state);

        /// <summary>
        /// User pressed/released modifier key.
        /// </summary>
        /// <param name="settings">Overlay settings.</param>
        /// <param name="data">Overlay data.</param>
        [Hot("Each frame while overlay active")]
        [Obsolete("This will be removed once tunnel overlay is done via patching")]
        void OnFrameChanged(ref OverlayState state);

        /// <summary>
        /// User pressed/released modifier key.
        /// </summary>
        /// <param name="settings">Overlay settings.</param>
        /// <param name="data">Overlay data.</param>
        [Cold("Camera moved noticeably")]
        void OnCameraMoved(ref OverlayState state);

        /// <summary>
        /// <see cref="InfoManager.instance.CurrentMode"/> changed.
        /// </summary>
        /// <param name="settings">Overlay settings.</param>
        /// <param name="data">Overlay data.</param>
        [Cold("InfoMode changed")]
        void OnInfoViewChanged(ref OverlayState state);
    }
}
