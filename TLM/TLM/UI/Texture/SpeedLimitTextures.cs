namespace TrafficManager.UI.Texture {
    using System;
    using System.Collections.Generic;
    using CSUtil.Commons;
    using Manager.Impl;
    using State;
    using Traffic.Data;
    using UnityEngine;
    using Util;

    public struct SpeedLimitTextures
    {
        public static readonly IDictionary<int, Texture2D> Kmph;
        public static readonly IDictionary<int, Texture2D> MphUS;
        public static readonly IDictionary<int, Texture2D> MphUK;

        static SpeedLimitTextures() {
            // TODO: Split loading here into dynamic sections, static enforces everything to stay in this ctor
            Kmph = new TinyDictionary<int, Texture2D>();
            MphUS = new TinyDictionary<int, Texture2D>();
            MphUK = new TinyDictionary<int, Texture2D>();

            // Load shared speed limit signs for Kmph and Mph
            // Assumes that signs from 0 to 140 with step 5 exist, 0 denotes no limit sign
            for (var speedLimit = 0; speedLimit <= 140; speedLimit += 5) {
                var resource = TextureResources.LoadDllResource($"SpeedLimits.Kmh.{speedLimit}.png", 200, 200);
                Kmph.Add(speedLimit, resource ? resource : Kmph[5]);
            }

            // Signs from 0 to 90 for MPH
            for (var speedLimit = 0; speedLimit <= 90; speedLimit += 5) {
                // Load US textures, they are rectangular
                var resourceUs = TextureResources.LoadDllResource($"SpeedLimits.Mph_US.{speedLimit}.png", 200, 250);
                MphUS.Add(speedLimit, resourceUs ? resourceUs : MphUS[5]);

                // Load UK textures, they are square
                var resourceUk = TextureResources.LoadDllResource($"SpeedLimits.Mph_UK.{speedLimit}.png", 200, 200);
                MphUK.Add(speedLimit, resourceUk ? resourceUk : MphUK[5]);
            }
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

        /// <summary>
        /// Given the float speed, style and MPH option return a texture to render.
        /// </summary>
        /// <param name="speedLimit">float speed</param>
        /// <param name="mphStyle">Signs theme</param>
        /// <param name="unit">Mph or km/h</param>
        /// <returns>The texture</returns>
        public static Texture2D GetSpeedLimitTexture(float speedLimit, MphSignStyle mphStyle, SpeedUnit unit) {
            // Select the source for the textures based on unit and the theme
            var mph = unit == SpeedUnit.Mph;
            var textures = SpeedLimitTextures.Kmph;
            if (mph) {
                switch (mphStyle) {
                    case MphSignStyle.SquareUS:
                        textures = SpeedLimitTextures.MphUS;
                        break;
                    case MphSignStyle.RoundUK:
                        textures = SpeedLimitTextures.MphUK;
                        break;
                    case MphSignStyle.RoundGerman:
                        // Do nothing, this is the default above
                        break;
                }
            }

            // Trim the range
            if (speedLimit > SpeedLimitManager.MAX_SPEED * 0.95f) {
                return textures[0];
            }

            // Round to nearest 5 MPH or nearest 10 km/h
            var index = mph ? SpeedLimit.ToMphRounded(speedLimit) : SpeedLimit.ToKmphRounded(speedLimit);

            // Trim the index since 140 km/h / 90 MPH is the max sign we have
            var upper = mph ? SpeedLimit.UPPER_MPH : SpeedLimit.UPPER_KMPH;
#if DEBUG
            if (index > upper) {
                Log._Debug($"Trimming speed={speedLimit} index={index} to {upper}");
            }
#endif

            var trimIndex = Math.Min(upper, Math.Max((ushort) 0, index));
            return textures[trimIndex];
        }
    }
}