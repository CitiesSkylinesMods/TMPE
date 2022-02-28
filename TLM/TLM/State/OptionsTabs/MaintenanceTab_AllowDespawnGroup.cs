namespace TrafficManager.State {
    using System;
    using ColossalFramework;
    using ICities;
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using ConfigData;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using Util;

    /// <summary>Exclusions list for "Force No Despawning" feature</summary>
    public static class MaintenanceTab_AllowDespawnGroup {

        public static CheckboxOption AllowDespawnPassengerCars =
            new ("AllowDespawnPassengerCar", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Passenger Car",
                Handler = (bool newValue) => OnChange(ExtVehicleType.PassengerCar, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.PassengerCar)
            };

        public static CheckboxOption AllowDespawnBuses =
            new ("AllowDespawnBus", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Buses",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Bus, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Bus)
            };

        public static CheckboxOption AllowDespawnTaxis =
            new ("AllowDespawnTaxi", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Taxis",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Taxi, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Taxi)
            };

        public static CheckboxOption AllowDespawnTrams =
            new ("AllowDespawnTram", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Trams",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Tram, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Tram)
            };

        public static CheckboxOption AllowDespawnTrolleybuses =
            new ("AllowDespawnTrolleybus", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Trolleybuses",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Trolleybus, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Trolleybus)
            };

        /* TODO #1368 - not supported yet
        public static CheckboxOption AllowDespawnPassengerPlanes =
            new ("AllowDespawnPassengerPlane", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Passenger Planes",
                Handler = (bool newValue) => OnChange(ExtVehicleType.PassengerPlane, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.PassengerPlane)
            };
        */

        public static CheckboxOption AllowDespawnPassengerTrains =
            new ("AllowDespawnPassengerTrain", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Passenger Trains",
                Handler = (bool newValue) => OnChange(ExtVehicleType.PassengerTrain, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.PassengerTrain)
            };

        /* TODO #1368 - not supported yet
         public static CheckboxOption AllowDespawnCargoPlanes =
            new ("AllowDespawnCargoPlane", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Cargo Planes",
                Handler = (bool newValue) => OnChange(ExtVehicleType.CargoPlane, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.CargoPlane)
            };
        */

        public static CheckboxOption AllowDespawnCargoTrains =
            new ("AllowDespawnCargoTrain", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Cargo Trains",
                Handler = (bool newValue) => OnChange(ExtVehicleType.CargoTrain, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.CargoTrain)
            };
        public static CheckboxOption AllowDespawnCargoTrucks =
            new ("AllowDespawnPassengerTruck", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Cargo Trucks",
                Handler = (bool newValue) => OnChange(ExtVehicleType.CargoTruck, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.CargoTruck)
            };
        public static CheckboxOption AllowDespawnService =
            new ("AllowDespawnService", Options.PersistTo.Global) {
                Label = "AllowDespawn.Checkbox:Service vehicles",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Service, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Service)
            };

        private static readonly Dictionary<CheckboxOption, ExtVehicleType> AllowDespawns = new ()
        {
            { AllowDespawnPassengerCars, ExtVehicleType.PassengerCar },
            { AllowDespawnBuses, ExtVehicleType.Bus },
            { AllowDespawnTaxis, ExtVehicleType.Taxi },
            { AllowDespawnTrams, ExtVehicleType.Tram },
            { AllowDespawnTrolleybuses, ExtVehicleType.Trolleybus },
            /*TODO #1368 - not supported yet
            { AllowDespawnPassengerPlanes, ExtVehicleType.PassengerPlane },
            */
            { AllowDespawnPassengerTrains, ExtVehicleType.PassengerTrain },
            /*TODO #1368 - not supported yet
             { AllowDespawnCargoPlanes, ExtVehicleType.CargoPlane },
             */
            { AllowDespawnCargoTrains, ExtVehicleType.CargoTrain },
            { AllowDespawnCargoTrucks, ExtVehicleType.CargoTruck },
            { AllowDespawnService, ExtVehicleType.Service },
        };

        static MaintenanceTab_AllowDespawnGroup() {
            try {
                // attach observer, update checkboxes with new values
                GlobalConfigObserver configObserver =
                    TMPELifecycle.Instance.gameObject.AddComponent<GlobalConfigObserver>();
                configObserver.OnUpdateObservers += (config) => OnUpdateCheckboxes(config);
            } catch (Exception e) {
                Log.Error($"Error initializing GlobalConfigObserver {e}");
            }
        }

        private static bool IsDespawnAllowed(ExtVehicleType evt) =>
            GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes.IsFlagSet(evt);

        /// <summary>
        /// Handles update of GlobalConfig in case of reload or reset
        /// </summary>
        /// <param name="config">New config instance after reload</param>
        private static void OnUpdateCheckboxes(GlobalConfig config) {
            Log._Debug("Updating Allow Despawn checkboxes with updated global config value");
            foreach (KeyValuePair<CheckboxOption,ExtVehicleType> entry in AllowDespawns) {
                entry.Key.Value = IsDespawnAllowed(entry.Value);
            }
        }

        /// <summary>
        /// Adds "Allow Despawn" group to the specified <paramref name="tab"/>.
        /// </summary>
        /// <param name="tab">The parent UI panel.</param>
        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("Maintenance.Group:Allow Despawn"));

            foreach (var allowed in AllowDespawns) {
                allowed.Key.AddUI(group);
            }
        }

        private static string T(string text) {
            return Translation.Options.Get(text);
        }

        private static void OnChange(ExtVehicleType vehicleType, bool newVal) {
            if (newVal) {
                GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes |= vehicleType;
            } else {
                GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes &= ~vehicleType;
            }
            GlobalConfig.WriteConfig();
            Log._Debug($"Updated GlobalConfig AllowedDespawnVehicleTypes: {GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes}");
        }
    }
}
