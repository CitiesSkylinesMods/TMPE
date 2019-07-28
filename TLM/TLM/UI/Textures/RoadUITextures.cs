namespace TrafficManager.UI.Textures {
    using System.Collections.Generic;
    using API.Traffic.Enums;
    using UnityEngine;
    using Util;
    using static TextureResources;

    /// <summary>
    /// UI Textures for controlling road segments
    /// </summary>
    public static class RoadUITextures {
        public static readonly IDictionary<PriorityType, Texture2D> PrioritySignTextures;
        public static readonly Texture2D SignRemove;
        public static readonly IDictionary<ExtVehicleType, IDictionary<bool, Texture2D>> VehicleRestrictionTextures;
        public static readonly IDictionary<ExtVehicleType, Texture2D> VehicleInfoSignTextures;
        public static readonly IDictionary<bool, Texture2D> ParkingRestrictionTextures;

        static RoadUITextures() {

            // priority signs
            PrioritySignTextures = new TinyDictionary<PriorityType, Texture2D> {
                [PriorityType.None] = LoadDllResource("sign_none.png", 200, 200),
                [PriorityType.Main] = LoadDllResource("sign_priority.png", 200, 200),
                [PriorityType.Stop] = LoadDllResource("sign_stop.png", 200, 200),
                [PriorityType.Yield] = LoadDllResource("sign_yield.png", 200, 200)
            };

            // delete priority sign
            SignRemove = LoadDllResource("remove_signs.png", 256, 256);

            VehicleRestrictionTextures =
                new TinyDictionary<ExtVehicleType, IDictionary<bool, Texture2D>> {
                    [ExtVehicleType.Bus] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.CargoTrain] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.CargoTruck] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.Emergency] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.PassengerCar] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.PassengerTrain] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.Service] = new TinyDictionary<bool, Texture2D>(),
                    [ExtVehicleType.Taxi] = new TinyDictionary<bool, Texture2D>()
                };

            foreach (KeyValuePair<ExtVehicleType, IDictionary<bool, Texture2D>> e in
                VehicleRestrictionTextures) {
                foreach (bool b in new[] {false, true}) {
                    string suffix = b ? "allowed" : "forbidden";
                    e.Value[b] = LoadDllResource(
                        e.Key.ToString().ToLower() + "_" + suffix + ".png",
                        200,
                        200);
                }
            }

            ParkingRestrictionTextures = new TinyDictionary<bool, Texture2D>();
            ParkingRestrictionTextures[true] = LoadDllResource("parking_allowed.png", 200, 200);
            ParkingRestrictionTextures[false] = LoadDllResource("parking_disallowed.png", 200, 200);

            VehicleInfoSignTextures = new TinyDictionary<ExtVehicleType, Texture2D> {
                [ExtVehicleType.Bicycle] = LoadDllResource("bicycle_infosign.png", 449, 411),
                [ExtVehicleType.Bus] = LoadDllResource("bus_infosign.png", 449, 411),
                [ExtVehicleType.CargoTrain] = LoadDllResource("cargotrain_infosign.png", 449, 411),
                [ExtVehicleType.CargoTruck] = LoadDllResource("cargotruck_infosign.png", 449, 411),
                [ExtVehicleType.Emergency] = LoadDllResource("emergency_infosign.png", 449, 411),
                [ExtVehicleType.PassengerCar] = LoadDllResource("passengercar_infosign.png", 449, 411),
                [ExtVehicleType.PassengerTrain] = LoadDllResource("passengertrain_infosign.png", 449, 411)
            };
            VehicleInfoSignTextures[ExtVehicleType.RailVehicle] = VehicleInfoSignTextures[ExtVehicleType.PassengerTrain];
            VehicleInfoSignTextures[ExtVehicleType.Service] = LoadDllResource("service_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.Taxi] = LoadDllResource("taxi_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.Tram] = LoadDllResource("tram_infosign.png", 449, 411);
       }
    }
}