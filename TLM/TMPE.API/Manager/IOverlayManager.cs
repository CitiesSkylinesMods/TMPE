using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.API.Manager {
    public interface IOverlayManager {

        /// <summary>
        /// Turn on common persistent overlays in vicinity of
        /// mouse cursor.
        /// </summary>
        /// <returns>Returns <c>true</c> if successful, otherwise <c>false</c>.</returns>
        public bool TurnOn();

        /// <summary>
        /// Turn on specific persistent overlays, and optionally set
        /// the overlay culling mode.
        /// </summary>
        /// <param name="persistent">
        /// Flags depicting the persistent overlays to display.
        /// If set to <see cref="Overlays.None"/> it will turn overlays off.
        /// </param>
        /// <param name="culling">
        /// The overlay culling mode. Defaults to <see cref="OverlayCulling.Mouse"/>.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> if overlays succcessfully displayed.
        /// </returns>
        public bool TurnOn(Overlays persistent, OverlayCulling culling);

        /// <summary>
        /// Turn off all overlays.
        /// </summary>
        public void TurnOff();

        /// <summary>
        /// Determine if one of specified overlays is interactive.
        /// </summary>
        /// <param name="overlays">Overlays to inspect.</param>
        /// <returns>Returns true if one of the overlays is iteractive.</returns>
        public bool IsInteractive(Overlays overlays);

        /// <summary>
        /// Determine if one of specified overlays is persistent.
        /// </summary>
        /// <param name="overlays">Overlays to inspect.</param>
        /// <returns>Returns true if one of the overlays is persistent.</returns>
        public bool IsPersistent(Overlays overlays);

        /// <summary>
        /// Returns <c>true</c> if any overlays active, otherwise <c>false</c>.
        /// </summary>
        public bool AnyOverlaysActive { get; }

        /// <summary>
        /// Returns <c>true</c> if persistent tunnels overlay is active.
        /// This means that underground overlays will be available in
        /// overground mode.
        /// </summary>
        public bool PersistentTunnels { get; }
    }
}
