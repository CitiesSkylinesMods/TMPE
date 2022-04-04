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
                Handler = (newValue) => AllowDespawnFiltersButton.ReadOnly = !newValue,
            };

        public static DropDownOption<RecklessDrivers> RecklessDrivers =
            new(nameof(Options.recklessDrivers), Options.PersistTo.Savegame) {
                Label = "Gameplay.Dropdown:Reckless drivers%",
            };


        public static ActionButton AllowDespawnFiltersButton = new() {
            Label = "Filter Disable despawning vehicle types",
            Handler = AllowDespawningPanel.OpenModal,
            ReadOnly = !Options.disableDespawning,
        };

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("Gameplay.Group:Vehicle behavior"));

            RecklessDrivers.AddUI(group);

            IndividualDrivingStyle.AddUI(group);

            if (HasSnowfallDLC)
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