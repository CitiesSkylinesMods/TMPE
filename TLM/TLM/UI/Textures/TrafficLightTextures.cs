namespace TrafficManager.UI.Textures {
    using UnityEngine;
    using static TextureResources;

    /// <summary>
    /// Textures for UI controlling the traffic lights
    /// </summary>
    public static class TrafficLightTextures {
        public static readonly Texture2D RedLight;
        public static readonly Texture2D RedLightForwardLeft;
        public static readonly Texture2D RedLightForwardRight;
        public static readonly Texture2D RedLightLeft;
        public static readonly Texture2D RedLightRight;
        public static readonly Texture2D RedLightStraight;
        public static readonly Texture2D PedestrianRedLight;

        public static readonly Texture2D YellowLight;
        public static readonly Texture2D YellowLightForwardLeft;
        public static readonly Texture2D YellowLightForwardRight;
        public static readonly Texture2D YellowLightLeft;
        public static readonly Texture2D YellowLightRight;
        public static readonly Texture2D YellowLightStraight;
        public static readonly Texture2D YellowRedLight;

        public static readonly Texture2D GreenLight;
        public static readonly Texture2D GreenLightForwardLeft;
        public static readonly Texture2D GreenLightForwardRight;
        public static readonly Texture2D GreenLightLeft;
        public static readonly Texture2D GreenLightRight;
        public static readonly Texture2D GreenLightStraight;
        public static readonly Texture2D PedestrianGreenLight;

        //--------------------------
        // Timed TL Editor
        //--------------------------
        public static readonly Texture2D LightMode;
        public static readonly Texture2D LightCounter;
        public static readonly Texture2D ClockPlay;
        public static readonly Texture2D ClockPause;
        public static readonly Texture2D ClockTest;
        public static readonly Texture2D PedestrianModeAutomatic;
        public static readonly Texture2D PedestrianModeManual;

        static TrafficLightTextures() {
            // simple
            RedLight = LoadDllResource("light_1_1.png", 103, 243);
            YellowRedLight = LoadDllResource("light_1_2.png", 103, 243);
            GreenLight = LoadDllResource("light_1_3.png", 103, 243);

            // forward
            RedLightStraight = LoadDllResource("light_2_1.png", 103, 243);
            YellowLightStraight = LoadDllResource("light_2_2.png", 103, 243);
            GreenLightStraight = LoadDllResource("light_2_3.png", 103, 243);

            // right
            RedLightRight = LoadDllResource("light_3_1.png", 103, 243);
            YellowLightRight = LoadDllResource("light_3_2.png", 103, 243);
            GreenLightRight = LoadDllResource("light_3_3.png", 103, 243);

            // left
            RedLightLeft = LoadDllResource("light_4_1.png", 103, 243);
            YellowLightLeft = LoadDllResource("light_4_2.png", 103, 243);
            GreenLightLeft = LoadDllResource("light_4_3.png", 103, 243);

            // forwardright
            RedLightForwardRight = LoadDllResource("light_5_1.png", 103, 243);
            YellowLightForwardRight = LoadDllResource("light_5_2.png", 103, 243);
            GreenLightForwardRight = LoadDllResource("light_5_3.png", 103, 243);

            // forwardleft
            RedLightForwardLeft = LoadDllResource("light_6_1.png", 103, 243);
            YellowLightForwardLeft = LoadDllResource("light_6_2.png", 103, 243);
            GreenLightForwardLeft = LoadDllResource("light_6_3.png", 103, 243);

            // yellow
            YellowLight = LoadDllResource("light_yellow.png", 103, 243);

            // pedestrian
            PedestrianRedLight = LoadDllResource("pedestrian_light_1.png", 73, 123);
            PedestrianGreenLight = LoadDllResource("pedestrian_light_2.png", 73, 123);

            //--------------------------
            // Timed TL Editor
            //--------------------------
            // light mode
            LightMode = LoadDllResource(
                Translation.GetTranslatedFileName("light_mode.png"), 103, 95);
            LightCounter = LoadDllResource(
                Translation.GetTranslatedFileName("light_counter.png"), 103, 95);

            // pedestrian mode
            PedestrianModeAutomatic = LoadDllResource("pedestrian_mode_1.png", 73, 70);
            PedestrianModeManual = LoadDllResource("pedestrian_mode_2.png", 73, 73);

            // timer
            ClockPlay = LoadDllResource("clock_play.png", 512, 512);
            ClockPause = LoadDllResource("clock_pause.png", 512, 512);
            ClockTest = LoadDllResource("clock_test.png", 512, 512);
        }
    }
}