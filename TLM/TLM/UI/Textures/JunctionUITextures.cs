namespace TrafficManager.UI.Textures {
    using System;
    using System.Collections.Generic;
    using State;
    using UnityEngine;
    using Util;
    using static TextureResources;

    /// <summary>
    /// Textures for UI controlling crossings, junctions and nodes
    /// </summary>
    public static class JunctionUITextures {
        public static readonly Texture2D LaneChangeForbiddenTexture2D;
        public static readonly Texture2D LaneChangeAllowedTexture2D;
        public static readonly Texture2D UturnAllowedTexture2D;
        public static readonly Texture2D UturnForbiddenTexture2D;
        public static readonly Texture2D RightOnRedForbiddenTexture2D;
        public static readonly Texture2D RightOnRedAllowedTexture2D;
        public static readonly Texture2D LeftOnRedForbiddenTexture2D;
        public static readonly Texture2D LeftOnRedAllowedTexture2D;
        public static readonly Texture2D EnterBlockedJunctionAllowedTexture2D;
        public static readonly Texture2D EnterBlockedJunctionForbiddenTexture2D;
        public static readonly Texture2D PedestrianCrossingAllowedTexture2D;
        public static readonly Texture2D PedestrianCrossingForbiddenTexture2D;

        static JunctionUITextures() {
            LaneChangeAllowedTexture2D = LoadDllResource("lanechange_allowed.png", 200, 200);
            LaneChangeForbiddenTexture2D = LoadDllResource("lanechange_forbidden.png", 200, 200);

            UturnAllowedTexture2D = LoadDllResource("uturn_allowed.png", 200, 200);
            UturnForbiddenTexture2D = LoadDllResource("uturn_forbidden.png", 200, 200);

            RightOnRedAllowedTexture2D = LoadDllResource("right_on_red_allowed.png", 200, 200);
            RightOnRedForbiddenTexture2D = LoadDllResource("right_on_red_forbidden.png", 200, 200);
            LeftOnRedAllowedTexture2D = LoadDllResource("left_on_red_allowed.png", 200, 200);
            LeftOnRedForbiddenTexture2D = LoadDllResource("left_on_red_forbidden.png", 200, 200);

            EnterBlockedJunctionAllowedTexture2D = LoadDllResource("enterblocked_allowed.png", 200, 200);
            EnterBlockedJunctionForbiddenTexture2D = LoadDllResource("enterblocked_forbidden.png", 200, 200);

            PedestrianCrossingAllowedTexture2D = LoadDllResource("crossing_allowed.png", 200, 200);
            PedestrianCrossingForbiddenTexture2D = LoadDllResource("crossing_forbidden.png", 200, 200);
        }
    }
}