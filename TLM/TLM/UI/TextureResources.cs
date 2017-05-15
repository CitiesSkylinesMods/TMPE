using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using TrafficManager.Traffic;
using TrafficManager.UI;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.PrioritySegment;

namespace TrafficManager.UI
{
    public class TextureResources
    {
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
		public static readonly IDictionary<ushort, Texture2D> SpeedLimitTextures;
		public static readonly IDictionary<ExtVehicleType, IDictionary<bool, Texture2D>> VehicleRestrictionTextures;
		public static readonly IDictionary<ExtVehicleType, Texture2D> VehicleInfoSignTextures;
		public static readonly IDictionary<bool, Texture2D> ParkingRestrictionTextures;
		public static readonly Texture2D LaneChangeForbiddenTexture2D;
		public static readonly Texture2D LaneChangeAllowedTexture2D;
		public static readonly Texture2D UturnAllowedTexture2D;
		public static readonly Texture2D UturnForbiddenTexture2D;
		public static readonly Texture2D EnterBlockedJunctionAllowedTexture2D;
		public static readonly Texture2D EnterBlockedJunctionForbiddenTexture2D;
		public static readonly Texture2D PedestrianCrossingAllowedTexture2D;
		public static readonly Texture2D PedestrianCrossingForbiddenTexture2D;
		public static readonly Texture2D MainMenuButtonTexture2D;
		public static readonly Texture2D MainMenuButtonsTexture2D;
		public static readonly Texture2D NoImageTexture2D;

		static TextureResources()
        {
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
            LightModeTexture2D = LoadDllResource(Translation.GetTranslatedFileName("light_mode.png"), 103, 95);
            LightCounterTexture2D = LoadDllResource(Translation.GetTranslatedFileName("light_counter.png"), 103, 95);
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

			SpeedLimitTextures = new TinyDictionary<ushort, Texture2D>();
			foreach (ushort speedLimit in SpeedLimitManager.Instance.AvailableSpeedLimits) {
				SpeedLimitTextures.Add(speedLimit, LoadDllResource(speedLimit.ToString() + ".png", 200, 200));
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

			foreach (KeyValuePair<ExtVehicleType, IDictionary<bool, Texture2D>> e in VehicleRestrictionTextures) {
				foreach (bool b in new bool[]{false, true}) {
					string suffix = b ? "allowed" : "forbidden";
					e.Value[b] = LoadDllResource(e.Key.ToString().ToLower() + "_" + suffix + ".png", 200, 200);
				}
			}

			ParkingRestrictionTextures = new TinyDictionary<bool, Texture2D>();
			ParkingRestrictionTextures[true] = LoadDllResource("parking_allowed.png", 200, 200);
			ParkingRestrictionTextures[false] = LoadDllResource("parking_disallowed.png", 200, 200);

			LaneChangeAllowedTexture2D = LoadDllResource("lanechange_allowed.png", 200, 200);
			LaneChangeForbiddenTexture2D = LoadDllResource("lanechange_forbidden.png", 200, 200);

			UturnAllowedTexture2D = LoadDllResource("uturn_allowed.png", 200, 200);
			UturnForbiddenTexture2D = LoadDllResource("uturn_forbidden.png", 200, 200);

			EnterBlockedJunctionAllowedTexture2D = LoadDllResource("enterblocked_allowed.png", 200, 200);
			EnterBlockedJunctionForbiddenTexture2D = LoadDllResource("enterblocked_forbidden.png", 200, 200);

			PedestrianCrossingAllowedTexture2D = LoadDllResource("crossing_allowed.png", 200, 200);
			PedestrianCrossingForbiddenTexture2D = LoadDllResource("crossing_forbidden.png", 200, 200);

			VehicleInfoSignTextures = new TinyDictionary<ExtVehicleType, Texture2D>();
			VehicleInfoSignTextures[ExtVehicleType.Bicycle] = LoadDllResource("bicycle_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.Bus] = LoadDllResource("bus_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.CargoTrain] = LoadDllResource("cargotrain_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.CargoTruck] = LoadDllResource("cargotruck_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.Emergency] = LoadDllResource("emergency_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.PassengerCar] = LoadDllResource("passengercar_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.PassengerTrain] = LoadDllResource("passengertrain_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.RailVehicle] = VehicleInfoSignTextures[ExtVehicleType.PassengerTrain];
			VehicleInfoSignTextures[ExtVehicleType.Service] = LoadDllResource("service_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.Taxi] = LoadDllResource("taxi_infosign.png", 449, 411);
			VehicleInfoSignTextures[ExtVehicleType.Tram] = LoadDllResource("tram_infosign.png", 449, 411);
		}

        private static Texture2D LoadDllResource(string resourceName, int width, int height)
        {
            var myAssembly = Assembly.GetExecutingAssembly();
            var myStream = myAssembly.GetManifestResourceStream("TrafficManager.Resources." + resourceName);

            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);

            texture.LoadImage(ReadToEnd(myStream));

            return texture;
        }

        static byte[] ReadToEnd(Stream stream)
        {
            var originalPosition = stream.Position;
            stream.Position = 0;

            try
            {
                var readBuffer = new byte[4096];

                var totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead != readBuffer.Length)
                        continue;

                    var nextByte = stream.ReadByte();
                    if (nextByte == -1)
                        continue;

                    var temp = new byte[readBuffer.Length * 2];
                    Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                    Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
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
            finally
            {
                stream.Position = originalPosition;
            }
        }
	}
}