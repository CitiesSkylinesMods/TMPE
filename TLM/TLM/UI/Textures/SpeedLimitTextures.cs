namespace TrafficManager.UI.Textures {
    using static TextureResources;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;

    public static class SpeedLimitTextures {
        // We have texture files for every 5 kmph but speed limits palette allows every 10. This is
        // for more precise MPH display.
        internal const ushort KMPH_STEP = 10;
        internal const ushort UPPER_KMPH = 140;
        private const ushort LOAD_KMPH_STEP = 5;
        internal const ushort UPPER_MPH = 90;
        internal const ushort MPH_STEP = 5;

        /// <summary>Displayed in Override view for Speed Limits tool, when there's no override.</summary>
        public static readonly Texture2D NoOverride;

        /// <summary>Blue textures for road/lane default speed limits.</summary>
        public static readonly IDictionary<int, Texture2D> RoadDefaults;

        /// <summary>German style textures for KM/hour also usable for MPH.</summary>
        public static readonly IDictionary<int, Texture2D> TexturesKmph;

        /// <summary>White rectangular textures for MPH US style.</summary>
        public static readonly IDictionary<int, Texture2D> TexturesMphUS;

        /// <summary>British style speed limit textures for MPH</summary>
        public static readonly IDictionary<int, Texture2D> TexturesMphUK;

        public static readonly Texture2D Clear;

        static SpeedLimitTextures() {
            // TODO: Split loading here into dynamic sections, static enforces everything to stay in this ctor
            RoadDefaults = new Dictionary<int, Texture2D>();
            TexturesKmph = new Dictionary<int, Texture2D>();
            TexturesMphUS = new Dictionary<int, Texture2D>();
            TexturesMphUK = new Dictionary<int, Texture2D>();

            IntVector2 sizeSquare = new IntVector2(200);
            IntVector2 sizeUS = new IntVector2(200, 250);

            NoOverride = LoadDllResource(
                resourceName: "SpeedLimits.NoOverride.png",
                size: new IntVector2(200));

            // Load shared speed limit signs for Kmph and Mph
            // Assumes that signs from 0 to 140 with step 5 exist, 0 denotes no limit sign
            for (var speedLimit = 0; speedLimit <= 140; speedLimit += 5) {
                var resource = LoadDllResource(
                    resourceName: $"SpeedLimits.Kmh.{speedLimit}.png",
                    size: sizeSquare,
                    mip: true);
                TexturesKmph.Add(speedLimit, resource ? resource : TexturesKmph[5]);
            }

            for (var speedLimit = 0; speedLimit <= 140; speedLimit += 5) {
                var resource = LoadDllResource(
                    resourceName: $"SpeedLimits.RoadDefaults.{speedLimit}.png",
                    size: sizeSquare,
                    mip: true);
                RoadDefaults.Add(speedLimit, resource ? resource : RoadDefaults[5]);
            }

            // Signs from 0 to 90 for MPH
            for (var speedLimit = 0; speedLimit <= 90; speedLimit += 5) {
                // Load US textures, they are rectangular
                var resourceUs = LoadDllResource(
                    resourceName: $"SpeedLimits.Mph_US.{speedLimit}.png",
                    size: sizeUS,
                    mip: true);
                TexturesMphUS.Add(speedLimit, resourceUs ? resourceUs : TexturesMphUS[5]);

                // Load UK textures, they are square
                var resourceUk = LoadDllResource(
                    resourceName: $"SpeedLimits.Mph_UK.{speedLimit}.png",
                    size: sizeSquare,
                    mip: true);
                TexturesMphUK.Add(speedLimit, resourceUk ? resourceUk : TexturesMphUK[5]);
            }

            Clear = LoadDllResource("clear.png", new IntVector2(256));
        }

        // /// <summary>
        // /// Given the float speed, style and MPH option return a texture to render.
        // /// </summary>
        // /// <param name="spd">float speed</param>
        // /// <param name="mphStyle">Signs theme</param>
        // /// <param name="unit">Mph or km/h</param>
        // /// <returns></returns>
        // public static Texture2D GetSpeedLimitTexture(SpeedValue spd,
        //                                              MphSignStyle mphStyle,
        //                                              SpeedUnit unit) {
        //     // Select the source for the textures based on unit and the theme
        //     bool mph = unit == SpeedUnit.Mph;
        //     IDictionary<int, Texture2D> textures = TexturesKmph;
        //     if (mph) {
        //         switch (mphStyle) {
        //             case MphSignStyle.SquareUS:
        //                 textures = TexturesMphUS;
        //                 break;
        //             case MphSignStyle.RoundUK:
        //                 textures = TexturesMphUK;
        //                 break;
        //             case MphSignStyle.RoundGerman:
        //                 // Do nothing, this is the default above
        //                 break;
        //         }
        //     }
        //
        //     // Round to nearest 5 MPH or nearest 10 km/h
        //     ushort index = mph
        //                        ? spd.ToMphRounded(MPH_STEP).Mph
        //                        : spd.ToKmphRounded(KMPH_STEP).Kmph;
        //
        //     // Trim the index since 140 km/h / 90 MPH is the max sign we have
        //     ushort upper = mph ? UPPER_MPH : UPPER_KMPH;
        //
        //     // Show unlimited if the speed cannot be represented by the available sign textures
        //     if (index == 0 || index > upper) {
        //         // Log._Debug($"Trimming speed={speedLimit} index={index} to {upper}");
        //         return textures[0];
        //     }
        //
        //     // Trim from below to not go below index 5 (5 kmph or 5 mph)
        //     ushort trimIndex = Math.Max((ushort)5, index);
        //     return textures[trimIndex];
        // }

        /// <summary>
        /// Given the float speed, style and MPH option return a texture to render.
        /// </summary>
        /// <param name="spd">Speed to display.</param>
        /// <returns>Texture to display.</returns>
        public static Texture2D GetSpeedLimitTexture(SpeedValue spd,
                                                     IDictionary<int, Texture2D> textureSource) {
            // Select the source for the textures based on unit and the theme
            bool mph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;

            // Round to nearest 5 MPH or nearest 10 km/h
            ushort index = mph
                               ? spd.ToMphRounded(MPH_STEP).Mph
                               : spd.ToKmphRounded(KMPH_STEP).Kmph;

            // Trim the index since 140 km/h / 90 MPH is the max sign we have
            ushort upper = mph
                               ? UPPER_MPH
                               : UPPER_KMPH;

            // Show unlimited if the speed cannot be represented by the available sign textures
            if (index == 0 || index > upper) {
                // Log._Debug($"Trimming speed={speedLimit} index={index} to {upper}");
                return textureSource[0];
            }

            // Trim from below to not go below index 5 (5 kmph or 5 mph)
            ushort trimIndex = Math.Max((ushort)5, index);
            return textureSource[trimIndex];
        }

        /// <summary>For current display settings get texture dictionary with the road signs.</summary>
        /// <returns>Texture source (loaded textures with keys matching speeds).</returns>
        public static IDictionary<int, Texture2D> GetTextureSource() {
            var configMain = GlobalConfig.Instance.Main;
            // Select the source for the textures based on unit and the theme
            bool mph = configMain.DisplaySpeedLimitsMph;

            if (mph) {
                switch (configMain.MphRoadSignStyle) {
                    case MphSignStyle.SquareUS:
                        return TexturesMphUS;
                    case MphSignStyle.RoundUK:
                        return TexturesMphUK;
                    case MphSignStyle.RoundGerman:
                        // Do nothing, this is the default above
                        break;
                }
            }

            return TexturesKmph;
        }

        public static Vector2 DefaultSpeedlimitsAspectRatio() => Vector2.one;

        /// <summary>
        /// Returns vector of one for square/circle textures, or a proportionally scaled rect of
        /// width one, for rectangular US signs.
        /// </summary>
        /// <returns>Scalable vector of texture aspect ratio.</returns>
        public static Vector2 GetTextureAspectRatio() {
            var configMain = GlobalConfig.Instance.Main;
            SpeedUnit unit = configMain.GetDisplaySpeedUnit();

            // Select the source for the textures based on unit and the theme
            bool mph = unit == SpeedUnit.Mph;

            if (mph) {
                switch (configMain.MphRoadSignStyle) {
                    case MphSignStyle.SquareUS:
                        return new Vector2(1.0f / 1.25f, 1.0f);
                    case MphSignStyle.RoundUK:
                    case MphSignStyle.RoundGerman:
                        break;
                }
            }

            return Vector2.one;
        }
    }
}