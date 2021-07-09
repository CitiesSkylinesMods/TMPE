namespace TrafficManager.UI.Textures {
    using TrafficManager.Lifecycle;
    using TrafficManager.Util;
    using UnityEngine;
    using static TextureResources;

    /// <summary>
    /// Textures for UI controlling the traffic lights
    /// </summary>
    public class TrafficLightTextures {
        public static TrafficLightTextures Instance => TMPELifecycle.Instance.Textures.TrafficLight;

        public readonly Texture2D RedLight;
        public readonly Texture2D RedLightForwardLeft;
        public readonly Texture2D RedLightForwardRight;
        public readonly Texture2D RedLightLeft;
        public readonly Texture2D RedLightRight;
        public readonly Texture2D RedLightStraight;
        public readonly Texture2D PedestrianRedLight;

        public readonly Texture2D YellowLight;
        public readonly Texture2D YellowLightForwardLeft;
        public readonly Texture2D YellowLightForwardRight;
        public readonly Texture2D YellowLightLeft;
        public readonly Texture2D YellowLightRight;
        public readonly Texture2D YellowLightStraight;
        public readonly Texture2D YellowRedLight;

        public readonly Texture2D GreenLight;
        public readonly Texture2D GreenLightForwardLeft;
        public readonly Texture2D GreenLightForwardRight;
        public readonly Texture2D GreenLightLeft;
        public readonly Texture2D GreenLightRight;
        public readonly Texture2D GreenLightStraight;
        public readonly Texture2D PedestrianGreenLight;

        //--------------------------
        // Timed TL Editor
        //--------------------------
        public Texture2D LightMode;
        public Texture2D LightCounter;
        public readonly Texture2D ClockPlay;
        public readonly Texture2D ClockPause;
        public readonly Texture2D ClockTest;
        public Texture2D PedestrianModeAutomatic;
        public Texture2D PedestrianModeManual;

        //--------------------------
        // Toggle TL Tool
        //--------------------------
        public readonly Texture2D TrafficLightEnabled;
        public readonly Texture2D TrafficLightEnabledTimed;
        public readonly Texture2D TrafficLightDisabled;

        public TrafficLightTextures() {
            IntVector2 tlSize = new IntVector2(103, 243);

            RedLight = LoadDllResource("TrafficLights.light_1_1.png", tlSize);
            YellowRedLight = LoadDllResource("TrafficLights.light_1_2.png", tlSize);
            GreenLight = LoadDllResource("TrafficLights.light_1_3.png", tlSize);

            // forward
            RedLightStraight = LoadDllResource("TrafficLights.light_2_1.png", tlSize);
            YellowLightStraight = LoadDllResource("TrafficLights.light_2_2.png", tlSize);
            GreenLightStraight = LoadDllResource("TrafficLights.light_2_3.png", tlSize);

            // right
            RedLightRight = LoadDllResource("TrafficLights.light_3_1.png", tlSize);
            YellowLightRight = LoadDllResource("TrafficLights.light_3_2.png", tlSize);
            GreenLightRight = LoadDllResource("TrafficLights.light_3_3.png", tlSize);

            // left
            RedLightLeft = LoadDllResource("TrafficLights.light_4_1.png", tlSize);
            YellowLightLeft = LoadDllResource("TrafficLights.light_4_2.png", tlSize);
            GreenLightLeft = LoadDllResource("TrafficLights.light_4_3.png", tlSize);

            // forwardright
            RedLightForwardRight = LoadDllResource("TrafficLights.light_5_1.png", tlSize);
            YellowLightForwardRight = LoadDllResource("TrafficLights.light_5_2.png", tlSize);
            GreenLightForwardRight = LoadDllResource("TrafficLights.light_5_3.png", tlSize);

            // forwardleft
            RedLightForwardLeft = LoadDllResource("TrafficLights.light_6_1.png", tlSize);
            YellowLightForwardLeft = LoadDllResource("TrafficLights.light_6_2.png", tlSize);
            GreenLightForwardLeft = LoadDllResource("TrafficLights.light_6_3.png", tlSize);

            // yellow
            YellowLight = LoadDllResource("TrafficLights.light_yellow.png", tlSize);

            // pedestrian
            IntVector2 pedSize = new IntVector2(73, 123);
            PedestrianRedLight = LoadDllResource("TrafficLights.pedestrian_light_1.png", pedSize);
            PedestrianGreenLight = LoadDllResource("TrafficLights.pedestrian_light_2.png", pedSize);

            //--------------------------
            // Timed TL Editor
            //--------------------------
            LoadTexturesWithTranslation();

            // timer
            IntVector2 timerSize = new IntVector2(512);

            ClockPlay = LoadDllResource("TrafficLights.clock_play.png", timerSize);
            ClockPause = LoadDllResource("TrafficLights.clock_pause.png", timerSize);
            ClockTest = LoadDllResource("TrafficLights.clock_test.png", timerSize);

            //--------------------------
            // Toggle TL Tool
            //--------------------------
            IntVector2 toggleSize = new IntVector2(64);
            TrafficLightEnabled = LoadDllResource("TrafficLights.IconJunctionTrafficLights.png", toggleSize);
            TrafficLightEnabledTimed = LoadDllResource("TrafficLights.IconJunctionTimedTL.png", toggleSize);
            TrafficLightDisabled = LoadDllResource("TrafficLights.IconJunctionNoTrafficLights.png", toggleSize);
        }

        public void ReloadTexturesWithTranslation() {
            if (LightMode) UnityEngine.GameObject.Destroy(LightMode);
            if (LightCounter) UnityEngine.GameObject.Destroy(LightCounter);
            if (PedestrianModeAutomatic) UnityEngine.GameObject.Destroy(PedestrianModeAutomatic);
            if (PedestrianModeManual) UnityEngine.GameObject.Destroy(PedestrianModeManual);
            LoadTexturesWithTranslation();
        }

        private void LoadTexturesWithTranslation() {
            // light mode
            IntVector2 tlModeSize = new IntVector2(103, 95);

            LightMode = LoadDllResource(
                Translation.GetTranslatedFileName("TrafficLights.light_mode.png"),
                tlModeSize);
            LightCounter = LoadDllResource(
                Translation.GetTranslatedFileName("TrafficLights.light_counter.png"),
                tlModeSize);

            // pedestrian mode
            IntVector2 pedModeSize = new IntVector2(73, 70);

            PedestrianModeAutomatic = LoadDllResource(
                Translation.GetTranslatedFileName("TrafficLights.pedestrian_mode_1.png"),
                pedModeSize);
            PedestrianModeManual = LoadDllResource(
                Translation.GetTranslatedFileName("TrafficLights.pedestrian_mode_2.png"),
                pedModeSize);
        }
    }
}
