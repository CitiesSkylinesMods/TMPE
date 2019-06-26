using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.UI;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.Data.PrioritySegment;

namespace TrafficManager.UI {
    public class TextureResources {
        public static readonly Texture2D RedLightTexture2D;
        public static readonly Texture2D YellowRedLightTexture2D;
        public static readonly Texture2D YellowLightTexture2D;
        public static readonly Texture2D GreenLightTexture2D;
        public static readonly Texture2D RedLightStraightTexture2D;
        public static readonly Texture2D YellowLightStraightTexture2D;
        public static readonly Texture2D GreenLightStraightTexture2D;
        public static readonly Texture2D RedLightRightTexture2D;
        public static readonly Texture2D YellowLightRightTexture2D;
        public static readonly Texture2D GreenLightRightTexture2D;
        public static readonly Texture2D RedLightLeftTexture2D;
        public static readonly Texture2D YellowLightLeftTexture2D;
        public static readonly Texture2D GreenLightLeftTexture2D;
        public static readonly Texture2D RedLightForwardRightTexture2D;
        public static readonly Texture2D YellowLightForwardRightTexture2D;
        public static readonly Texture2D GreenLightForwardRightTexture2D;
        public static readonly Texture2D RedLightForwardLeftTexture2D;
        public static readonly Texture2D YellowLightForwardLeftTexture2D;
        public static readonly Texture2D GreenLightForwardLeftTexture2D;
        public static readonly Texture2D PedestrianRedLightTexture2D;
        public static readonly Texture2D PedestrianGreenLightTexture2D;
        public static readonly Texture2D LightModeTexture2D;
        public static readonly Texture2D LightCounterTexture2D;
        public static readonly Texture2D PedestrianModeAutomaticTexture2D;
        public static readonly Texture2D PedestrianModeManualTexture2D;
        public static readonly IDictionary<PriorityType, Texture2D> PrioritySignTextures;
        public static readonly Texture2D SignRemoveTexture2D;
        public static readonly Texture2D ClockPlayTexture2D;
        public static readonly Texture2D ClockPauseTexture2D;
        public static readonly Texture2D ClockTestTexture2D;
        public static readonly IDictionary<int, Texture2D> SpeedLimitTexturesKmph;
        public static readonly IDictionary<int, Texture2D> SpeedLimitTexturesMphUS;
        public static readonly IDictionary<int, Texture2D> SpeedLimitTexturesMphUK;
        public static readonly IDictionary<ExtVehicleType, IDictionary<bool, Texture2D>> VehicleRestrictionTextures;
        public static readonly IDictionary<ExtVehicleType, Texture2D> VehicleInfoSignTextures;
        public static readonly IDictionary<bool, Texture2D> ParkingRestrictionTextures;
        public static readonly Texture2D LaneChangeForbiddenTexture2D;
        public static readonly Texture2D LaneChangeAllowedTexture2D;
        public static readonly Texture2D UturnAllowedTexture2D;
        public static readonly Texture2D UturnForbiddenTexture2D;
        public static readonly Texture2D RightOnRedForbiddenTexture2D;
        public static readonly Texture2D RightOnRedAllowedTexture2D;
        public static readonly Texture2D LeftOnRedForbiddenTexture2D;
        public static readonly Texture2D LeftOnRedAllowedTexture2D;
        public static readonly Texture2D EnterBlockedJunctionAllowedTexture2D;
        public static readonly Texture2D EnterBlockedJunctionForbiddenTexture2D;
        public static readonly Texture2D PedestrianCrossingAllowedTexture2D;
        public static readonly Texture2D PedestrianCrossingForbiddenTexture2D;
        public static readonly Texture2D MainMenuButtonTexture2D;
        public static readonly Texture2D MainMenuButtonsTexture2D;
        public static readonly Texture2D NoImageTexture2D;
        public static readonly Texture2D RemoveButtonTexture2D;
        public static readonly Texture2D WindowBackgroundTexture2D;

        /// <summary>
        /// Groups resources for Lane Arrows Tool, 32x32
        /// Row 0 (64px) contains: red X, green ↑, →, ↑→, ←, ←↑, ←→, ←↑→
        /// Row 1 (32px) contains: Blue ←, ↑, →, disabled ←, ↑, →
        /// Row 2 (0px) contains: black ←, ↑, → 
        /// </summary>
        public struct LaneArrows {
            public static readonly Texture2D Atlas;
            private const int ATLAS_H = 96;
            private const float SPRITE_H = 32f;
            private const float SPRITE_W = 32f;

            static LaneArrows() {
                Atlas = LoadDllResource("LaneArrows.Atlas_Lane_Arrows.png", 256, ATLAS_H);
            }

            /// <summary>
            /// Returns sprite, where rect has Y inverted, because Unity has Y axis going up...
            /// </summary>
            /// <param name="x">Horizontal sprite number</param>
            /// <param name="y">Vertical sprite number, 0 = top row</param>
            /// <returns>Rect</returns>
            static Sprite GetSprite(int x, int y) {
                var rc = new Rect(x * 32f, ATLAS_H - 32f - (y * 32f),
                                SPRITE_H, SPRITE_W);
                return Sprite.Create(Atlas, rc, Vector2.zero);
            }

            /// <summary>
            /// The first row of the atlas contains arrow signs, where Left, Forward and Right form bit-combinations
            /// </summary>
            /// <param name="flags">Actual lane flags to display</param>
            /// <returns>A sprite</returns>
            public static Sprite GetLaneControlSprite(NetLane.Flags flags) {
                var forward = (flags & NetLane.Flags.Forward) != 0 ? 1 : 0;
                var right = (flags & NetLane.Flags.Right) != 0 ? 2 : 0;
                var left = (flags & NetLane.Flags.Left) != 0 ? 4 : 0;
                var spriteIndex = forward | left | right;
                return GetSprite(spriteIndex, 0);
            }

            /// <summary>
            /// For lane direction and possibly disabled lane, return a sprite
            /// </summary>
            /// <param name="dir">Direction</param>
            /// <param name="disabled">Whether the sprite should be gray and crossed out</param>
            /// <returns>The sprite</returns>
            public static Sprite GetLaneArrowSprite(ArrowDirection dir, bool on, bool disabled) {
                var x = 0;
                var y = 0; // 0,0 is red x default fallback sprite

                switch (dir) {
                    case ArrowDirection.Left:
                        x = 1; y = 1;
                        break;
                    case ArrowDirection.Forward:
                        x = 0; y = 1;
                        break;
                    case ArrowDirection.Right:
                        x = 2; y = 1;
                        break;
                    case ArrowDirection.None:
                    case ArrowDirection.Turn:
                        break;
                }

                if (!on) {
                    // off sprites are on row 2 (64px)
                    y++;
                }
                if (disabled) {
                    x += 3; y = 1; 
                }
                return GetSprite(x, y);
            }
        }

        static TextureResources() {
            // missing image
            NoImageTexture2D = LoadDllResource("noimage.png", 64, 64);

            // main menu icon
            MainMenuButtonTexture2D = LoadDllResource("MenuButton.png", 300, 50);
            MainMenuButtonTexture2D.name = "TMPE_MainMenuButtonIcon";

            // main menu buttons
            MainMenuButtonsTexture2D = LoadDllResource("mainmenu-btns.png", 960, 30);
            MainMenuButtonsTexture2D.name = "TMPE_MainMenuButtons";

            // simple
            RedLightTexture2D = LoadDllResource("light_1_1.png", 103, 243);
            YellowRedLightTexture2D = LoadDllResource("light_1_2.png", 103, 243);
            GreenLightTexture2D = LoadDllResource("light_1_3.png", 103, 243);
            // forward
            RedLightStraightTexture2D = LoadDllResource("light_2_1.png", 103, 243);
            YellowLightStraightTexture2D = LoadDllResource("light_2_2.png", 103, 243);
            GreenLightStraightTexture2D = LoadDllResource("light_2_3.png", 103, 243);
            // right
            RedLightRightTexture2D = LoadDllResource("light_3_1.png", 103, 243);
            YellowLightRightTexture2D = LoadDllResource("light_3_2.png", 103, 243);
            GreenLightRightTexture2D = LoadDllResource("light_3_3.png", 103, 243);
            // left
            RedLightLeftTexture2D = LoadDllResource("light_4_1.png", 103, 243);
            YellowLightLeftTexture2D = LoadDllResource("light_4_2.png", 103, 243);
            GreenLightLeftTexture2D = LoadDllResource("light_4_3.png", 103, 243);
            // forwardright
            RedLightForwardRightTexture2D = LoadDllResource("light_5_1.png", 103, 243);
            YellowLightForwardRightTexture2D = LoadDllResource("light_5_2.png", 103, 243);
            GreenLightForwardRightTexture2D = LoadDllResource("light_5_3.png", 103, 243);
            // forwardleft
            RedLightForwardLeftTexture2D = LoadDllResource("light_6_1.png", 103, 243);
            YellowLightForwardLeftTexture2D = LoadDllResource("light_6_2.png", 103, 243);
            GreenLightForwardLeftTexture2D = LoadDllResource("light_6_3.png", 103, 243);
            // yellow
            YellowLightTexture2D = LoadDllResource("light_yellow.png", 103, 243);
            // pedestrian
            PedestrianRedLightTexture2D = LoadDllResource("pedestrian_light_1.png", 73, 123);
            PedestrianGreenLightTexture2D = LoadDllResource("pedestrian_light_2.png", 73, 123);
            // light mode
            LightModeTexture2D =
                LoadDllResource(Translation.GetTranslatedFileName("light_mode.png"), 103, 95);
            LightCounterTexture2D =
                LoadDllResource(Translation.GetTranslatedFileName("light_counter.png"), 103, 95);
            // pedestrian mode
            PedestrianModeAutomaticTexture2D = LoadDllResource("pedestrian_mode_1.png", 73, 70);
            PedestrianModeManualTexture2D = LoadDllResource("pedestrian_mode_2.png", 73, 73);

            // priority signs
            PrioritySignTextures = new TinyDictionary<PriorityType, Texture2D>();
            PrioritySignTextures[PriorityType.None] = LoadDllResource("sign_none.png", 200, 200);
            PrioritySignTextures[PriorityType.Main] = LoadDllResource("sign_priority.png", 200, 200);
            PrioritySignTextures[PriorityType.Stop] = LoadDllResource("sign_stop.png", 200, 200);
            PrioritySignTextures[PriorityType.Yield] = LoadDllResource("sign_yield.png", 200, 200);

            // delete priority sign
            SignRemoveTexture2D = LoadDllResource("remove_signs.png", 256, 256);

            // timer
            ClockPlayTexture2D = LoadDllResource("clock_play.png", 512, 512);
            ClockPauseTexture2D = LoadDllResource("clock_pause.png", 512, 512);
            ClockTestTexture2D = LoadDllResource("clock_test.png", 512, 512);

            // TODO: Split loading here into dynamic sections, static enforces everything to stay in this ctor
            SpeedLimitTexturesKmph = new TinyDictionary<int, Texture2D>();
            SpeedLimitTexturesMphUS = new TinyDictionary<int, Texture2D>();
            SpeedLimitTexturesMphUK = new TinyDictionary<int, Texture2D>();

            // Load shared speed limit signs for Kmph and Mph
            // Assumes that signs from 0 to 140 with step 5 exist, 0 denotes no limit sign
            for (var speedLimit = 0; speedLimit <= 140; speedLimit += 5) {
                var resource = LoadDllResource($"SpeedLimits.Kmh.{speedLimit}.png", 200, 200);
                SpeedLimitTexturesKmph.Add(speedLimit, resource ?? SpeedLimitTexturesKmph[5]);
            }

            // Signs from 0 to 90 for MPH
            for (var speedLimit = 0; speedLimit <= 90; speedLimit += 5) {
                // Load US textures, they are rectangular
                var resourceUs = LoadDllResource($"SpeedLimits.Mph_US.{speedLimit}.png", 200, 250);
                SpeedLimitTexturesMphUS.Add(speedLimit, resourceUs ?? SpeedLimitTexturesMphUS[5]);
                // Load UK textures, they are square
                var resourceUk = LoadDllResource($"SpeedLimits.Mph_UK.{speedLimit}.png", 200, 200);
                SpeedLimitTexturesMphUK.Add(speedLimit, resourceUk ?? SpeedLimitTexturesMphUK[5]);
            }

            VehicleRestrictionTextures = new TinyDictionary<ExtVehicleType, IDictionary<bool, Texture2D>>();
            VehicleRestrictionTextures[ExtVehicleType.Bus] = new TinyDictionary<bool, Texture2D>();
            VehicleRestrictionTextures[ExtVehicleType.CargoTrain] = new TinyDictionary<bool, Texture2D>();
            VehicleRestrictionTextures[ExtVehicleType.CargoTruck] = new TinyDictionary<bool, Texture2D>();
            VehicleRestrictionTextures[ExtVehicleType.Emergency] = new TinyDictionary<bool, Texture2D>();
            VehicleRestrictionTextures[ExtVehicleType.PassengerCar] = new TinyDictionary<bool, Texture2D>();
            VehicleRestrictionTextures[ExtVehicleType.PassengerTrain] = new TinyDictionary<bool, Texture2D>();
            VehicleRestrictionTextures[ExtVehicleType.Service] = new TinyDictionary<bool, Texture2D>();
            VehicleRestrictionTextures[ExtVehicleType.Taxi] = new TinyDictionary<bool, Texture2D>();

            foreach (KeyValuePair<ExtVehicleType, IDictionary<bool, Texture2D>> e in
                VehicleRestrictionTextures) {
                foreach (bool b in new bool[] {false, true}) {
                    string suffix = b ? "allowed" : "forbidden";
                    e.Value[b] = LoadDllResource(
                        $"{e.Key.ToString().ToLower()}_{suffix}.png",
                        200, 200);
                }
            }

            ParkingRestrictionTextures = new TinyDictionary<bool, Texture2D>();
            ParkingRestrictionTextures[true] = LoadDllResource("parking_allowed.png", 200, 200);
            ParkingRestrictionTextures[false] = LoadDllResource("parking_disallowed.png", 200, 200);

            LaneChangeAllowedTexture2D = LoadDllResource("lanechange_allowed.png", 200, 200);
            LaneChangeForbiddenTexture2D = LoadDllResource("lanechange_forbidden.png", 200, 200);

            UturnAllowedTexture2D = LoadDllResource("uturn_allowed.png", 200, 200);
            UturnForbiddenTexture2D = LoadDllResource("uturn_forbidden.png", 200, 200);

            RightOnRedAllowedTexture2D = LoadDllResource("right_on_red_allowed.png", 200, 200);
            RightOnRedForbiddenTexture2D = LoadDllResource("right_on_red_forbidden.png", 200, 200);
            LeftOnRedAllowedTexture2D = LoadDllResource("left_on_red_allowed.png", 200, 200);
            LeftOnRedForbiddenTexture2D = LoadDllResource("left_on_red_forbidden.png", 200, 200);

            EnterBlockedJunctionAllowedTexture2D = LoadDllResource("enterblocked_allowed.png", 200, 200);
            EnterBlockedJunctionForbiddenTexture2D =
                LoadDllResource("enterblocked_forbidden.png", 200, 200);

            PedestrianCrossingAllowedTexture2D = LoadDllResource("crossing_allowed.png", 200, 200);
            PedestrianCrossingForbiddenTexture2D = LoadDllResource("crossing_forbidden.png", 200, 200);

            VehicleInfoSignTextures = new TinyDictionary<ExtVehicleType, Texture2D>();
            VehicleInfoSignTextures[ExtVehicleType.Bicycle] =
                LoadDllResource("bicycle_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.Bus] = LoadDllResource("bus_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.CargoTrain] =
                LoadDllResource("cargotrain_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.CargoTruck] =
                LoadDllResource("cargotruck_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.Emergency] =
                LoadDllResource("emergency_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.PassengerCar] =
                LoadDllResource("passengercar_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.PassengerTrain] =
                LoadDllResource("passengertrain_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.RailVehicle] =
                VehicleInfoSignTextures[ExtVehicleType.PassengerTrain];
            VehicleInfoSignTextures[ExtVehicleType.Service] =
                LoadDllResource("service_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.Taxi] = LoadDllResource("taxi_infosign.png", 449, 411);
            VehicleInfoSignTextures[ExtVehicleType.Tram] = LoadDllResource("tram_infosign.png", 449, 411);

            RemoveButtonTexture2D = LoadDllResource("remove-btn.png", 150, 30);

            WindowBackgroundTexture2D = LoadDllResource("WindowBackground.png", 16, 60);
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
        /// <returns></returns>
        public static Texture2D GetSpeedLimitTexture(float speedLimit, MphSignStyle mphStyle, SpeedUnit unit) {
            // Select the source for the textures based on unit and the theme
            var mph = unit == SpeedUnit.Mph;
            var textures = SpeedLimitTexturesKmph;
            if (mph) {
                switch (mphStyle) {
                    case MphSignStyle.SquareUS:
                        textures = SpeedLimitTexturesMphUS;
                        break;
                    case MphSignStyle.RoundUK:
                        textures = SpeedLimitTexturesMphUK;
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
            if (index > upper) {
                Log.Info($"Trimming speed={speedLimit} index={index} to {upper}");
            }

            var trimIndex = Math.Min(upper, Math.Max((ushort) 0, index));
            return textures[trimIndex];
        }

        private static Texture2D LoadDllResource(string resourceName, int width, int height) {
#if DEBUG
            bool debug = State.GlobalConfig.Instance.Debug.Switches[11];
#endif
            try {
#if DEBUG
                if (debug) Log._Debug($"Loading DllResource {resourceName}");
#endif
                var myAssembly = Assembly.GetExecutingAssembly();
                var myStream = myAssembly.GetManifestResourceStream(
                    $"TrafficManager.Resources.{resourceName}");

                var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);

                texture.LoadImage(ReadToEnd(myStream));

                return texture;
            }
            catch (Exception e) {
                Log.Error(e.StackTrace.ToString());
                return null;
            }
        }

        static byte[] ReadToEnd(Stream stream) {
            var originalPosition = stream.Position;
            stream.Position = 0;

            try {
                var readBuffer = new byte[4096];

                var totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead,
                                                readBuffer.Length - totalBytesRead)) > 0) {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead != readBuffer.Length)
                        continue;

                    var nextByte = stream.ReadByte();
                    if (nextByte == -1)
                        continue;

                    var temp = new byte[readBuffer.Length * 2];
                    Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                    Buffer.SetByte(temp, totalBytesRead, (byte) nextByte);
                    readBuffer = temp;
                    totalBytesRead++;
                }

                var buffer = readBuffer;
                if (readBuffer.Length == totalBytesRead)
                    return buffer;

                buffer = new byte[totalBytesRead];
                Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                return buffer;
            }
            catch (Exception e) {
                Log.Error(e.StackTrace.ToString());
                return null;
            }
            finally {
                stream.Position = originalPosition;
            }
        }
    } // class
}