namespace TrafficManager.UI.Textures {
    using TrafficManager.Util;
    using UnityEngine;
    using static TextureResources;

    /// <summary>
    /// Textures for UI controlling crossings, junctions and nodes
    /// </summary>
    public class JunctionRestrictionsTextures {
        public readonly Texture2D LaneChangeForbidden;
        public readonly Texture2D LaneChangeAllowed;

        public readonly Texture2D UturnAllowed;
        public readonly Texture2D UturnForbidden;

        public readonly Texture2D RightOnRedForbidden;
        public readonly Texture2D RightOnRedAllowed;

        public readonly Texture2D LeftOnRedForbidden;
        public readonly Texture2D LeftOnRedAllowed;

        public readonly Texture2D EnterBlockedJunctionAllowed;
        public readonly Texture2D EnterBlockedJunctionForbidden;

        public readonly Texture2D PedestrianCrossingAllowed;
        public readonly Texture2D PedestrianCrossingForbidden;

        public JunctionRestrictionsTextures() {
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