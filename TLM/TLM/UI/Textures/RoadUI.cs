namespace TrafficManager.UI.Textures {
    using System;
    using static TextureResources;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Singleton which manages UI Textures for controlling road segments.
    /// The textures are loaded when OnLevelLoaded event is fired from the <see cref="Lifecycle.TMPELifecycle"/>.
    /// </summary>
    public class RoadUI : AbstractCustomManager {
        public static RoadUI Instance = new();

        public Texture2D SignClear;

        /// <summary>Smaller Arrow-Down sign to be rendered as half-size for underground nodes.</summary>
        public Texture2D Underground;

        public IDictionary<ExtVehicleType, Texture2D> VehicleInfoSignTextures;

        public override void OnLevelLoading() {
            // delete priority sign
            SignClear = LoadDllResource("clear.png", new IntVector2(256));

            // Arrow down for underground nodes, rendered half-size
            Underground = LoadDllResource("Underground.png", new IntVector2(128), mip: true);

            IntVector2 signSize = new IntVector2(449, 411);

            VehicleInfoSignTextures = new Dictionary<ExtVehicleType, Texture2D> {
                [ExtVehicleType.Bicycle] = LoadDllResource("RoadUI.bicycle_infosign.png", signSize, true),
                [ExtVehicleType.Bus] = LoadDllResource("RoadUI.bus_infosign.png", signSize, true),
                [ExtVehicleType.CargoTrain] = LoadDllResource("RoadUI.cargotrain_infosign.png", signSize, true),
                [ExtVehicleType.CargoTruck] = LoadDllResource("RoadUI.cargotruck_infosign.png", signSize, true),
                [ExtVehicleType.Emergency] = LoadDllResource("RoadUI.emergency_infosign.png", signSize, true),
                [ExtVehicleType.PassengerCar] = LoadDllResource("RoadUI.passengercar_infosign.png", signSize, true),
                [ExtVehicleType.PassengerTrain] = LoadDllResource("RoadUI.passengertrain_infosign.png", signSize, true),
            };
            VehicleInfoSignTextures[ExtVehicleType.RailVehicle] = VehicleInfoSignTextures[ExtVehicleType.PassengerTrain];
            VehicleInfoSignTextures[ExtVehicleType.Service] = LoadDllResource("RoadUI.service_infosign.png", signSize);
            VehicleInfoSignTextures[ExtVehicleType.Taxi] = LoadDllResource("RoadUI.taxi_infosign.png", signSize);
            VehicleInfoSignTextures[ExtVehicleType.Tram] = LoadDllResource("RoadUI.tram_infosign.png", signSize);

            base.OnLevelLoading();
        }

        public override void OnLevelUnloading() {
            foreach (var t in VehicleInfoSignTextures) {
                UnityEngine.Object.Destroy(t.Value);
            }
            VehicleInfoSignTextures.Clear();

            UnityEngine.Object.Destroy(SignClear);
            UnityEngine.Object.Destroy(Underground);

            base.OnLevelUnloading();
        }
    }
}
