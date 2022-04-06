namespace TrafficManager.UI.Textures {
    using System;
    using System.Collections.Generic;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State;
    using TrafficManager.UI.SubTools;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Defines one theme for road signs. All themes are accessible via, and stored in
    /// <see cref="RoadSignThemes"/>.
    /// </summary>
    public class RoadSignTheme {
        private IntVector2 TextureSize;

        /// <summary>Speed limit signs from 5 to 140 km (from 5 to 90 mph) and zero for no limit.</summary>
        public readonly Dictionary<int, Texture2D> Textures = new();

        private Dictionary<PriorityType, Texture2D> priority_ = new();

        public Texture2D Priority(PriorityType p) =>
            this.priority_.ContainsKey(p)
                ? this.priority_[p]
                : RoadSignThemes.Instance.FallbackTheme.priority_[p];

        private Dictionary<bool, Texture2D> parking_ = new();

        public Texture2D Parking(bool p) =>
            this.parking_.ContainsKey(p)
                ? this.parking_[p]
                : RoadSignThemes.Instance.FallbackTheme.parking_[p];

        /// <summary>
        /// Road signs for restrictions per vehicle types. Not all vehicle types have an icon,
        /// only those supported in <see cref="VehicleRestrictionsTool.RoadVehicleTypes"/>
        /// and <see cref="VehicleRestrictionsTool.RailVehicleTypes"/>.
        /// </summary>
        public Dictionary<ExtVehicleType, RoadSignThemes.RestrictionTextureDef> restrictions_ =
            new();

        public Texture2D VehicleRestriction(ExtVehicleType type, bool allow) {
            if (allow) {
                return this.restrictions_.ContainsKey(type)
                    ? this.restrictions_[type].allow
                    : RoadSignThemes.Instance.FallbackTheme.restrictions_[type].allow;
            }

            return this.restrictions_.ContainsKey(type)
                       ? this.restrictions_[type].restrict
                       : RoadSignThemes.Instance.FallbackTheme.restrictions_[type].restrict;
        }

        /// <summary>This list of required speed signs is used for loading.</summary>
        private List<int> SignValues = new();

        private string PathPrefix;

        // Kmph sign sets include range for MPH, but not all pictures are good to go with Kmph or Mph setting.
        // For example Canadian signs have all values to show MPH, but make no sense because the sign says km/h.

        /// <summary>Whether km/h signs range is supported from 5 to 140 step 5.</summary>
        public readonly bool SupportsKmph;

        /// <summary>Whether MPH signs range is supported from 5 to 90 step 5.</summary>
        public readonly bool SupportsMph;

        public readonly string Name;

        /// <summary>Set to true if an attempt to find and load textures was made.</summary>
        public bool AttemptedToLoad = false;

        public RoadSignTheme(string name,
                             bool supportsMph,
                             bool supportsKmph,
                             IntVector2 size,
                             string pathPrefix) {
            Log._DebugIf(
                this.TextureSize.x <= this.TextureSize.y,
                () =>
                    $"Constructing a road sign theme {pathPrefix}: Portrait oriented size not supported");

            this.Name = name;
            this.SupportsMph = supportsMph;
            this.SupportsKmph = supportsKmph;
            this.PathPrefix = pathPrefix;
            this.TextureSize = size;

            if (supportsKmph) {
                // Assumes that signs from 0 to 140 with step 5 exist, 0 denotes no-limit sign
                for (var kmphValue = 0;
                     kmphValue <= RoadSignThemes.UPPER_KMPH;
                     kmphValue += RoadSignThemes.LOAD_KMPH_STEP) {
                    this.SignValues.Add(kmphValue);
                }
            } else if (supportsMph) {
                for (var mphValue = 0;
                     mphValue <= RoadSignThemes.UPPER_MPH;
                     mphValue += RoadSignThemes.MPH_STEP) {
                    this.SignValues.Add(mphValue);
                }
            }
        }

        public RoadSignTheme Load() {
            if (this.AttemptedToLoad) {
                return this;
            }

            this.Textures.Clear();
            this.AttemptedToLoad = true;

            foreach (var speedLimit in this.SignValues) {
                // Log._Debug($"Loading sign texture {this.PathPrefix}.{speedLimit}.png");
                var resource = TextureResources.LoadDllResource(
                    resourceName: $"{this.PathPrefix}.{speedLimit}.png",
                    size: this.TextureSize,
                    mip: true);
                this.Textures.Add(
                    speedLimit,
                    resource ? resource : RoadSignThemes.Instance.Clear);
            }

            LoadPrioritySign(p: PriorityType.None, name: "PriorityNone");
            LoadPrioritySign(p: PriorityType.Main, name: "PriorityRightOfWay");
            LoadPrioritySign(p: PriorityType.Yield, name: "PriorityYield");
            LoadPrioritySign(p: PriorityType.Stop, name: "PriorityStop");

            LoadParkingSign(allow: true, name: "Parking");
            LoadParkingSign(allow: false, name: "NoParking");

            LoadRestrictionSign(index: ExtVehicleType.PassengerCar, name: "PersonalCar");
            LoadRestrictionSign(index: ExtVehicleType.Bus, name: "Bus");
            LoadRestrictionSign(index: ExtVehicleType.Taxi, name: "Taxi");
            LoadRestrictionSign(index: ExtVehicleType.CargoTruck, name: "Truck");
            LoadRestrictionSign(index: ExtVehicleType.Service, name: "Service");
            LoadRestrictionSign(index: ExtVehicleType.Emergency, name: "Emergency");
            LoadRestrictionSign(index: ExtVehicleType.PassengerTrain, name: "PassengerTrain");
            LoadRestrictionSign(index: ExtVehicleType.CargoTrain, name: "CargoTrain");

            return this;
        }

        private void LoadPrioritySign(PriorityType p, string name) {
            var tex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.{name}.png",
                size: new IntVector2(200),
                mip: true,
                failIfNotFound: false);

            if (tex != null) {
                this.priority_[p] = tex;
            }
        }

        private void LoadParkingSign(bool allow, string name) {
            var tex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.{name}.png",
                size: new IntVector2(200),
                mip: true,
                failIfNotFound: false);

            if (tex != null) {
                this.parking_[allow] = tex;
            }
        }

        private void LoadRestrictionSign(ExtVehicleType index,
                                         string name) {
            var size200 = new IntVector2(200);
            var signs = new RoadSignThemes.RestrictionTextureDef {
                allow = TextureResources.LoadDllResource(
                    resourceName: $"{this.PathPrefix}.Allow-{name}.png",
                    size: size200,
                    mip: true,
                    failIfNotFound: false),
                restrict = TextureResources.LoadDllResource(
                    resourceName: $"{this.PathPrefix}.Restrict-{name}.png",
                    size: size200,
                    mip: true,
                    failIfNotFound: false),
            };
            this.restrictions_[index] = signs;
        }

        public void Unload() {
            // Speed limit textures
            foreach (var texture in this.Textures) {
                UnityEngine.Object.Destroy(texture.Value);
            }

            this.Textures.Clear();

            // Priority signs
            foreach (var texture in this.priority_) {
                UnityEngine.Object.Destroy(texture.Value);
            }

            this.priority_.Clear();

            // Parking signs
            foreach (var texture in this.parking_) {
                UnityEngine.Object.Destroy(texture.Value);
            }

            this.parking_.Clear();

            // Vehicle Restriction signs
            foreach (var rs in this.restrictions_) {
                UnityEngine.Object.Destroy(rs.Value.allow);
                UnityEngine.Object.Destroy(rs.Value.restrict);
            }

            this.parking_.Clear();

            this.AttemptedToLoad = false;
        }

        /// <summary>
        /// Assumes that signs can be square or vertical rectangle, no horizontal themes.
        /// Aspect ratio value which scales width down to have height fully fit.
        /// </summary>
        public Vector2 GetAspectRatio() {
            return new(this.TextureSize.x / (float)this.TextureSize.y, 1.0f);
        }

        /// <summary>Given the speed, return a texture to render.</summary>
        /// <param name="spd">Speed to display.</param>
        /// <returns>Texture to display.</returns>
        public Texture2D GetTexture(SpeedValue spd) {
            // Round to nearest 5 MPH or nearest 5 km/h
            bool mph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
            ushort index = mph
                               ? spd.ToMphRounded(RoadSignThemes.MPH_STEP).Mph
                               : spd.ToKmphRounded(RoadSignThemes.KMPH_STEP).Kmph;

            // Trim the index since 140 km/h / 90 MPH is the max sign we have
            ushort upper = mph ? RoadSignThemes.UPPER_MPH : RoadSignThemes.UPPER_KMPH;

            try {
                // Show unlimited if the speed cannot be represented by the available sign textures
                if (index == 0 || index > upper) {
                    return this.Textures[0];
                }

                // Trim from below to not go below index 5 (5 kmph or 5 mph)
                ushort trimIndex = Math.Max((ushort)5, index);
                return this.Textures[trimIndex];
            }
            catch (KeyNotFoundException) {
                return RoadSignThemes.Instance.NoOverride;
            }
        }
    }
}