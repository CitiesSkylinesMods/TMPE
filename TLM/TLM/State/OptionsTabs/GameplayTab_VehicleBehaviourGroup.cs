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
                DefaultValue = true,
                Label = "Gameplay.Checkbox:Individual driving styles",
            };

        // Requires Snowfall DLC
        public static DLCRestrictedCheckboxOption StrongerRoadConditionEffects =
            new(nameof(Options.strongerRoadConditionEffects), requiredDLC: SteamHelper.DLC.SnowFallDLC, Options.PersistTo.Savegame) {
                Label = "Gameplay.Checkbox:Increase road condition impact",
                Validator = SnowfallDlcValidator,
            };

        public static CheckboxOption DisableDespawning =
            new(nameof(Options.disableDespawning), Options.PersistTo.Savegame) {
                Label = "Maintenance.Checkbox:Disable despawning",
                Handler = (newValue) => AllowDespawnFiltersButton.ReadOnly = !newValue,
            };

        public static DropDownOption<RecklessDrivers> RecklessDriversOption =
            new(nameof(Options.recklessDrivers), Options.PersistTo.Savegame) {
                Label = "Gameplay.Dropdown:Reckless drivers%",
                DefaultValue = RecklessDrivers.HolyCity,
            };

        public static ActionButton AllowDespawnFiltersButton = new() {
            Label = "Gameplay.Button:Filter Disable despawning vehicle type",
            Handler = AllowDespawningPanel.OpenModal,
            ReadOnly = !Options.disableDespawning,
        };

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("Gameplay.Group:Vehicle behavior"));

            RecklessDriversOption.AddUI(group);

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