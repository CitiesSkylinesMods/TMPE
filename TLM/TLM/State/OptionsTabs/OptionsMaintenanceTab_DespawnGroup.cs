
namespace TrafficManager.State {
    using ColossalFramework;
    using ICities;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    /// <summary>Filterable despawn tool which only appears in-game (not from main menu or editors).</summary>
    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1516:Elements should be separated by blank line", Justification = "Brevity.")]
    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500:Braces for multi-line statements should not share line", Justification = "Brevity.")]
    public static class OptionsMaintenanceTab_DespawnGroup {
        public static CheckboxOption DespawnerAll =
            new (nameof(Options.despawnerAll), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:All vehicles",
                Handler = OnDespawnerAllChange,
            };
        public static CheckboxOption DespawnerRoad =
            new (nameof(Options.despawnerRoad), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:Road vehicles",
                Handler = OnDespawnerChange,
            };
        public static CheckboxOption DespawnerParked =
            new (nameof(Options.despawnerParked), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:Parked vehicles",
                Handler = OnDespawnerChange,
            };
        public static CheckboxOption DespawnerServices =
            new (nameof(Options.despawnerServices), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:Service vehicles",
                Handler = OnDespawnerChange,
            };
        public static CheckboxOption DespawnerTransport =
            new (nameof(Options.despawnerTransport), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:Public Transport vehicles",
                Handler = OnDespawnerChange,
            };
        public static CheckboxOption DespawnerPassengerTrains =
            new (nameof(Options.despawnerPassengerTrains), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:Passenger Trains",
                Handler = OnDespawnerChange,
            };
        public static CheckboxOption DespawnerCargoTrains =
            new (nameof(Options.despawnerCargoTrains), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:Cargo Trains",
                Handler = OnDespawnerChange,
            };
        public static CheckboxOption DespawnerAircraft =
            new (nameof(Options.despawnerAircraft), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:Aircraft",
                Handler = OnDespawnerChange,
            };
        public static CheckboxOption DespawnerShips =
            new (nameof(Options.despawnerShips), Options.PersistTo.None) {
                Label = "Despawn.Checkbox:Ships",
                Handler = OnDespawnerChange,
            };

        private static readonly Dictionary<CheckboxOption, ExtVehicleType> Despawners = new ()
        {
            { DespawnerRoad,
                ExtVehicleType.RoadVehicle },
            { DespawnerParked,
                ExtVehicleType.None }, // special case
            { DespawnerServices,
                ExtVehicleType.Service |
                ExtVehicleType.Emergency },
            { DespawnerTransport,
                ExtVehicleType.PublicTransport |
                ExtVehicleType.PassengerFerry |
                ExtVehicleType.PassengerBlimp |
                ExtVehicleType.PassengerPlane |
                ExtVehicleType.PassengerShip },
            { DespawnerPassengerTrains,
                ExtVehicleType.PassengerTrain },
            { DespawnerCargoTrains,
                ExtVehicleType.CargoTrain },
            { DespawnerAircraft,
                ExtVehicleType.Plane |
                ExtVehicleType.Blimp |
                ExtVehicleType.Helicopter },
            { DespawnerShips,
                ExtVehicleType.Ship |
                ExtVehicleType.Ferry },
        };

        // When true, `On...Change` will do nothing
        private static bool ignoreDespawnerChange = false;

        // Stores the compiled list of selected despawner flags
        // null = clear all traffic; ExtVehicleType.None = do nothing
        private static ExtVehicleType? despawnerMask = ExtVehicleType.None;

        public static void AddUI(UIHelperBase tab) {
            if (!TMPELifecycle.PlayMode) return;

            var group = tab.AddGroup(T("Maintenance.Group:Despawn"));

            DespawnerAll.AddUI(group);

            foreach (var despawner in Despawners) {
                despawner.Key.Indent = true;
                despawner.Key.AddUI(group);
            }

            group.AddButton(T("Maintenance.Despawn.Button:Despawn"), OnDespawnButtonClick);
        }

        private static string T(string text) {
            return Translation.Options.Get(text);
        }

        private static void OnDespawnButtonClick() {
            if (!TMPELifecycle.PlayMode) return;

            if (!despawnerMask.HasValue) {
                Singleton<SimulationManager>.instance.AddAction(() => {
                    UtilityManager.Instance.ClearTraffic();
                });
            } else if (despawnerMask != ExtVehicleType.None) {
                Singleton<SimulationManager>.instance.AddAction(() => {
                    UtilityManager.Instance.DespawnVehicles(despawnerMask);
                });
            }

            if (DespawnerParked) {
                Singleton<SimulationManager>.instance.AddAction(() => {
                    UtilityManager.Instance.RemoveParkedVehicles();
                });
            }
        }

        private static void OnDespawnerAllChange(bool selected) {
            if (ignoreDespawnerChange) return;
            ignoreDespawnerChange = true;

            foreach (var despawner in Despawners) {
                despawner.Key.Value = selected;
            }

            despawnerMask = selected ? null : ExtVehicleType.None;

            ignoreDespawnerChange = false;
        }

        private static void OnDespawnerChange(bool _) {
            if (ignoreDespawnerChange) return;
            ignoreDespawnerChange = true;

            ExtVehicleType mask = ExtVehicleType.None;
            int numSelected = 0;

            foreach (var despawner in Despawners) {
                if (despawner.Key) {
                    mask |= despawner.Value;
                    numSelected++;
                }
            }

            DespawnerAll.Value = numSelected == Despawners.Count;

            despawnerMask = DespawnerAll ? null : mask;

            ignoreDespawnerChange = false;
        }
    }
}
