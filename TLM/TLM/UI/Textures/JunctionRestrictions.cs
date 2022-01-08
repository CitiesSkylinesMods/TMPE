namespace TrafficManager.UI.Textures {
    using TrafficManager.Manager;
    using TrafficManager.Util;
    using UnityEngine;
    using static TextureResources;

    /// <summary>
    /// Textures for UI controlling crossings, junctions and nodes
    /// </summary>
    public class JunctionRestrictions : AbstractCustomManager {
        public static JunctionRestrictions Instance = new();

        public Texture2D LaneChangeForbidden;
        public Texture2D LaneChangeAllowed;

        public Texture2D UturnAllowed;
        public Texture2D UturnForbidden;

        public Texture2D RightOnRedForbidden;
        public Texture2D RightOnRedAllowed;

        public Texture2D LeftOnRedForbidden;
        public Texture2D LeftOnRedAllowed;

        public Texture2D EnterBlockedJunctionAllowed;
        public Texture2D EnterBlockedJunctionForbidden;

        public Texture2D PedestrianCrossingAllowed;
        public Texture2D PedestrianCrossingForbidden;

        /// <summary>Called by the lifecycle when textures are to be loaded.</summary>
        public override void OnLevelLoading() {
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

            base.OnLevelLoading();
        }

        /// <summary>Called by the lifecycle when textures are to be unloaded.</summary>
        public override void OnLevelUnloading() {
            UnityEngine.Object.Destroy(LaneChangeAllowed);
            UnityEngine.Object.Destroy(LaneChangeForbidden);

            UnityEngine.Object.Destroy(UturnAllowed);
            UnityEngine.Object.Destroy(UturnForbidden);

            UnityEngine.Object.Destroy(RightOnRedAllowed);
            UnityEngine.Object.Destroy(RightOnRedForbidden);
            UnityEngine.Object.Destroy(LeftOnRedAllowed);
            UnityEngine.Object.Destroy(LeftOnRedForbidden);

            UnityEngine.Object.Destroy(EnterBlockedJunctionAllowed);
            UnityEngine.Object.Destroy(EnterBlockedJunctionForbidden);

            UnityEngine.Object.Destroy(PedestrianCrossingAllowed);
            UnityEngine.Object.Destroy(PedestrianCrossingForbidden);

            base.OnLevelUnloading();
        }
    }
}