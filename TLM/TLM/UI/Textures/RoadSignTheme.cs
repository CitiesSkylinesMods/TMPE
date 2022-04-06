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
    using Object = UnityEngine.Object;

    /// <summary>
    /// Defines one theme for road signs. All themes are accessible via, and stored in
    /// <see cref="RoadSignThemeManager"/>.
    /// </summary>
    public class RoadSignTheme {
        private IntVector2 TextureSize;

        /// <summary>Speed limit signs from 5 to 140 km (from 5 to 90 mph) and zero for no limit.</summary>
        public readonly Dictionary<int, Texture2D> Textures = new();

        private Dictionary<PriorityType, Texture2D> priority_ = new();

        public Texture2D Priority(PriorityType p) =>
            this.priority_.ContainsKey(p)
                ? this.priority_[p]
                : RoadSignThemeManager.Instance.FallbackTheme.priority_[p];

        private Dictionary<bool, Texture2D> parking_ = new();

        public Texture2D Parking(bool p) =>
            this.parking_.ContainsKey(p)
                ? this.parking_[p]
                : RoadSignThemeManager.Instance.FallbackTheme.parking_[p];

        /// <summary>
        /// Road signs for restrictions per vehicle types. Not all vehicle types have an icon,
        /// only those supported in <see cref="VehicleRestrictionsTool.RoadVehicleTypes"/>
        /// and <see cref="VehicleRestrictionsTool.RailVehicleTypes"/>.
        /// </summary>
        private Dictionary<ExtVehicleType, AllowDisallowTexture> restrictions_ = new();

        public Texture2D VehicleRestriction(ExtVehicleType type, bool allow) {
            if (allow) {
                return this.restrictions_.ContainsKey(type)
                           ? this.restrictions_[type].allow
                           : RoadSignThemeManager.Instance.FallbackTheme.restrictions_[type].allow;
            }

            return this.restrictions_.ContainsKey(type)
                       ? this.restrictions_[type].restrict
                       : RoadSignThemeManager.Instance.FallbackTheme.restrictions_[type].restrict;
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
                     kmphValue <= RoadSignThemeManager.UPPER_KMPH;
                     kmphValue += RoadSignThemeManager.LOAD_KMPH_STEP) {
                    this.SignValues.Add(kmphValue);
                }
            } else if (supportsMph) {
                for (var mphValue = 0;
                     mphValue <= RoadSignThemeManager.UPPER_MPH;
                     mphValue += RoadSignThemeManager.MPH_STEP) {
                    this.SignValues.Add(mphValue);
                }
            }
        }

        public RoadSignTheme Load(bool whiteTexture = false) {
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
                    mip: true,
                    failIfNotFound: false);
                this.Textures.Add(
                    speedLimit,
                    resource ? resource : Texture2D.whiteTexture);
            }

            LoadPrioritySign(p: PriorityType.None, name: "PriorityNone", whiteTexture);
            LoadPrioritySign(p: PriorityType.Main, name: "PriorityRightOfWay", whiteTexture);
            LoadPrioritySign(p: PriorityType.Yield, name: "PriorityYield", whiteTexture);
            LoadPrioritySign(p: PriorityType.Stop, name: "PriorityStop", whiteTexture);

            LoadParkingSign(allow: true, name: "Parking", whiteTexture);
            LoadParkingSign(allow: false, name: "NoParking", whiteTexture);

            LoadRestrictionSign(index: ExtVehicleType.PassengerCar, name: "PersonalCar", whiteTexture);
            LoadRestrictionSign(index: ExtVehicleType.Bus, name: "Bus", whiteTexture);
            LoadRestrictionSign(index: ExtVehicleType.Taxi, name: "Taxi", whiteTexture);
            LoadRestrictionSign(index: ExtVehicleType.CargoTruck, name: "Truck", whiteTexture);
            LoadRestrictionSign(index: ExtVehicleType.Service, name: "Service", whiteTexture);
            LoadRestrictionSign(index: ExtVehicleType.Emergency, name: "Emergency", whiteTexture);
            LoadRestrictionSign(index: ExtVehicleType.PassengerTrain, name: "PassengerTrain", whiteTexture);
            LoadRestrictionSign(index: ExtVehicleType.CargoTrain, name: "CargoTrain", whiteTexture);

            return this;
        }

        private void LoadPrioritySign(PriorityType p, string name, bool whiteTexture) {
            var tex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.{name}.png",
                size: new IntVector2(200),
                mip: true,
                failIfNotFound: false);

            if (tex != null) {
                this.priority_[p] = tex;
            } else if (whiteTexture) {
                this.priority_[p] = Texture2D.whiteTexture;
            }
        }

        private void LoadParkingSign(bool allow, string name, bool whiteTexture) {
            var tex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.{name}.png",
                size: new IntVector2(200),
                mip: true,
                failIfNotFound: false);

            if (tex != null) {
                this.parking_[allow] = tex;
            } else if (whiteTexture) {
                this.parking_[allow] = Texture2D.whiteTexture;
            }
        }

        public struct AllowDisallowTexture {
            public Texture2D allow;
            public Texture2D restrict;
        }

        private void LoadRestrictionSign(ExtVehicleType index,
                                         string name, bool whiteTexture) {
            var size200 = new IntVector2(200);
            Texture2D allowTex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.Allow-{name}.png",
                size: size200,
                mip: true,
                failIfNotFound: false);
            Texture2D restrictTex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.Restrict-{name}.png",
                size: size200,
                mip: true,
                failIfNotFound: false);
            if (allowTex && restrictTex) {
                var pairOfSigns = new AllowDisallowTexture {
                    allow = allowTex,
                    restrict = restrictTex,
                };
                this.restrictions_[index] = pairOfSigns;
            } else if (whiteTexture) {
                var whiteBox = new AllowDisallowTexture {
                    allow = Texture2D.whiteTexture,
                    restrict = Texture2D.whiteTexture,
                };
                this.restrictions_[index] = whiteBox;
            }
        }

        public void Unload() {
            // Speed limit textures
            foreach (var texture in this.Textures) {
                Object.Destroy(texture.Value);
            }

            this.Textures.Clear();

            // Priority signs
            foreach (var texture in this.priority_) {
                Object.Destroy(texture.Value);
            }

            this.priority_.Clear();

            // Parking signs
            foreach (var texture in this.parking_) {
                Object.Destroy(texture.Value);
            }

            this.parking_.Clear();

            // Vehicle Restriction signs
            foreach (var rs in this.restrictions_) {
                Object.Destroy(rs.Value.allow);
                Object.Destroy(rs.Value.restrict);
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
                               ? spd.ToMphRounded(RoadSignThemeManager.MPH_STEP).Mph
                               : spd.ToKmphRounded(RoadSignThemeManager.KMPH_STEP).Kmph;

            // Trim the index since 140 km/h / 90 MPH is the max sign we have
            ushort upper = mph ? RoadSignThemeManager.UPPER_MPH : RoadSignThemeManager.UPPER_KMPH;

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
                return RoadSignThemeManager.Instance.NoOverride;
            }
        }
    }
}