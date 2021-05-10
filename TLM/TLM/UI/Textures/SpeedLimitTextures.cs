namespace TrafficManager.UI.Textures {
    using static TextureResources;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Lifecycle;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;

    public class SpeedLimitTextures {
        public const ushort UPPER_KMPH = 140;

        /// <summary>
        /// We have texture files for every 5 kmph but speed limits palette allows every 10. This is
        /// for more precise MPH display.
        /// </summary>
        public const ushort KMPH_STEP = 10;
        public const ushort LOAD_KMPH_STEP = 5;

        public const ushort UPPER_MPH = 90;
        public const ushort MPH_STEP = 5;

        /// <summary>Blue textures for road/lane default speed limits.</summary>
        public readonly IDictionary<int, Texture2D> RoadDefaults;

        /// <summary>German style textures for KM/hour also usable for MPH.</summary>
        public readonly IDictionary<int, Texture2D> TexturesKmph;

        /// <summary>White rectangular textures for MPH US style.</summary>
        public readonly IDictionary<int, Texture2D> TexturesMphUS;

        /// <summary>British style speed limit textures for MPH</summary>
        public readonly IDictionary<int, Texture2D> TexturesMphUK;

        public readonly Texture2D Clear;

        /// <summary>
        ///     List available km/h speed limit textures and road default speed limit textures.
        ///     Also this is a selection of speed limit buttons.
        /// </summary>
        public readonly List<int> KmphList;

        /// <summary>List available MPH textures and is a selection of speed limit buttons.</summary>
        public readonly List<int> MphList;

        public SpeedLimitTextures() {
            RoadDefaults = new TinyDictionary<int, Texture2D>();
            // TODO: Split loading here into dynamic sections, static enforces everything to stay in this ctor
            RoadDefaults = new TinyDictionary<int, Texture2D>();
            TexturesKmph = new TinyDictionary<int, Texture2D>();
            TexturesMphUS = new TinyDictionary<int, Texture2D>();
            TexturesMphUK = new TinyDictionary<int, Texture2D>();

            IntVector2 sizeSquare = new IntVector2(200);
            IntVector2 sizeRectangular = new IntVector2(200, 250);

            KmphList = new List<int>();
            MphList = new List<int>();
            for (var kmph = LOAD_KMPH_STEP; kmph <= UPPER_KMPH; kmph += LOAD_KMPH_STEP) {
                KmphList.Add(kmph);
            }

            for (var mph = MPH_STEP; mph <= UPPER_MPH; mph += MPH_STEP) {
                MphList.Add(mph);
            }

            // Load shared speed limit signs for Kmph and Mph
            // Assumes that signs from 0 to 140 with step 5 exist, 0 denotes no limit sign
            void LoadKmphTexture(int kmph1) {
                Texture2D resource = LoadDllResource(
                    resourceName: $"SpeedLimits.Kmh.{kmph1}.png",
                    size: sizeSquare,
                    mip: true);
                TexturesKmph.Add(kmph1, resource ? resource : TexturesKmph[5]);

                resource = LoadDllResource(
                    resourceName: $"SpeedLimits.RoadDefaults.{kmph1}.png",
                    size: sizeSquare,
                    mip: true);
                RoadDefaults.Add(kmph1, resource ? resource : RoadDefaults[5]);
            }

            LoadKmphTexture(0);
            foreach (var kmph in KmphList) {
                LoadKmphTexture(kmph);
            }

            // Signs from 0 to 90 for MPH
            void LoadMphTexture(int mph1) {
                // Load US textures, they are rectangular
                Texture2D resourceUs = LoadDllResource(
                    resourceName: $"SpeedLimits.Mph_US.{mph1}.png",
                    size: sizeRectangular,
                    mip: true);
                TexturesMphUS.Add(mph1, resourceUs ? resourceUs : TexturesMphUS[5]);

                // Load UK textures, they are square
                Texture2D resourceUk = LoadDllResource(
                    resourceName: $"SpeedLimits.Mph_UK.{mph1}.png",
                    size: sizeSquare,
                    mip: true);
                TexturesMphUK.Add(mph1, resourceUk ? resourceUk : TexturesMphUK[5]);
            }

            LoadMphTexture(0);
            foreach (var mph in MphList) {
                LoadMphTexture(mph);
            }

            Clear = LoadDllResource(resourceName: "clear.png", size: new IntVector2(256));
        }

        /// <summary>
        /// Given the float speed, style and MPH option return a texture to render.
        /// </summary>
        /// <param name="spd">Speed to display.</param>
        /// <returns>Texture to display.</returns>
        public Texture2D GetSpeedLimitTexture(SpeedValue spd,
                                              IDictionary<int, Texture2D> textureSource) {
            // Select the source for the textures based on unit and the theme
            Main m = GlobalConfig.Instance.Main;
            SpeedUnit unit = m.DisplaySpeedLimitsMph ? SpeedUnit.Mph : SpeedUnit.Kmph;
            bool mph = unit == SpeedUnit.Mph;

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
            var m = GlobalConfig.Instance.Main;
            var unit = m.DisplaySpeedLimitsMph ? SpeedUnit.Mph : SpeedUnit.Kmph;

            // Select the source for the textures based on unit and the theme
            bool mph = unit == SpeedUnit.Mph;
            SpeedLimitTextures self = TMPELifecycle.Instance.Textures.SpeedLimits;

            if (mph) {
                switch (m.MphRoadSignStyle) {
                    case MphSignStyle.SquareUS:
                        return self.TexturesMphUS;
                    case MphSignStyle.RoundUK:
                        return self.TexturesMphUK;
                    case MphSignStyle.RoundGerman:
                        // Do nothing, this is the default above
                        break;
                }
            }

            return self.TexturesKmph;
        }

        /// <summary>
        /// Returns vector of one for square/circle textures, or a proportionally scaled rect of
        /// width one, for rectangular US signs.
        /// </summary>
        /// <returns>Scalable vector of texture aspect ratio.</returns>
        public static Vector2 GetTextureAspectRatio() {
            Main m = GlobalConfig.Instance.Main;
            SpeedUnit unit = m.DisplaySpeedLimitsMph ? SpeedUnit.Mph : SpeedUnit.Kmph;

            // Select the source for the textures based on unit and the theme
            bool mph = unit == SpeedUnit.Mph;

            if (mph) {
                switch (m.MphRoadSignStyle) {
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