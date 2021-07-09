namespace TrafficManager.UI.Textures {
    using CSUtil.Commons;
    using TrafficManager.State;

    /// <summary>
    /// Stores loaded mod textures.
    /// Move all static texture classes here to avoid using static classes and members.
    /// </summary>
    public class AllTextures {
        public SpeedLimitTextures SpeedLimits;
        public JunctionRestrictionsTextures JunctionRestrictions;
        public MainMenuTextures MainMenu;
        public RoadUITextures RoadUI;
        public TrafficLightTextures TrafficLight;

        public void Load() {
            bool debugResourceLoading = GlobalConfig.Instance.Debug.ResourceLoading;

            if (debugResourceLoading) {
                Log._Debug("AllTex: Loading Speed Limits textures...");
            }

            SpeedLimits = new SpeedLimitTextures();

            if (debugResourceLoading) {
                Log._Debug("AllTex: Loading Junction Restrictions textures...");
            }

            JunctionRestrictions = new JunctionRestrictionsTextures();

            MainMenu = new MainMenuTextures();

            if (debugResourceLoading) {
                Log._Debug("AllTex: Loading Road UI textures...");
            }

            RoadUI = new RoadUITextures();

            if (debugResourceLoading) {
                Log._Debug("AllTex: Loading Traffic Light textures...");
            }

            TrafficLight = new TrafficLightTextures();
        }
    }
}