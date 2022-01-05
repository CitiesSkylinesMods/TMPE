namespace TrafficManager.UI.Textures {
    using TrafficManager.Manager;
    using TrafficManager.Util;
    using UnityEngine;
    using static TextureResources;

    /// <summary>
    /// Textures for UI controlling the traffic lights
    /// </summary>
    public class TrafficLightTextures : AbstractCustomManager {
        public static TrafficLightTextures Instance = new();

        public Texture2D RedLight;
        public Texture2D RedLightForwardLeft;
        public Texture2D RedLightForwardRight;
        public Texture2D RedLightLeft;
        public Texture2D RedLightRight;
        public Texture2D RedLightStraight;
        public Texture2D PedestrianRedLight;

        public Texture2D YellowLight;
        public Texture2D YellowLightForwardLeft;
        public Texture2D YellowLightForwardRight;
        public Texture2D YellowLightLeft;
        public Texture2D YellowLightRight;
        public Texture2D YellowLightStraight;
        public Texture2D YellowRedLight;

        public Texture2D GreenLight;
        public Texture2D GreenLightForwardLeft;
        public Texture2D GreenLightForwardRight;
        public Texture2D GreenLightLeft;
        public Texture2D GreenLightRight;
        public Texture2D GreenLightStraight;
        public Texture2D PedestrianGreenLight;

        //--------------------------
        // Timed TL Editor
        //--------------------------
        public Texture2D LightMode;
        public Texture2D LightCounter;
        public Texture2D ClockPlay;
        public Texture2D ClockPause;
        public Texture2D ClockTest;
        public Texture2D PedestrianModeAutomatic;
        public Texture2D PedestrianModeManual;

        //--------------------------
        // Toggle TL Tool
        //--------------------------
        public Texture2D TrafficLightEnabled;
        public Texture2D TrafficLightEnabledTimed;
        public Texture2D TrafficLightDisabled;

        public override void OnLevelLoading() {
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

            base.OnLevelLoading();
        }

        public override void OnLevelUnloading() {
            UnityEngine.Object.Destroy(RedLight);
            UnityEngine.Object.Destroy(RedLightForwardLeft);
            UnityEngine.Object.Destroy(RedLightForwardRight);
            UnityEngine.Object.Destroy(RedLightLeft);
            UnityEngine.Object.Destroy(RedLightRight);
            UnityEngine.Object.Destroy(RedLightStraight);
            UnityEngine.Object.Destroy(PedestrianRedLight);

            UnityEngine.Object.Destroy(YellowLight);
            UnityEngine.Object.Destroy(YellowLightForwardLeft);
            UnityEngine.Object.Destroy(YellowLightForwardRight);
            UnityEngine.Object.Destroy(YellowLightLeft);
            UnityEngine.Object.Destroy(YellowLightRight);
            UnityEngine.Object.Destroy(YellowLightStraight);
            UnityEngine.Object.Destroy(YellowRedLight);

            UnityEngine.Object.Destroy(GreenLight);
            UnityEngine.Object.Destroy(GreenLightForwardLeft);
            UnityEngine.Object.Destroy(GreenLightForwardRight);
            UnityEngine.Object.Destroy(GreenLightLeft);
            UnityEngine.Object.Destroy(GreenLightRight);
            UnityEngine.Object.Destroy(GreenLightStraight);
            UnityEngine.Object.Destroy(PedestrianGreenLight);

            UnityEngine.Object.Destroy(LightMode);
            UnityEngine.Object.Destroy(LightCounter);
            UnityEngine.Object.Destroy(ClockPlay);
            UnityEngine.Object.Destroy(ClockPause);
            UnityEngine.Object.Destroy(ClockTest);
            UnityEngine.Object.Destroy(PedestrianModeAutomatic);
            UnityEngine.Object.Destroy(PedestrianModeManual);

            UnityEngine.Object.Destroy(TrafficLightEnabled);
            UnityEngine.Object.Destroy(TrafficLightEnabledTimed);
            UnityEngine.Object.Destroy(TrafficLightDisabled);

            base.OnLevelUnloading();
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
