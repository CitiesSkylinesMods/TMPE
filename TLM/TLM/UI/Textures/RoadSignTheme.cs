namespace TrafficManager.UI.Textures {
    using System;
    using System.Collections.Generic;
    using System.Management.Instrumentation;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.UI;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.UI.SubTools;
    using TrafficManager.Util;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Defines one theme for road signs. All themes are accessible via, and stored in
    /// <see cref="RoadSignThemeManager"/>.
    /// </summary>
    public class RoadSignTheme : ITheme {
        public enum OtherRestriction {
            Crossing,
            EnterBlockedJunction,
            LaneChange,
            LeftOnRed,
            RightOnRed,
            UTurn,
        }

        public readonly string Name;

        // Kmph sign sets include range for MPH, but not all pictures are good to go with Kmph or Mph setting.
        // For example Canadian signs have all values to show MPH, but make no sense because the sign says km/h.

        /// <summary>Whether km/h signs range is supported from 5 to 140 step 5.</summary>
        public readonly bool SupportsKmph;

        /// <summary>Whether MPH signs range is supported from 5 to 90 step 5.</summary>
        public readonly bool SupportsMph;

        /// <summary>Speed limit signs from 5 to 140 km (from 5 to 90 mph) and zero for no limit.</summary>
        public readonly Dictionary<int, SpeedLimitSignTexture> Textures = new();

        /// <summary>Set to true if an attempt to find and load textures was made.</summary>
        public bool AttemptedToLoad = false;

        private bool HaveSpeedLimitSigns = false;

        /// <summary>
        /// Road signs for other restrictions such as pedestrian crossings, blocked junction, u-turn etc.
        /// </summary>
        private Dictionary<OtherRestriction, AllowDisallowTexture> otherRestrictions_ = new();

        /// <summary>
        /// This theme will be tried instead of the `Fallback` theme. Defaults to <see cref="RoadSignThemeManager.FallbackTheme"/>.
        /// </summary>
        private RoadSignTheme ParentTheme;

        private Dictionary<bool, Texture2D> parking_ = new();

        private string PathPrefix;

        private Dictionary<PriorityType, Texture2D> priority_ = new();

        /// <summary>This list of required speed signs is used for loading.</summary>
        private List<int> SignValues = new();

        private IntVector2 TextureSize;

        /// <summary>
        /// Road signs for restrictions per vehicle types. Not all vehicle types have an icon,
        /// only those supported in <see cref="VehicleRestrictionsTool.RoadVehicleTypes"/>
        /// and <see cref="VehicleRestrictionsTool.RailVehicleTypes"/>.
        /// </summary>
        private Dictionary<ExtVehicleType, AllowDisallowTexture> vehicleRestrictions_ = new();

        public RoadSignTheme(string name,
                             bool supportsMph,
                             bool supportsKmph,
                             IntVector2 size,
                             string pathPrefix,
                             [CanBeNull]
                             RoadSignTheme parentTheme = null,
                             bool speedLimitSigns = true) {
            Log._DebugIf(
                this.TextureSize.x <= this.TextureSize.y,
                () =>
                    $"Constructing a road sign theme {pathPrefix}: Portrait oriented size not supported");

            this.Name = name;
            this.SupportsMph = supportsMph;
            this.SupportsKmph = supportsKmph;
            this.PathPrefix = pathPrefix;
            this.TextureSize = size;
            this.ParentTheme = parentTheme;
            this.HaveSpeedLimitSigns = speedLimitSigns;

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

        public Texture2D Priority(PriorityType p) =>
            this.priority_.ContainsKey(p)
                ? this.priority_[p]
                : this.ParentTheme.Priority(p);

        public Texture2D Parking(bool p) =>
            this.parking_.ContainsKey(p)
                ? this.parking_[p]
                : this.ParentTheme.Parking(p);

        public Texture2D VehicleRestriction(ExtVehicleType type, bool allow) {
            return VehicleRestriction(type, allow, false);
        }

        public Texture2D VehicleRestriction(ExtVehicleType type, bool allow, bool disabled) {
            if (allow) {
                return this.vehicleRestrictions_.ContainsKey(type)
                           ? !disabled ? this.vehicleRestrictions_[type].allow : this.vehicleRestrictions_[type].allowDisabled
                           : this.ParentTheme.VehicleRestriction(type, allow: true, disabled: disabled);
            }

            return this.vehicleRestrictions_.ContainsKey(type)
                       ? !disabled ? this.vehicleRestrictions_[type].restrict : this.vehicleRestrictions_[type].restrictDisabled
                       : this.ParentTheme.VehicleRestriction(type, allow: false, disabled: disabled);
        }

        /// <summary>
        /// Returns road sign for other restrictions (pedestrian, blocked junction, u-turn etc).
        /// </summary>
        /// <param name="type">The restriction we need.</param>
        /// <param name="allow">Allow or restrict.</param>
        public Texture2D GetOtherRestriction(OtherRestriction type, bool allow) {
            if (allow) {
                return this.otherRestrictions_.ContainsKey(type)
                           ? this.otherRestrictions_[type].allow
                           : this.ParentTheme.GetOtherRestriction(type, allow: true);
            }

            return this.otherRestrictions_.ContainsKey(type)
                       ? this.otherRestrictions_[type].restrict
                       : this.ParentTheme.GetOtherRestriction(type, allow: false);
        }

        public Texture2D JunctionRestriction(JunctionRestrictionFlags rule, bool allowed) {
            bool rht = Shortcuts.RHT;
            switch (rule) {
                case JunctionRestrictionFlags.AllowPedestrianCrossing:
                    return GetOtherRestriction(OtherRestriction.Crossing, allowed);
                case JunctionRestrictionFlags.AllowUTurn:
                    return GetOtherRestriction(OtherRestriction.UTurn, allowed);
                case JunctionRestrictionFlags.AllowEnterWhenBlocked:
                    return GetOtherRestriction(OtherRestriction.EnterBlockedJunction, allowed);
                case JunctionRestrictionFlags.AllowForwardLaneChange:
                    return GetOtherRestriction(OtherRestriction.LaneChange, allowed);
                case JunctionRestrictionFlags.AllowFarTurnOnRed when rht:
                case JunctionRestrictionFlags.AllowNearTurnOnRed when !rht:
                    return GetOtherRestriction(OtherRestriction.LeftOnRed, allowed);
                case JunctionRestrictionFlags.AllowNearTurnOnRed when rht:
                case JunctionRestrictionFlags.AllowFarTurnOnRed when !rht:
                    return GetOtherRestriction(OtherRestriction.RightOnRed, allowed);
                default:
                    Log.Error($"could not get texture for {rule}.");
                    return null;
            }
        }

        public Texture2D TrafficLightIcon(ushort nodeId) {
            if (!TrafficLightManager.Instance.HasTrafficLight(nodeId)) {
                return TrafficLightTextures.Instance.TrafficLightDisabled;
            } else if (TrafficLightSimulationManager.Instance.HasSimulation(nodeId)) {
                return TrafficLightTextures.Instance.TrafficLightEnabledTimed;
            } else {
                return TrafficLightTextures.Instance.TrafficLightEnabled;
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
                    logIfNotFound: false);
                if (resource) {
                    var disabledVarinat = TextureUtil.ToGrayscale(resource);
                    this.Textures.Add(speedLimit, new SpeedLimitSignTexture() {regular = resource, disabled = disabledVarinat});
                } else {
                    this.Textures.Add(speedLimit, new SpeedLimitSignTexture() {regular = Texture2D.whiteTexture, disabled = Texture2D.whiteTexture});
                }
            }

            LoadPrioritySign(p: PriorityType.None, name: "PriorityNone", whiteTexture);
            LoadPrioritySign(p: PriorityType.Main, name: "PriorityRightOfWay", whiteTexture);
            LoadPrioritySign(p: PriorityType.Yield, name: "PriorityYield", whiteTexture);
            LoadPrioritySign(p: PriorityType.Stop, name: "PriorityStop", whiteTexture);

            LoadParkingSign(allow: true, name: "Parking", whiteTexture);
            LoadParkingSign(allow: false, name: "NoParking", whiteTexture);

            // Load Vehicle type restrictions
            LoadVehicleRestrictionSign(ExtVehicleType.PassengerCar, "PersonalCar", whiteTexture);
            LoadVehicleRestrictionSign(ExtVehicleType.Bus, "Bus", whiteTexture);
            LoadVehicleRestrictionSign(ExtVehicleType.Taxi, "Taxi", whiteTexture);
            LoadVehicleRestrictionSign(ExtVehicleType.CargoTruck, "Truck", whiteTexture);
            LoadVehicleRestrictionSign(ExtVehicleType.Service, "Service", whiteTexture);
            LoadVehicleRestrictionSign(ExtVehicleType.Emergency, "Emergency", whiteTexture);
            LoadVehicleRestrictionSign(
                ExtVehicleType.PassengerTrain,
                "PassengerTrain",
                whiteTexture);
            LoadVehicleRestrictionSign(ExtVehicleType.CargoTrain, "CargoTrain", whiteTexture);

            // Load other restrictions
            LoadOtherRestrictionSign(OtherRestriction.Crossing, "PedestrianCrossing", whiteTexture);
            LoadOtherRestrictionSign(
                OtherRestriction.EnterBlockedJunction,
                "EnterBlocked",
                whiteTexture);
            LoadOtherRestrictionSign(OtherRestriction.LaneChange, "LaneChange", whiteTexture);
            LoadOtherRestrictionSign(OtherRestriction.LeftOnRed, "LeftOnRed", whiteTexture);
            LoadOtherRestrictionSign(OtherRestriction.RightOnRed, "RightOnRed", whiteTexture);
            LoadOtherRestrictionSign(OtherRestriction.UTurn, "UTurn", whiteTexture);

            // Setup parent theme to be `Fallback` theme if ParentTheme is null
            // For Fallback theme itself, keep it null.
            if (this.ParentTheme == null
                && !System.Object.ReferenceEquals(
                    this,
                    RoadSignThemeManager.Instance.FallbackTheme)) {
                this.ParentTheme = RoadSignThemeManager.Instance.FallbackTheme;
            }

            this.ParentTheme?.Load(); // Reload parent theme if necessary

            return this;
        }

        private void LoadPrioritySign(PriorityType p, string name, bool whiteTexture) {
            var tex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.{name}.png",
                size: new IntVector2(200),
                mip: true,
                logIfNotFound: false);

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
                logIfNotFound: false);

            if (tex != null) {
                this.parking_[allow] = tex;
            } else if (whiteTexture) {
                this.parking_[allow] = Texture2D.whiteTexture;
            }
        }

        /// <summary>
        /// Generic function to load Allow-X and Restrict-X textures into either
        /// <see cref="vehicleRestrictions_"/> or <see cref="otherRestrictions_"/>
        /// </summary>
        /// <param name="index">The key to store as.</param>
        /// <param name="dict">The destination dictionary.</param>
        /// <param name="name">Name to append to either Allow- or Restrict-.</param>
        /// <param name="whiteTexture">Create record even if resource is missing, using white texture.</param>
        /// <typeparam name="TIndex">Type of the key.</typeparam>
        private void LoadRestrictionSignGeneric<TIndex>(
            TIndex index,
            Dictionary<TIndex, AllowDisallowTexture> dict,
            string name,
            bool whiteTexture,
            int sizeHint,
            bool createDisabledVariant = false) {
            var size = new IntVector2(sizeHint);
            Texture2D allowTex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.Allow-{name}.png",
                size: size,
                mip: true,
                logIfNotFound: false);
            Texture2D restrictTex = TextureResources.LoadDllResource(
                resourceName: $"{this.PathPrefix}.Restrict-{name}.png",
                size: size,
                mip: true,
                logIfNotFound: false);
            if (allowTex && restrictTex) {
                var pairOfSigns = new AllowDisallowTexture {
                    allow = allowTex,
                    restrict = restrictTex,
                    allowDisabled = Texture2D.whiteTexture,
                    restrictDisabled = Texture2D.whiteTexture,
                };
                if (createDisabledVariant) {
                    Texture2D allowDisabledTex = TextureUtil.ToGrayscale(allowTex);
                    Texture2D restrictDisabledTex = TextureUtil.ToGrayscale(restrictTex);
                    pairOfSigns.allowDisabled = allowDisabledTex;
                    pairOfSigns.restrictDisabled = restrictDisabledTex;
                }
                dict[index] = pairOfSigns;
            } else if (whiteTexture) {
                var whiteBox = new AllowDisallowTexture {
                    allow = Texture2D.whiteTexture,
                    allowDisabled = Texture2D.whiteTexture,
                    restrict = Texture2D.whiteTexture,
                    restrictDisabled = Texture2D.whiteTexture,
                };
                dict[index] = whiteBox;
            }
        }

        private void LoadVehicleRestrictionSign(ExtVehicleType index,
                                                string name,
                                                bool whiteTexture) {
            LoadRestrictionSignGeneric(index, this.vehicleRestrictions_, name, whiteTexture, 200, createDisabledVariant: true);
        }

        private void LoadOtherRestrictionSign(OtherRestriction index,
                                              string name,
                                              bool whiteTexture) {
            LoadRestrictionSignGeneric(index, this.otherRestrictions_, name, whiteTexture, 256, createDisabledVariant: false);
        }

        private void DestroyTexture(Texture2D t) {
            // Only destroy our textures, don't try to destroy UnityWhite
            if (!System.Object.ReferenceEquals(t, Texture2D.whiteTexture)) {
                UnityEngine.Object.Destroy(t);
            }
        }

        public void Unload() {
            this.ParentTheme?.Unload(); // Unload parent theme if necessary

            // Speed limit textures
            foreach (var texture in this.Textures) {
                DestroyTexture(texture.Value.regular);
                DestroyTexture(texture.Value.disabled);
            }

            this.Textures.Clear();

            // Priority signs
            foreach (var texture in this.priority_) {
                DestroyTexture(texture.Value);
            }

            this.priority_.Clear();

            // Parking signs
            foreach (var texture in this.parking_) {
                DestroyTexture(texture.Value);
            }

            this.parking_.Clear();

            // Vehicle Restriction signs
            foreach (var rs in this.vehicleRestrictions_) {
                DestroyTexture(rs.Value.allow);
                DestroyTexture(rs.Value.allowDisabled);
                DestroyTexture(rs.Value.restrict);
                DestroyTexture(rs.Value.restrictDisabled);
            }

            // Other Restriction signs
            foreach (var rs in this.otherRestrictions_) {
                DestroyTexture(rs.Value.allow);
                DestroyTexture(rs.Value.restrict);
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

        /// <summary>
        /// Given the speed, return a texture to render.
        /// Does not use <see cref="ParentTheme"/> assuming that all speeds must be covered in a theme.
        /// </summary>
        /// <param name="spd">Speed to display.</param>
        /// <param name="disabled">Should use disabled version of sign (grayscale)</param>
        /// <returns>Texture to display.</returns>
        public Texture2D SpeedLimitTexture(SpeedValue spd, bool disabled = false) {
            if (!this.HaveSpeedLimitSigns) {
                return this.ParentTheme.SpeedLimitTexture(spd, disabled);
            }

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
                    return disabled ? this.Textures[0].disabled : this.Textures[0].regular;
                }

                // Trim from below to not go below index 5 (5 kmph or 5 mph)
                ushort trimIndex = Math.Max((ushort)5, index);
                return disabled ? this.Textures[trimIndex].disabled : this.Textures[trimIndex].regular;
            }
            catch (KeyNotFoundException) {
                return RoadSignThemeManager.Instance.NoOverride;
            }
        }

        public struct AllowDisallowTexture {
            public Texture2D allow;
            public Texture2D allowDisabled;
            public Texture2D restrict;
            public Texture2D restrictDisabled;
        }

        public struct SpeedLimitSignTexture {
            public Texture2D regular;
            public Texture2D disabled;
        }
    }
}