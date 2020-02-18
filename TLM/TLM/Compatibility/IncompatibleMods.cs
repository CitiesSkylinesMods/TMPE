namespace TrafficManager.Compatibility {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.Compatibility.Enum;

    /// <summary>
    /// A list of incompatible mods.
    /// </summary>
    public class IncompatibleMods {
        private static IncompatibleMods instance;

        public static IncompatibleMods Instance => instance ?? (instance = new IncompatibleMods());

        /// <summary>
        /// The list of incompatible mods.
        /// </summary>
        public readonly Dictionary<ulong, Severity> List;

        /// <summary>
        /// Initializes a new instance of the <see cref="IncompatibleMods"/> class.
        /// </summary>
        public IncompatibleMods() {
            List = new Dictionary<ulong, Severity>() {
                // Note: TM:PE v10.20 not currently listed as lots of users still use it
                // It will be picked up by duplicate assemblies detector

                // Obsolete & rogue versions of TM:PE
                { 1957033250u, Severity.Critical }, // Traffic Manager: President Edition (Industries Compatible)
                { 1581695572u, Severity.Critical }, // Traffic Manager: President Edition
                { 1546870472u, Severity.Critical }, // Traffic Manager: President Edition (Industries Compatible)
                { 1348361731u, Severity.Critical }, // Traffic Manager: President Edition ALPHA/DEBUG

                // Traffic Manager + Traffic++ AI (obsolete; game breaking)
                { 498363759u, Severity.Critical }, // Traffic Manager + Improved AI
                { 563720449u, Severity.Critical }, // Traffic Manager + Improved AI (Japanese Ver.)

                // Traffic++ (obsolete; game breaking)
                { 492391912u, Severity.Critical }, // Improved AI (Traffic++)
                { 409184143u, Severity.Critical }, // Traffic++
                { 626024868u, Severity.Critical }, // Traffic++ V2

                // Extremely old verisons of Traffic Manager (obsolete; game breaking)
                { 427585724u, Severity.Critical }, // Traffic Manager
                { 481786333u, Severity.Critical }, // Traffic Manager Plus
                { 568443446u, Severity.Critical }, // Traffic Manager Plus 1.2.0

                // ARIS Hearse AI
                { 433249875u, Severity.Critical }, // [ARIS] Enhanced Hearse AI
                { 583556014u, Severity.Critical }, // Enhanced Hearse AI [Fixed for v1.4+]
                { 813835241u, Severity.Critical }, // Enhanced Hearse AI [1.6]

                // ARIS Garbage AI
                { 439582006u, Severity.Critical }, // [ARIS] Enhanced Garbage Truck AI
                { 583552152u, Severity.Critical }, // Enhanced Garbage Truck AI [Fixed for v1.4+]
                { 813835391u, Severity.Critical }, // Enhanced Garbage Truck AI [1.6]

                // ARIS Remove Stuck (use TM:PE "Reset Stuck Vehicles and Cims" instead)
                { 428094792u, Severity.Critical }, // [ARIS] Remove Stuck Vehicles
                { 587530437u, Severity.Critical }, // Remove Stuck Vehicles [Fixed for v1.4+]
                { 813834836u, Severity.Critical }, // Remove Stuck Vehicles [1.6]

                // ARIS Overwatch
                { 421028969u, Severity.Critical }, // [ARIS] Skylines Overwatch
                { 583538182u, Severity.Critical }, // Skylines Overwatch [Fixed for v1.3+]
                { 813833476u, Severity.Critical }, // Skylines Overwatch [1.6]

                // Old road anarchy mods (make a huge mess of networks and terrain!)
                { 418556522u, Severity.Critical }, // Road Anarchy
                { 954034590u, Severity.Critical }, // Road Anarchy V2

                // NExt v1 (fix with "Road Removal Tool" mod)
                { 478820060u,  Severity.Critical }, // Network Extensions Project (v1)
                { 929114228u,  Severity.Critical }, // New Roads for Network Extensions

                // Other game-breaking mods
                { 411095553u,  Severity.Critical }, // Terraform Tool v0.9 (just sick of seeing this break games!)
                { 414702884u,  Severity.Critical }, // Zoneable Pedestrian Paths
                { 417926819u,  Severity.Critical }, // Road Assistant
                { 422554572u,  Severity.Critical }, // 81 Tiles Updated
                { 436253779u,  Severity.Critical }, // Road Protractor
                { 553184329u,  Severity.Critical }, // Sharp Junction Angles
                { 651610627u,  Severity.Critical }, // Road Color Changer Continued
                { 658653260u,  Severity.Critical }, // Network Nodes Editor [Experimental]
                { 912329352u,  Severity.Critical }, // Building Anarchy (just sick of seeing this break games!)
                { 1072157697u, Severity.Critical }, // Cargo Info
                { 1767246646u, Severity.Critical }, // AutoLineBudget (PropVehCount errors, using .Net Framework 2, published as camera script?!!)

                // Incompatible with TM:PE (patch conflicts or does not fire events)
                { 512341354u,  Severity.Major }, // Central Services Dispatcher (WtM)
                { 844180955u,  Severity.Major }, // City Drive
                { 413847191u,  Severity.Major }, // SOM - Services Optimisation Module
                { 649522495u,  Severity.Major }, // District Service Limit
                { 1803209875u, Severity.Major }, // Trees Respiration

                // Mods made obsolete by TM:PE (and conflict with TM:PE patches/state)
                { 407335588u,  Severity.Major }, // No Despawn Mod
                { 411833858u,  Severity.Major }, // Toggle Traffic Lights
                { 529979180u,  Severity.Major }, // CSL Service Reserve
                { 600733054u,  Severity.Major }, // No On-Street Parking
                { 631930385u,  Severity.Major }, // Realistic Vehicle Speeds
                { 1628112268u, Severity.Major }, // RightTurnNoStop

                // Obsolete by vanilla game functionality (also, does not fire events we need to maintain state)
                { 631694768u, Severity.Minor }, // Extended Road Upgrade
                { 408209297u, Severity.Minor }, // Extended Road Upgrade

                // Oudated
                { 532863263u, Severity.Minor }, // Multi-track Station Enabler
                { 442957897u, Severity.Minor }, // Multi-track Station Enabler

                // Breaks toll booths
                { 726005715u, Severity.Minor }, // Roads United Core+
                { 680748394u, Severity.Minor }, // Roads United: North America
            };
            Log.InfoFormat(
                "Compatibility.IncompatibleMods.List  {0}",
                List.Count);
        }
    }
}
