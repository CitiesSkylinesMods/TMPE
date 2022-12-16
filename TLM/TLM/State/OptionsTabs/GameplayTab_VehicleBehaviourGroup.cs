namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.UI;
    using TrafficManager.UI.AllowDespawn;
    using TrafficManager.UI.Helpers;

    public static class GameplayTab_VehicleBehaviourGroup {

        public static CheckboxOption IndividualDrivingStyle =
            new (nameof(SavedGameOptions.individualDrivingStyle), Scope.Savegame) {
                Label = "Gameplay.Checkbox:Individual driving styles",
            };

        // Requires Snowfall DLC
        public static DLCRestrictedCheckboxOption StrongerRoadConditionEffects =
            new(nameof(SavedGameOptions.strongerRoadConditionEffects), requiredDLC: SteamHelper.DLC.SnowFallDLC, Scope.Savegame) {
                Label = "Gameplay.Checkbox:Increase road condition impact",
                Validator = SnowfallDlcValidator,
            };

        public static CheckboxOption DisableDespawning =
            new(nameof(SavedGameOptions.disableDespawning), Scope.Savegame) {
                Label = "Maintenance.Checkbox:Disable despawning",
                Handler = (newValue) => AllowDespawnFiltersButton.ReadOnly = !newValue,
            };

        public static DropDownOption<RecklessDrivers> RecklessDrivers =
            new(nameof(SavedGameOptions.recklessDrivers), Scope.Savegame) {
                Label = "Gameplay.Dropdown:Reckless drivers%",
            };


        public static ActionButton AllowDespawnFiltersButton = new() {
            Label = "Gameplay.Button:Filter Disable despawning vehicle type",
            Handler = AllowDespawningPanel.OpenModal,
            ReadOnly = !SavedGameOptions.Instance.disableDespawning,
        };

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("Gameplay.Group:Vehicle behavior"));

            RecklessDrivers.AddUI(group);

            IndividualDrivingStyle.AddUI(group);

            StrongerRoadConditionEffects.AddUI(group);

            DisableDespawning.AddUI(group);

            AllowDespawnFiltersButton.AddUI(group);
        }

        private static bool HasSnowfallDLC
            => SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC);

        private static bool SnowfallDlcValidator(bool desired, out bool result) {
            result = HasSnowfallDLC && desired;
            return true;
        }

        private static string T(string key) => Translation.Options.Get(key);
    }
}