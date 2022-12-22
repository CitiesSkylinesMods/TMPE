namespace TrafficManager.State {
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class GameplayTab_AIGroups {

        // Advanced AI group

        public static CheckboxOption AdvancedAI =
            new (nameof(SavedGameOptions.advancedAI), Scope.Savegame) {
                Label = "Gameplay.Checkbox:Enable advanced vehicle AI",
                Handler = OnAdvancedAIChanged,
            };

        public static SliderOption AltLaneSelectionRatio =
            new(nameof(SavedGameOptions.altLaneSelectionRatio), Scope.Savegame) {
                Label = "Gameplay.Slider:Dynamic lane selection",
                Tooltip = "%",
                Min = 0,
                Max = 100,
                Handler = OnAltLaneSelectionRatioChanged,
            };

        // Parking AI group

        public static CheckboxOption ParkingAI =
            new(nameof(SavedGameOptions.parkingAI), Scope.Savegame) {
                Label = "Gameplay.Checkbox:Enable more realistic parking",
                Handler = OnParkingAIChanged,
            };

        // Public Transport group

        public static CheckboxOption RealisticPublicTransport =
            new(nameof(SavedGameOptions.realisticPublicTransport), Scope.Savegame) {
                Label = "Gameplay.Checkbox:No excessive transfers",
            };

        public static DLCRestrictedCheckboxOption AllowBusInOldTown =
            new(nameof(GlobalConfig.Instance.PathFinding.AllowBusInOldTownDistricts), requiredDLC: SteamHelper.DLC.AfterDarkDLC, Scope.Global) {
                Label = "Gameplay.Checkbox:Allow Buses in Old Town districts",
                Handler = OnAllowBusInOldTownChanged,
                Validator = AfterDarkDlcValidator,
            };

        public static DLCRestrictedCheckboxOption AllowTaxiInOldTown =
            new(nameof(GlobalConfig.Instance.PathFinding.AllowTaxiInOldTownDistricts), requiredDLC: SteamHelper.DLC.AfterDarkDLC, Scope.Global) {
                Label = "Gameplay.Checkbox:Allow Taxis in Old Town districts",
                Handler = OnAllowTaxiInOldTownChanged,
                Validator = AfterDarkDlcValidator,
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

            AllowBusInOldTown.AddUI(group);
            AllowTaxiInOldTown.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static bool HasAfterDarkDLC
            => SteamHelper.IsDLCOwned(SteamHelper.DLC.AfterDarkDLC);

        private static void OnAdvancedAIChanged(bool _) {
            if (TMPELifecycle.Instance.Deserializing) return;

            if (!SavedGameOptions.Instance.advancedAI)
                AltLaneSelectionRatio.Value = 0;
        }

        private static void OnAltLaneSelectionRatioChanged(float _) {
            if (TMPELifecycle.Instance.Deserializing) return;

            SavedGameOptions.Instance.altLaneSelectionRatio = AltLaneSelectionRatio.Save();

            if (SavedGameOptions.Instance.altLaneSelectionRatio > 0)
                AdvancedAI.Value = true;
        }

        private static void OnParkingAIChanged(bool _) {
            if (TMPELifecycle.Instance.Deserializing) return;

            if (SavedGameOptions.Instance.parkingAI) {
                AdvancedParkingManager.Instance.OnEnableFeature();
            } else {
                AdvancedParkingManager.Instance.OnDisableFeature();
            }
        }

        private static void OnAllowBusInOldTownChanged(bool value) {
            if (TMPELifecycle.Instance.Deserializing) return;

            PathFinding config = GlobalConfig.Instance.PathFinding;
            if (config.AllowBusInOldTownDistricts != value) {
                config.AllowBusInOldTownDistricts = value;
                GlobalConfig.WriteConfig();
            }
        }

        private static void OnAllowTaxiInOldTownChanged(bool value) {
            if (TMPELifecycle.Instance.Deserializing) return;

            PathFinding config = GlobalConfig.Instance.PathFinding;
            if (config.AllowTaxiInOldTownDistricts != value) {
                config.AllowTaxiInOldTownDistricts = value;
                GlobalConfig.WriteConfig();
            }
        }

        private static bool AfterDarkDlcValidator(bool desired, out bool result) {
            result = HasAfterDarkDLC && desired;
            return true;
        }
    }
}