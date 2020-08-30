namespace TrafficManager.UI.Textures {
    using static TextureResources;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>UI Textures for controlling road segments.</summary>
    public static class RoadUI {
        public static readonly IDictionary<PriorityType, Texture2D> PrioritySignTextures;
        public static readonly Texture2D SignClear;
        public static readonly IDictionary<ExtVehicleType, IDictionary<bool, Texture2D>> VehicleRestrictionTextures;
        public static readonly IDictionary<ExtVehicleType, Texture2D> VehicleInfoSignTextures;
        public static readonly IDictionary<bool, Texture2D> ParkingRestrictionTextures;

        static RoadUI() {
            // priority signs
            PrioritySignTextures = new TinyDictionary<PriorityType, Texture2D> {
                [PriorityType.None] = LoadDllResource("RoadUI.sign_none.png", 200, 200),
                [PriorityType.Main] = LoadDllResource("RoadUI.sign_priority.png", 200, 200),
                [PriorityType.Stop] = LoadDllResource(Translation.GetTranslatedFileName("RoadUI.sign_stop.png"), 200, 200),
                [PriorityType.Yield] = LoadDllResource(Translation.GetTranslatedFileName("RoadUI.sign_yield.png"), 200, 200),
            };

            // delete priority sign
            SignClear = LoadDllResource("clear.png", 256, 256);

            VehicleRestrictionTextures =
                new TinyDictionary<ExtVehicleType, IDictionary<bool, Texture2D>> {
                    [ExtVehicleType.Bus] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.CargoTrain] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.CargoTruck] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.Emergency] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.PassengerCar] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.PassengerTrain] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.Service] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.Taxi] = new TinyDictionary<bool, Texture2D>(),
                };

            foreach (KeyValuePair<ExtVehicleType, IDictionary<bool, Texture2D>> e in
                VehicleRestrictionTextures) {
                foreach (bool b in new[] { false, true }) {
                    string suffix = b ? "allowed" : "forbidden";
                    e.Value[b] = LoadDllResource(
                        e.Key.ToString().ToLower() + "_" + suffix + ".png",
                        200,
                        200);
                }
            }

            ParkingRestrictionTextures = new TinyDictionary<bool, Texture2D>();
            ParkingRestrictionTextures[true] = LoadDllResource("RoadUI.parking_allowed.png", 200, 200);
            ParkingRestrictionTextures[false] = LoadDllResource("RoadUI.parking_disallowed.png", 200, 200);

            VehicleInfoSignTextures = new TinyDictionary<ExtVehicleType, Texture2D> {
                [ExtVehicleType.Bicycle] = LoadDllResource("RoadUI.bicycle_infosign.png", 449, 411),
                [ExtVehicleType.Bus] = LoadDllResource("RoadUI.bus_infosign.png", 449, 411),
                [ExtVehicleType.CargoTrain] = LoadDllResource("RoadUI.cargotrain_infosign.png", 449, 411),
                [ExtVehicleType.CargoTruck] = LoadDllResource("RoadUI.cargotruck_infosign.png", 449, 411),
                [ExtVehicleType.Emergency] = LoadDllResource("RoadUI.emergency_infosign.png", 449, 411),
                [ExtVehicleType.PassengerCar] = LoadDllResource("RoadUI.passengercar_infosign.png", 449, 411),
                [ExtVehicleType.PassengerTrain] = LoadDllResource("RoadUI.passengertrain_infosign.png", 449, 411),
            };
            VehicleInfoSignTextures[ExtVehicleType.RailVehicle] = VehicleInfoSignTextures[ExtVehicleType.PassengerTrain];
            VehicleInfoSignTextures[ExtVehicleType.Service] = LoadDllResource("RoadUI.service_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.Taxi] = LoadDllResource("RoadUI.taxi_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.Tram] = LoadDllResource("RoadUI.tram_infosign.png", 449, 411);
       }
    }
}