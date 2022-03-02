namespace TrafficManager.State {
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class GameplayTab_AIGroups {

        // Advanced AI group

        public static CheckboxOption AdvancedAI =
            new (nameof(Options.advancedAI), Options.PersistTo.Savegame) {
                Label = "Gameplay.Checkbox:Enable advanced vehicle AI",
                Handler = OnAdvancedAIChanged,
            };

        public static SliderOption AltLaneSelectionRatio =
            new(nameof(Options.altLaneSelectionRatio), Options.PersistTo.None) {
                Label = "Gameplay.Slider:Dynamic lane selection",
                Tooltip = "%",
                Min = 0,
                Max = 100,
                Handler = OnAltLaneSelectionRatioChanged,
            };

        // Parking AI group

        public static CheckboxOption ParkingAI =
            new(nameof(Options.parkingAI), Options.PersistTo.Savegame) {
                Label = "Gameplay.Checkbox:Enable more realistic parking",
                Handler = OnParkingAIChanged,
            };

        // Public Transport group

        public static CheckboxOption RealisticPublicTransport =
            new(nameof(Options.realisticPublicTransport), Options.PersistTo.Savegame) {
                Label = "Gameplay.Checkbox:No excessive transfers",
            };

        internal static void AddUI(UIHelperBase tab) {
            UIHelperBase group;

            group = tab.AddGroup(T("Gameplay.Group:Advanced vehicle AI"));

            AdvancedAI.AddUI(group);

            AltLaneSelectionRatio.AddUI(group)
                .ReadOnly = !TMPELifecycle.PlayMode;

            group = tab.AddGroup(T("Gameplay.Group:Parking AI"));

            ParkingAI.AddUI(group);

            group = tab.AddGroup(T("Gameplay.Group:Public transport"));

            RealisticPublicTransport.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void OnAdvancedAIChanged(bool _) {
            if (TMPELifecycle.Instance.Deserializing) return;

            if (!Options.advancedAI)
                AltLaneSelectionRatio.Value = 0;
        }

        private static void OnAltLaneSelectionRatioChanged(float _) {
            if (TMPELifecycle.Instance.Deserializing) return;

            Options.altLaneSelectionRatio = AltLaneSelectionRatio.Save();

            if (Options.altLaneSelectionRatio > 0)
                AdvancedAI.Value = true;
        }

        private static void OnParkingAIChanged(bool _) {
            if (TMPELifecycle.Instance.Deserializing) return;

            if (Options.parkingAI) {
                AdvancedParkingManager.Instance.OnEnableFeature();
            } else {
                AdvancedParkingManager.Instance.OnDisableFeature();
            }
        }
    }
}