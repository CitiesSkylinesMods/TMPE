namespace TrafficManager.UI.Textures {
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
            LaneChangeAllowed = LoadDllResource(
                "JunctionRestrictions.lanechange_allowed.png",
                200,
                200);
            LaneChangeForbidden = LoadDllResource(
                "JunctionRestrictions.lanechange_forbidden.png",
                200,
                200);

            UturnAllowed = LoadDllResource(
                "JunctionRestrictions.uturn_allowed.png",
                200,
                200);
            UturnForbidden = LoadDllResource(
                "JunctionRestrictions.uturn_forbidden.png",
                200,
                200);

            RightOnRedAllowed = LoadDllResource(
                "JunctionRestrictions.right_on_red_allowed.png",
                200,
                200);
            RightOnRedForbidden = LoadDllResource(
                "JunctionRestrictions.right_on_red_forbidden.png",
                200,
                200);
            LeftOnRedAllowed = LoadDllResource(
                "JunctionRestrictions.left_on_red_allowed.png",
                200,
                200);
            LeftOnRedForbidden = LoadDllResource(
                "JunctionRestrictions.left_on_red_forbidden.png",
                200,
                200);

            EnterBlockedJunctionAllowed = LoadDllResource(
                "JunctionRestrictions.enterblocked_allowed.png",
                200,
                200);
            EnterBlockedJunctionForbidden = LoadDllResource(
                "JunctionRestrictions.enterblocked_forbidden.png",
                200,
                200);

            PedestrianCrossingAllowed = LoadDllResource(
                "JunctionRestrictions.crossing_allowed.png",
                200,
                200);
            PedestrianCrossingForbidden = LoadDllResource(
                "JunctionRestrictions.crossing_forbidden.png",
                200,
                200);
        }
    }
}