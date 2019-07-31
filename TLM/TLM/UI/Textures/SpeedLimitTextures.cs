namespace TrafficManager.UI.Textures {
    using System;
    using System.Collections.Generic;
    using State;
    using SubTools.SpeedLimits;
    using UnityEngine;
    using Util;
    using static TextureResources;

    public static class SpeedLimitTextures {
        public static readonly IDictionary<int, Texture2D> TexturesKmph;
        public static readonly IDictionary<int, Texture2D> TexturesMphUS;
        public static readonly IDictionary<int, Texture2D> TexturesMphUK;

        static SpeedLimitTextures() {
            // TODO: Split loading here into dynamic sections, static enforces everything to stay in this ctor
            TexturesKmph = new TinyDictionary<int, Texture2D>();
            TexturesMphUS = new TinyDictionary<int, Texture2D>();
            TexturesMphUK = new TinyDictionary<int, Texture2D>();

            // Load shared speed limit signs for Kmph and Mph
            // Assumes that signs from 0 to 140 with step 5 exist, 0 denotes no limit sign
            for (var speedLimit = 0; speedLimit <= 140; speedLimit += 5) {
                var resource = LoadDllResource($"SpeedLimits.Kmh.{speedLimit}.png", 200, 200);
                TexturesKmph.Add(speedLimit, resource ?? TexturesKmph[5]);
            }
            // Signs from 0 to 90 for MPH
            for (var speedLimit = 0; speedLimit <= 90; speedLimit += 5) {
                // Load US textures, they are rectangular
                var resourceUs = LoadDllResource($"SpeedLimits.Mph_US.{speedLimit}.png", 200, 250);
                TexturesMphUS.Add(speedLimit, resourceUs ?? TexturesMphUS[5]);
                // Load UK textures, they are square
                var resourceUk = LoadDllResource($"SpeedLimits.Mph_UK.{speedLimit}.png", 200, 200);
                TexturesMphUK.Add(speedLimit, resourceUk ?? TexturesMphUK[5]);
            }
        }

        /// <summary>
        /// Given the float speed, style and MPH option return a texture to render.
        /// </summary>
        /// <param name="speedLimit">float speed</param>
        /// <param name="mphStyle">Signs theme</param>
        /// <param name="unit">Mph or km/h</param>
        /// <returns></returns>
        public static Texture2D GetSpeedLimitTexture(float speedLimit, MphSignStyle mphStyle, SpeedUnit unit) {
            // Select the source for the textures based on unit and the theme
            bool mph = unit == SpeedUnit.Mph;
            IDictionary<int, Texture2D> textures = TexturesKmph;
            if (mph) {
                switch (mphStyle) {
                    case MphSignStyle.SquareUS:
                        textures = TexturesMphUS;
                        break;
                    case MphSignStyle.RoundUK:
                        textures = TexturesMphUK;
                        break;
                    case MphSignStyle.RoundGerman:
                        // Do nothing, this is the default above
                        break;
                }
            }

            // Round to nearest 5 MPH or nearest 10 km/h
            ushort index = mph ? SpeedLimit.ToMphRounded(speedLimit) : SpeedLimit.ToKmphRounded(speedLimit);

            // Trim the index since 140 km/h / 90 MPH is the max sign we have
            ushort upper = mph ? SpeedLimit.UPPER_MPH : SpeedLimit.UPPER_KMPH;

            // Show unlimited if the speed cannot be represented by the available sign textures
            if (index == 0 || index > upper) {
                // Log._Debug($"Trimming speed={speedLimit} index={index} to {upper}");
                return textures[0];
            }

            // Trim from below to not go below index 5 (5 kmph or 5 mph)
            ushort trimIndex = Math.Max((ushort)5, index);
            return textures[trimIndex];
        }

        /// <summary>
        /// Given speed limit, round it up to nearest Kmph or Mph and produce a texture
        /// </summary>
        /// <param name="speedLimit">Ingame speed</param>
        /// <returns>The texture, hopefully it existed</returns>
        public static Texture2D GetSpeedLimitTexture(float speedLimit) {
            var m = GlobalConfig.Instance.Main;
            var unit = m.DisplaySpeedLimitsMph ? SpeedUnit.Mph : SpeedUnit.Kmph;
            return GetSpeedLimitTexture(speedLimit, m.MphRoadSignStyle, unit);
        }
    }
}