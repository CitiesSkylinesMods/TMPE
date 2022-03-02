namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class GameplayTab_VehicleBehaviourGroup {

        public static CheckboxOption IndividualDrivingStyle =
            new (nameof(Options.individualDrivingStyle), Options.PersistTo.Savegame) {
                Label = "Gameplay.Checkbox:Individual driving styles",
            };

        // Requires Snowfall DLC
        public static CheckboxOption StrongerRoadConditionEffects =
            new(nameof(Options.strongerRoadConditionEffects), Options.PersistTo.Savegame) {
                Label = "Gameplay.Checkbox:Increase road condition impact",
                Validator = SnowfallDlcValidator,
            };

        public static CheckboxOption DisableDespawning =
            new(nameof(Options.disableDespawning), Options.PersistTo.Savegame) {
                Label = "Maintenance.Checkbox:Disable despawning",
            };

        private static UIDropDown _recklessDriversDropdown;

        public static void SetRecklessDrivers(int newRecklessDrivers) {
            Options.recklessDrivers = newRecklessDrivers;

            if (_recklessDriversDropdown != null) {
                _recklessDriversDropdown.selectedIndex = newRecklessDrivers;
            }
        }

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("Gameplay.Group:Vehicle behavior"));

            AddRecklessDriversDropDown(group);

            IndividualDrivingStyle.AddUI(group);

            if (HasSnowfallDLC)
                StrongerRoadConditionEffects.AddUI(group);

            DisableDespawning.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static bool HasSnowfallDLC
            => SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC);

        private static bool SnowfallDlcValidator(bool desired, out bool result) {
            result = HasSnowfallDLC && desired;
            return true;
        }

        private static void AddRecklessDriversDropDown(UIHelperBase group) {
            _recklessDriversDropdown
                = group.AddDropdown(
                      T("Gameplay.Dropdown:Reckless drivers%") + ":",
                      new[] {
                                T("Gameplay.Dropdown.Option:Path Of Evil (10%)"),
                                T("Gameplay.Dropdown.Option:Rush Hour (5%)"),
                                T("Gameplay.Dropdown.Option:Minor Complaints (2%)"),
                                T("Gameplay.Dropdown.Option:Holy City (0%)"),
                      },
                      Options.recklessDrivers,
                      OnRecklessDriversChanged) as UIDropDown;

            _recklessDriversDropdown.width = 350;
        }

        private static void OnRecklessDriversChanged(int newRecklessDrivers) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log.Info($"Reckless driver amount changed to {newRecklessDrivers}");
            Options.recklessDrivers = newRecklessDrivers;
        }
    }
}