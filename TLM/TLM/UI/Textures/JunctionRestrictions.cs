namespace TrafficManager.UI.Textures {
    using TrafficManager.Util;
    using UnityEngine;
    using static TextureResources;

    /// <summary>
    /// Textures for UI controlling crossings, junctions and nodes
    /// </summary>
    public static class JunctionRestrictions {
        public static readonly Texture2D LaneChangeForbidden;
        public static readonly Texture2D LaneChangeAllowed;

        public static readonly Texture2D UturnAllowed;
        public static readonly Texture2D UturnForbidden;

        public static readonly Texture2D RightOnRedForbidden;
        public static readonly Texture2D RightOnRedAllowed;

        public static readonly Texture2D LeftOnRedForbidden;
        public static readonly Texture2D LeftOnRedAllowed;

        public static readonly Texture2D EnterBlockedJunctionAllowed;
        public static readonly Texture2D EnterBlockedJunctionForbidden;

        public static readonly Texture2D PedestrianCrossingAllowed;
        public static readonly Texture2D PedestrianCrossingForbidden;

        static JunctionRestrictions() {
            IntVector2 size = new IntVector2(200);

            LaneChangeAllowed = LoadDllResource("JunctionRestrictions.lanechange_allowed.png", size);
            LaneChangeForbidden = LoadDllResource("JunctionRestrictions.lanechange_forbidden.png", size);

            UturnAllowed = LoadDllResource("JunctionRestrictions.uturn_allowed.png", size);
            UturnForbidden = LoadDllResource("JunctionRestrictions.uturn_forbidden.png", size);

            RightOnRedAllowed = LoadDllResource("JunctionRestrictions.right_on_red_allowed.png", size);
            RightOnRedForbidden = LoadDllResource("JunctionRestrictions.right_on_red_forbidden.png", size);
            LeftOnRedAllowed = LoadDllResource("JunctionRestrictions.left_on_red_allowed.png", size);
            LeftOnRedForbidden = LoadDllResource("JunctionRestrictions.left_on_red_forbidden.png", size);

            EnterBlockedJunctionAllowed = LoadDllResource("JunctionRestrictions.enterblocked_allowed.png", size);
            EnterBlockedJunctionForbidden = LoadDllResource("JunctionRestrictions.enterblocked_forbidden.png", size);

            PedestrianCrossingAllowed = LoadDllResource("JunctionRestrictions.crossing_allowed.png", size);
            PedestrianCrossingForbidden = LoadDllResource("JunctionRestrictions.crossing_forbidden.png", size);
        }
    }
}