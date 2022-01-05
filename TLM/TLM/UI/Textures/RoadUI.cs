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

        [Obsolete("These are now available via RoadSignThemes.ActiveTheme.Priority(PriorityType p)")]
        public IDictionary<PriorityType, Texture2D> PrioritySignTextures;

        public Texture2D SignClear;

        /// <summary>Smaller Arrow-Down sign to be rendered as half-size for underground nodes.</summary>
        public Texture2D Underground;

        public IDictionary<ExtVehicleType, IDictionary<bool, Texture2D>> VehicleRestrictionTextures;
        public IDictionary<ExtVehicleType, Texture2D> VehicleInfoSignTextures;

        [Obsolete("These are now available via RoadSignThemes.ActiveTheme.Parking(bool)")]
        public IDictionary<bool, Texture2D> ParkingRestrictionTextures;

        public void ReloadTexturesWithTranslation() {
            IntVector2 size = new IntVector2(200);
            Texture2D stopTexture = PrioritySignTextures[PriorityType.Stop];
            if (stopTexture) {
                UnityEngine.GameObject.Destroy(stopTexture);
            }

            PrioritySignTextures[PriorityType.Stop] = LoadDllResource(Translation.GetTranslatedFileName("RoadUI.sign_stop.png"), size);

            Texture2D yieldTexture = PrioritySignTextures[PriorityType.Yield];
            if (yieldTexture) {
                UnityEngine.GameObject.Destroy(yieldTexture);
            }

            PrioritySignTextures[PriorityType.Yield] = LoadDllResource(Translation.GetTranslatedFileName("RoadUI.sign_yield.png"), size);
        }

        public override void OnLevelLoading() {
            IntVector2 size = new IntVector2(200);

            // priority signs
            PrioritySignTextures = new Dictionary<PriorityType, Texture2D> {
                [PriorityType.None] = LoadDllResource("RoadUI.sign_none.png", size),
                [PriorityType.Main] = LoadDllResource("RoadUI.sign_priority.png", size),
                [PriorityType.Stop] = LoadDllResource(Translation.GetTranslatedFileName("RoadUI.sign_stop.png"), size),
                [PriorityType.Yield] = LoadDllResource(Translation.GetTranslatedFileName("RoadUI.sign_yield.png"), size),
            };

            // delete priority sign
            SignClear = LoadDllResource("clear.png", new IntVector2(256));

            // Arrow down for underground nodes, rendered half-size
            Underground = LoadDllResource("Underground.png", new IntVector2(128), mip: true);

            VehicleRestrictionTextures =
                new Dictionary<ExtVehicleType, IDictionary<bool, Texture2D>> {
                    [ExtVehicleType.Bus] = new Dictionary<bool, Texture2D>(),
                    [ExtVehicleType.CargoTrain] = new Dictionary<bool, Texture2D>(),
                    [ExtVehicleType.CargoTruck] = new Dictionary<bool, Texture2D>(),
                    [ExtVehicleType.Emergency] = new Dictionary<bool, Texture2D>(),
                    [ExtVehicleType.PassengerCar] = new Dictionary<bool, Texture2D>(),
                    [ExtVehicleType.PassengerTrain] = new Dictionary<bool, Texture2D>(),
                    [ExtVehicleType.Service] = new Dictionary<bool, Texture2D>(),
                    [ExtVehicleType.Taxi] = new Dictionary<bool, Texture2D>(),
                };

            foreach (KeyValuePair<ExtVehicleType, IDictionary<bool, Texture2D>> e in
                VehicleRestrictionTextures) {
                foreach (bool b in new[] { false, true }) {
                    string suffix = b ? "allowed" : "forbidden";
                    e.Value[b] = LoadDllResource(
                        resourceName: e.Key.ToString().ToLower() + "_" + suffix + ".png",
                        size: size);
                }
            }

            ParkingRestrictionTextures = new Dictionary<bool, Texture2D>();
            ParkingRestrictionTextures[true] = LoadDllResource("RoadUI.parking_allowed.png", size);
            ParkingRestrictionTextures[false] = LoadDllResource("RoadUI.parking_disallowed.png", size);

            IntVector2 signSize = new IntVector2(449, 411);

            VehicleInfoSignTextures = new Dictionary<ExtVehicleType, Texture2D> {
                [ExtVehicleType.Bicycle] = LoadDllResource("RoadUI.bicycle_infosign.png", signSize),
                [ExtVehicleType.Bus] = LoadDllResource("RoadUI.bus_infosign.png", signSize),
                [ExtVehicleType.CargoTrain] = LoadDllResource("RoadUI.cargotrain_infosign.png", signSize),
                [ExtVehicleType.CargoTruck] = LoadDllResource("RoadUI.cargotruck_infosign.png", signSize),
                [ExtVehicleType.Emergency] = LoadDllResource("RoadUI.emergency_infosign.png", signSize),
                [ExtVehicleType.PassengerCar] = LoadDllResource("RoadUI.passengercar_infosign.png", signSize),
                [ExtVehicleType.PassengerTrain] = LoadDllResource("RoadUI.passengertrain_infosign.png", signSize),
            };
            VehicleInfoSignTextures[ExtVehicleType.RailVehicle] = VehicleInfoSignTextures[ExtVehicleType.PassengerTrain];
            VehicleInfoSignTextures[ExtVehicleType.Service] = LoadDllResource("RoadUI.service_infosign.png", signSize);
            VehicleInfoSignTextures[ExtVehicleType.Taxi] = LoadDllResource("RoadUI.taxi_infosign.png", signSize);
            VehicleInfoSignTextures[ExtVehicleType.Tram] = LoadDllResource("RoadUI.tram_infosign.png", signSize);

            base.OnLevelLoading();
        }

        public override void OnLevelUnloading() {
            foreach (var t in PrioritySignTextures) {
                UnityEngine.Object.Destroy(t.Value);
            }
            PrioritySignTextures.Clear();

            foreach (var vrt in VehicleRestrictionTextures) {
                foreach (var t in vrt.Value) {
                    UnityEngine.Object.Destroy(t.Value);
                }
            }
            VehicleRestrictionTextures.Clear();

            foreach (var t in VehicleInfoSignTextures) {
                UnityEngine.Object.Destroy(t.Value);
            }
            VehicleInfoSignTextures.Clear();

            foreach (var t in ParkingRestrictionTextures) {
                UnityEngine.Object.Destroy(t.Value);
            }
            ParkingRestrictionTextures.Clear();

            UnityEngine.Object.Destroy(SignClear);
            UnityEngine.Object.Destroy(Underground);

            base.OnLevelUnloading();
        }
    }
}
