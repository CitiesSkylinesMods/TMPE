namespace TrafficManager.UI.Textures {
    using static TextureResources;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;

    public static class SpeedLimitTextures {
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
            RoadDefaults = new TinyDictionary<int, Texture2D>();
            TexturesKmph = new TinyDictionary<int, Texture2D>();
            TexturesMphUS = new TinyDictionary<int, Texture2D>();
            TexturesMphUK = new TinyDictionary<int, Texture2D>();

            IntVector2 size = new IntVector2(200);
            IntVector2 sizeUS = new IntVector2(200, 250);

            // Load shared speed limit signs for Kmph and Mph
            // Assumes that signs from 0 to 140 with step 5 exist, 0 denotes no limit sign
            for (var speedLimit = 0; speedLimit <= 140; speedLimit += 5) {
                var resource = LoadDllResource($"SpeedLimits.Kmh.{speedLimit}.png", size, true);
                TexturesKmph.Add(speedLimit, resource ?? TexturesKmph[5]);
            }

            for (var speedLimit = 0; speedLimit <= 140; speedLimit += 5) {
                var resource = LoadDllResource($"SpeedLimits.RoadDefaults.{speedLimit}.png", size, true);
                RoadDefaults.Add(speedLimit, resource ?? RoadDefaults[5]);
            }

            // Signs from 0 to 90 for MPH
            for (var speedLimit = 0; speedLimit <= 90; speedLimit += 5) {
                // Load US textures, they are rectangular
                var resourceUs = LoadDllResource($"SpeedLimits.Mph_US.{speedLimit}.png", sizeUS, true);
                TexturesMphUS.Add(speedLimit, resourceUs ?? TexturesMphUS[5]);

                // Load UK textures, they are square
                var resourceUk = LoadDllResource($"SpeedLimits.Mph_UK.{speedLimit}.png", size, true);
                TexturesMphUK.Add(speedLimit, resourceUk ?? TexturesMphUK[5]);
            }

            Clear = LoadDllResource("clear.png", new IntVector2(256));
        }

        /// <summary>
        /// Given the float speed, style and MPH option return a texture to render.
        /// </summary>
        /// <param name="spd">Speed to display.</param>
        /// <returns>Texture to display.</returns>
        public static Texture2D GetSpeedLimitTexture(SpeedValue spd,
                                                     IDictionary<int, Texture2D> textureSource) {
            // Select the source for the textures based on unit and the theme
            State.ConfigData.Main m = GlobalConfig.Instance.Main;
            SpeedUnit unit = m.DisplaySpeedLimitsMph ? SpeedUnit.Mph : SpeedUnit.Kmph;
            bool mph = unit == SpeedUnit.Mph;

            // Round to nearest 5 MPH or nearest 10 km/h
            ushort index = mph ? spd.ToMphRounded(SpeedLimitsTool.MPH_STEP).Mph
                               : spd.ToKmphRounded(SpeedLimitsTool.KMPH_STEP).Kmph;

            // Trim the index since 140 km/h / 90 MPH is the max sign we have
            ushort upper = mph ? SpeedLimitsTool.UPPER_MPH
                               : SpeedLimitsTool.UPPER_KMPH;

            // Show unlimited if the speed cannot be represented by the available sign textures
            if (index == 0 || index > upper) {
                // Log._Debug($"Trimming speed={speedLimit} index={index} to {upper}");
                return textureSource[0];
            }

            // Trim from below to not go below index 5 (5 kmph or 5 mph)
            ushort trimIndex = Math.Max((ushort)5, index);
            return textureSource[trimIndex];
        }

        // /// <summary>
        // /// Given speed limit, round it up to nearest Kmph or Mph and produce a texture
        // /// </summary>
        // /// <param name="spd">Ingame speed</param>
        // /// <returns>The texture, hopefully it existed</returns>
        // public static Texture2D GetSpeedLimitTexture(SpeedValue spd,
        //                                              IDictionary<int, Texture2D> textureSource) {
        //     return GetSpeedLimitTexture(spd, textureSource);
        // }

        /// <summary>For current display settings get texture dictionary with the road signs.</summary>
        public static IDictionary<int, Texture2D> GetTextureSource() {
            var m = GlobalConfig.Instance.Main;
            var unit = m.DisplaySpeedLimitsMph ? SpeedUnit.Mph : SpeedUnit.Kmph;

            // Select the source for the textures based on unit and the theme
            bool mph = unit == SpeedUnit.Mph;

            if (mph) {
                switch (m.MphRoadSignStyle) {
                    case SpeedLimitSignTheme.RectangularUS:
                        return TexturesMphUS;
                    case SpeedLimitSignTheme.RoundUK:
                        return TexturesMphUK;
                    case SpeedLimitSignTheme.RoundGerman:
                        // Do nothing, this is the default above
                        break;
                }
            }
            return TexturesKmph;
        }

        /// <summary>
        /// Returns vector of one for square/circle textures, or a proportionally scaled rect of
        /// width one, for rectangular US signs.
        /// </summary>
        /// <returns>Scalable vector of texture aspect ratio.</returns>
        public static Vector2 GetTextureAspectRatio() {
            var m = GlobalConfig.Instance.Main;
            var unit = m.DisplaySpeedLimitsMph ? SpeedUnit.Mph : SpeedUnit.Kmph;

            // Select the source for the textures based on unit and the theme
            bool mph = unit == SpeedUnit.Mph;

            if (mph) {
                switch (m.MphRoadSignStyle) {
                    case SpeedLimitSignTheme.RectangularUS:
                        return new Vector2(1.0f / 1.25f, 1.0f);
                    case SpeedLimitSignTheme.RoundUK:
                    case SpeedLimitSignTheme.RoundGerman:
                        break;
                }
            }
            return Vector2.one;
        }
    }
}