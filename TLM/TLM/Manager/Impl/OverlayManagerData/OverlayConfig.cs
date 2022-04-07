namespace TrafficManager.Manager.Impl.OverlayManagerData {
    using JetBrains.Annotations;
    using System;
    using TrafficManager.API.Attributes;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Lifecycle;
    using TrafficManager.State;
    using static InfoManager;

    public struct OverlayConfig {

        internal OverlayContext Context;

        internal InfoMode Info; // Context = Info

        [CanBeNull]
        internal Type Tool; // Context = Tool

        internal OverlayCulling Culling;

        internal Overlays Persistent;

        internal Overlays Interactive;

        internal RestrictedVehicles Filter;

        /// <summary>
        /// Internal use only. Defines the targets for <see cref="ViewportCache"/>.
        /// </summary>
        internal EntityType Targets;

        /* The following fields are for internal use only. */

        internal EntityType RefreshMapCache;
        internal Overlays RefreshOverlays;

        internal bool IsEnabled =>
            Context != OverlayContext.None &&
            AllOverlays != 0;

        internal bool IsContext(InfoMode mode) =>
            Context == OverlayContext.Info &&
            Info == mode;

        internal bool IsContext(Type tool) =>
            Context == OverlayContext.Tool &&
            Tool == tool;

        internal bool IsInteractive(Overlays overlay) =>
            (Interactive & overlay) != 0;

        internal bool IsPersistent(Overlays overlay) =>
            (Persistent & overlay) != 0;

        internal Overlays AllOverlays =>
            Persistent | Interactive;

        internal bool HasOverlay(Overlays overlay) =>
            (AllOverlays & overlay) != 0;

        [Spike("Likely to cause lag spike, use rarely.")]
        internal void FullRefresh() {
            RefreshMapCache = Targets;
            RefreshOverlays = AllOverlays;
        }

        [Spike("May cause lag spike, use sparingly.")]
        internal void PartialRefresh(Overlays overlays) {
            if (overlays == 0)
                return;

            if (overlays == AllOverlays) {
                FullRefresh();
                return;
            }

            RefreshMapCache = EntityType.None;
            foreach (var target in OverlayManager.Targets) {
                if ((target.Key & overlays) != 0)
                    RefreshMapCache |= target.Value;
            }

            RefreshOverlays = overlays;
        }

        /// <summary>
        /// These settings will turn off the overlay rendering.
        /// </summary>
        /// <remarks>New struct generated each time.</remarks>
        internal static OverlayConfig Inactive =>
            new() { };

        /// <summary>
        /// These settings turn on situational overlay rendering.
        /// </summary>
        /// <remarks>New struct generated each time.</remarks>
        internal static OverlayConfig SituationalAwareness =>
            new OverlayConfig {
                Context = OverlayContext.Custom,
                Culling = OverlayCulling.Mouse,
                Persistent = Overlays.GroupAwareness,
                Interactive = Overlays.None,
                Filter = RestrictedVehicles.All,
            };

        internal static OverlayConfig Compile(OverlayConfig settings) {

            if (!settings.IsEnabled || !TMPELifecycle.InGameOrEditor())
                return Inactive;

            var compiled = new OverlayConfig {
                Context = settings.Context,
                Info = settings.Info,
                Tool = settings.Tool,
                Culling = settings.Culling,
                Persistent = settings.Persistent,
                Interactive = settings.Interactive,
                Filter = settings.Filter,
                Targets = EntityType.None,
            };

            // Validate interactive overlay
            if ((settings.Interactive & Overlays.TMPE | Overlays.Tunnels) != 0 ||
                !OverlayManager.IsIndividualOverlay(settings.Interactive)) {

                compiled.Interactive = Overlays.None;
            }

            // Replace with TMPE mod option config?
            if ((settings.Persistent & Overlays.TMPE) != 0)
                compiled.Persistent = Options.PersistentOverlays;

            // Remove interactive overlay from persistent overlays
            compiled.Persistent &= ~compiled.Interactive;

            // Remove tunnels overlay if Info context
            if (compiled.Context == OverlayContext.Info)
                compiled.Persistent &= ~Overlays.Tunnels;

            var allOverlays = compiled.AllOverlays;
            if (allOverlays == 0)
                return Inactive;

            // compile render targets
            foreach (var target in OverlayManager.Targets) {
                if ((target.Key & allOverlays) != 0)
                    compiled.Targets |= target.Value;
            }

            compiled.RefreshMapCache = compiled.Targets;
            compiled.RefreshOverlays = allOverlays;

            return compiled;
        }
    }
}
