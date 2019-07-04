namespace TrafficManager.UI.Texture {
    using System.Collections.Generic;
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
    }
}