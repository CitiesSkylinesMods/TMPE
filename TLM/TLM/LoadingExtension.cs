using System;
using System.Reflection;
using ColossalFramework;
using ICities;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using TrafficManager.State;
using ColossalFramework.UI;
using ColossalFramework.Math;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Util;
using TrafficManager.Custom.Manager;
using TrafficManager.Manager;

namespace TrafficManager {
	public class LoadingExtension : LoadingExtensionBase {
		public class Detour {
			public MethodInfo OriginalMethod;
			public MethodInfo CustomMethod;
			public RedirectCallsState Redirect;

			public Detour(MethodInfo originalMethod, MethodInfo customMethod) {
				this.OriginalMethod = originalMethod;
				this.CustomMethod = customMethod;
				this.Redirect = RedirectionHelper.RedirectCalls(originalMethod, customMethod);
			}
		}

		//public static LoadingExtension Instance;

		public static bool IsPathManagerReplaced {
			get; private set;
		} = false;

		public static bool IsRainfallLoaded {
			get; private set;
		} = false;

		public static bool IsRushHourLoaded {
			get; private set;
		} = false;

		public static CustomPathManager CustomPathManager { get; set; }
		public static bool DetourInited { get; set; }
		public static List<Detour> Detours { get; set; }
		public static TrafficManagerMode ToolMode { get; set; }
		public static TrafficManagerTool TrafficManagerTool { get; set; }
#if !TAM
		public static UIBase BaseUI { get; private set; }
#endif

		public static UITransportDemand TransportDemandUI { get; private set; }

		public static List<ICustomManager> RegisteredManagers { get; private set; }

		private static bool gameLoaded = false;

		public LoadingExtension() {
		}

		public void revertDetours() {
			if (DetourInited) {
				Log.Info("Revert detours");
				Detours.Reverse();
				foreach (Detour d in Detours) {
					RedirectionHelper.RevertRedirect(d.OriginalMethod, d.Redirect);
				}
				DetourInited = false;
				Detours.Clear();
				Log.Info("Reverting detours finished.");
			}
		}

		public void initDetours() {
			if (!DetourInited) {
				Log.Info("Init detours");
				bool detourFailed = false;

				// REVERSE REDIRECTION

				Log.Info("Reverse-Redirection CustomVehicleManager::ReleaseVehicleImplementation calls");
				try {
					Detours.Add(new Detour(typeof(CustomVehicleManager).GetMethod("ReleaseVehicleImplementation",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
							},
							null),
							typeof(VehicleManager).GetMethod("ReleaseVehicleImplementation",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomVehicleManager::ReleaseVehicleImplementation");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomCitizenManager::ReleaseCitizenInstanceImplementation calls");
				try {
					Detours.Add(new Detour(typeof(CustomCitizenManager).GetMethod("ReleaseCitizenInstanceImplementation",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
							},
							null),
							typeof(CitizenManager).GetMethod("ReleaseCitizenInstanceImplementation",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomCitizenManager::ReleaseCitizenInstanceImplementation");
					detourFailed = true;
				}

				/*Log.Info("Reverse-Redirection CitizenAI::SimulationStep calls");
				try {
					Detours.Add(new Detour(typeof(CustomCitizenAI).GetMethod("SimulationStep",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (CitizenInstance.Frame).MakeByRefType(),
									typeof (bool)
							},
							null),
							typeof(CitizenAI).GetMethod("SimulationStep",
								BindingFlags.Public | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (CitizenInstance.Frame).MakeByRefType(),
									typeof (bool)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CitizenAI::SimulationStep");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CitizenAI::InvalidPath calls");
				try {
					Detours.Add(new Detour(typeof(CustomCitizenAI).GetMethod("InvalidPath",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType()
							},
							null),
							typeof(CitizenAI).GetMethod("InvalidPath",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CitizenAI::InvalidPath");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CitizenAI::FindPathPosition calls");
				try {
					Detours.Add(new Detour(typeof(CustomCitizenAI).GetMethod("FindPathPosition",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Vector3),
									typeof (NetInfo.LaneType),
									typeof (VehicleInfo.VehicleType),
									typeof (bool),
									typeof (PathUnit.Position).MakeByRefType()
							},
							null),
							typeof(CitizenAI).GetMethod("FindPathPosition",
								BindingFlags.Public | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Vector3),
									typeof (NetInfo.LaneType),
									typeof (VehicleInfo.VehicleType),
									typeof (bool),
									typeof (PathUnit.Position).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CitizenAI::FindPathPosition");
					detourFailed = true;
				}*/

				Log.Info("Reverse-Redirection ResidentAI::GetTaxiProbability calls");
				try {
					Detours.Add(new Detour(typeof(CustomResidentAI).GetMethod("GetTaxiProbability",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Citizen.AgeGroup)
							},
							null),
							typeof(ResidentAI).GetMethod("GetTaxiProbability",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Citizen.AgeGroup)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect ResidentAI::GetTaxiProbability");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection ResidentAI::GetBikeProbability calls");
				try {
					Detours.Add(new Detour(typeof(CustomResidentAI).GetMethod("GetBikeProbability",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Citizen.AgeGroup)
							},
							null),
							typeof(ResidentAI).GetMethod("GetBikeProbability",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Citizen.AgeGroup)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect ResidentAI::GetBikeProbability");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection ResidentAI::GetCarProbability calls");
				try {
					Detours.Add(new Detour(typeof(CustomResidentAI).GetMethod("GetCarProbability",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Citizen.AgeGroup)
							},
							null),
							typeof(ResidentAI).GetMethod("GetCarProbability",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Citizen.AgeGroup)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect ResidentAI::GetCarProbability");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection TouristAI::GetTaxiProbability calls");
				try {
					Detours.Add(new Detour(
							typeof(CustomTouristAI).GetMethod("GetTaxiProbability", BindingFlags.NonPublic | BindingFlags.Instance),
							typeof(TouristAI).GetMethod("GetTaxiProbability", BindingFlags.NonPublic | BindingFlags.Instance)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect TouristAI::GetTaxiProbability");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection TouristAI::GetBikeProbability calls");
				try {
					Detours.Add(new Detour(
							typeof(CustomTouristAI).GetMethod("GetBikeProbability", BindingFlags.NonPublic | BindingFlags.Instance),
							typeof(TouristAI).GetMethod("GetBikeProbability", BindingFlags.NonPublic | BindingFlags.Instance)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect TouristAI::GetBikeProbability");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection TouristAI::GetCarProbability calls");
				try {
					Detours.Add(new Detour(
							typeof(CustomTouristAI).GetMethod("GetCarProbability", BindingFlags.NonPublic | BindingFlags.Instance),
							typeof(TouristAI).GetMethod("GetCarProbability", BindingFlags.NonPublic | BindingFlags.Instance)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect TouristAI::GetCarProbability");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection HumanAI::ArriveAtDestination calls");
				try {
					Detours.Add(new Detour(typeof(CustomHumanAI).GetMethod("ArriveAtDestination",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (bool)
							},
							null),
							typeof(HumanAI).GetMethod("ArriveAtDestination",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (bool)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect HumanAI::ArriveAtDestination");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection HumanAI::GetBuildingTargetPosition calls");
				try {
					Detours.Add(new Detour(typeof(CustomHumanAI).GetMethod("GetBuildingTargetPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (float)
							},
							null),
							typeof(HumanAI).GetMethod("GetBuildingTargetPosition",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (float)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect HumanAI::GetBuildingTargetPosition");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection HumanAI::PathfindFailure calls");
				try {
					Detours.Add(new Detour(typeof(CustomHumanAI).GetMethod("PathfindFailure",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType()
							},
							null),
							typeof(HumanAI).GetMethod("PathfindFailure",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect HumanAI::PathfindFailure");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection HumanAI::PathfindSuccess calls");
				try {
					Detours.Add(new Detour(typeof(CustomHumanAI).GetMethod("PathfindSuccess",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType()
							},
							null),
							typeof(HumanAI).GetMethod("PathfindSuccess",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect HumanAI::PathfindSuccess");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection HumanAI::Spawn calls");
				try {
					Detours.Add(new Detour(typeof(CustomHumanAI).GetMethod("Spawn",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType()
							},
							null),
							typeof(HumanAI).GetMethod("Spawn",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect HumanAI::Spawn");
					detourFailed = true;
				}

				/*Log.Info("Reverse-Redirection PassengerCarAI::FindParkingSpaceBuilding calls");
				try {
					Detours.Add(new Detour(typeof(CustomPassengerCarAI).GetMethod("FindParkingSpaceBuilding",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (ushort),
									typeof (ushort),
									typeof (Building).MakeByRefType(),
									typeof (Vector3),
									typeof (float),
									typeof (float),
									typeof (float).MakeByRefType(),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType()
							},
							null),
							typeof(PassengerCarAI).GetMethod("FindParkingSpaceBuilding",
								BindingFlags.NonPublic | BindingFlags.Static,
								null,
								new[]
								{
									typeof (ushort),
									typeof (ushort),
									typeof (ushort),
									typeof (Building).MakeByRefType(),
									typeof (Vector3),
									typeof (float),
									typeof (float),
									typeof (float).MakeByRefType(),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect PassengerCarAI::FindParkingSpaceBuilding");
					detourFailed = true;
				}*/

				Log.Info("Reverse-Redirection PassengerCarAI::FindParkingSpaceRoadSide calls");
				try {
					Detours.Add(new Detour(typeof(CustomPassengerCarAI).GetMethod("FindParkingSpaceRoadSide",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (ushort),
									typeof (Vector3),
									typeof (float),
									typeof (float),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType(),
									typeof (float).MakeByRefType()
							},
							null),
							typeof(PassengerCarAI).GetMethod("FindParkingSpaceRoadSide",
								BindingFlags.NonPublic | BindingFlags.Static,
								null,
								new[]
								{
									typeof (ushort),
									typeof (ushort),
									typeof (Vector3),
									typeof (float),
									typeof (float),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType(),
									typeof (float).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect PassengerCarAI::FindParkingSpaceRoadSide");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection PassengerCarAI::FindParkingSpace calls");
				try {
					Detours.Add(new Detour(typeof(CustomPassengerCarAI).GetMethod("FindParkingSpace",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vector3),
									typeof (Vector3),
									typeof (ushort),
									typeof (float),
									typeof (float),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType(),
									typeof (float).MakeByRefType()
							},
							null),
							typeof(PassengerCarAI).GetMethod("FindParkingSpace",
								BindingFlags.NonPublic | BindingFlags.Static,
								null,
								new[]
								{
									typeof (ushort),
									typeof (Vector3),
									typeof (Vector3),
									typeof (ushort),
									typeof (float),
									typeof (float),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType(),
									typeof (float).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect PassengerCarAI::FindParkingSpace");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection PassengerCarAI::FindParkingSpaceProp calls");
				try {
					Detours.Add(new Detour(typeof(CustomPassengerCarAI).GetMethod("FindParkingSpaceProp",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (PropInfo),
									typeof (Vector3),
									typeof (float),
									typeof (bool),
									typeof (Vector3),
									typeof (float),
									typeof (float),
									typeof (float).MakeByRefType(),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType()
							},
							null),
							typeof(PassengerCarAI).GetMethod("FindParkingSpaceProp",
								BindingFlags.NonPublic | BindingFlags.Static,
								null,
								new[]
								{
									typeof (ushort),
									typeof (PropInfo),
									typeof (Vector3),
									typeof (float),
									typeof (bool),
									typeof (Vector3),
									typeof (float),
									typeof (float),
									typeof (float).MakeByRefType(),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect PassengerCarAI::FindParkingSpaceProp");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CarAI::CheckOverlap calls");
				try {
					Detours.Add(new Detour(typeof(CustomCarAI).GetMethod("CheckOverlap",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (Segment3),
									typeof (ushort),
									typeof (float),
							},
							null),
							typeof(CarAI).GetMethod("CheckOverlap",
								BindingFlags.NonPublic | BindingFlags.Static,
								null,
								new[]
								{
									typeof (Segment3),
									typeof (ushort),
									typeof (float),
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CarAI::CheckOverlap");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection TrainAI::InitializePath calls");
				try {
					Detours.Add(new Detour(typeof(CustomTrainAI).GetMethod("InitializePath",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
							},
							null),
							typeof(TrainAI).GetMethod("InitializePath",
								BindingFlags.NonPublic | BindingFlags.Static,
								null,
								new[]
								{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect TrainAI::InitializePath");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection TramBaseAI::InitializePath calls");
				try {
					Detours.Add(new Detour(typeof(CustomTramBaseAI).GetMethod("InitializePath",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
							},
							null),
							typeof(TramBaseAI).GetMethod("InitializePath",
								BindingFlags.NonPublic | BindingFlags.Static,
								null,
								new[]
								{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect TramBaseAI::InitializePath");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomRoadAI::CheckBuildings calls");
				try {
					Detours.Add(new Detour(typeof(CustomRoadAI).GetMethod("CheckBuildings",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType(),
							},
							null),
							typeof(RoadBaseAI).GetMethod("CheckBuildings",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType(),
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomRoadAI::CheckBuildings");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTrainAI::CheckOverlap calls (1)");
				try {
					Detours.Add(new Detour(typeof(CustomTrainAI).GetMethod("CheckOverlap",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Segment3),
									typeof (ushort)
							},
							null),
							typeof(TrainAI).GetMethod("CheckOverlap",
								BindingFlags.NonPublic | BindingFlags.Static,
								null,
								new[]
								{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Segment3),
									typeof (ushort)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomRoadBaseAI::CheckOverlap (1)");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTrainAI::CheckOverlap calls (2)");
				try {
					Detours.Add(new Detour(typeof(CustomTrainAI).GetMethod("CheckOverlap",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Segment3),
									typeof (ushort),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (bool).MakeByRefType(),
									typeof (Vector3),
									typeof (Vector3)
							},
							null), typeof(TrainAI).GetMethod("CheckOverlap",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Segment3),
									typeof (ushort),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (bool).MakeByRefType(),
									typeof (Vector3),
									typeof (Vector3)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomRoadBaseAI::CheckOverlap (2)");
					detourFailed = true;
				}

				// FORWARD REDIRECTION

				Log.Info("Redirecting Vehicle AI Calculate Segment Calls (1)");
				try {
					Detours.Add(new Detour(typeof(VehicleAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (PathUnit.Position),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte),
								typeof (int),
								typeof (Vector3).MakeByRefType(),
								typeof (Vector3).MakeByRefType(),
								typeof (float).MakeByRefType()
							},
							null),
							typeof(CustomVehicleAI).GetMethod("CustomCalculateSegmentPosition")));
				} catch (Exception) {
					Log.Error("Could not redirect VehicleAI::CalculateSegmentPosition (1).");
					detourFailed = true;
				}


				Log.Info("Redirecting Vehicle AI Calculate Segment Calls (2)");
				try {
					Detours.Add(new Detour(typeof(VehicleAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte),
								typeof (Vector3).MakeByRefType(),
								typeof (Vector3).MakeByRefType(),
								typeof (float).MakeByRefType()
							},
							null),
							typeof(CustomVehicleAI).GetMethod("CustomCalculateSegmentPositionPathFinder")));
				} catch (Exception) {
					Log.Error("Could not redirect VehicleAI::CalculateSegmentPosition (2).");
					detourFailed = true;
				}

				Log.Info("Redirection CitizenManager::ReleaseCitizenInstance calls");
				try {
					Detours.Add(new Detour(typeof(CitizenManager).GetMethod("ReleaseCitizenInstance",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort)
							},
							null),
							typeof(CustomCitizenManager).GetMethod("CustomReleaseCitizenInstance")));
				} catch (Exception) {
					Log.Error("Could not redirect CitizenManager::ReleaseCitizenInstance");
					detourFailed = true;
				}

				Log.Info("Redirection VehicleManager::ReleaseVehicle calls");
				try {
					Detours.Add(new Detour(typeof(VehicleManager).GetMethod("ReleaseVehicle",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort)
							},
							null),
							typeof(CustomVehicleManager).GetMethod("CustomReleaseVehicle")));
				} catch (Exception) {
					Log.Error("Could not redirect VehicleManager::ReleaseVehicle");
					detourFailed = true;
				}

				Log.Info("Redirection VehicleManager::CreateVehicle calls");
				try {
					Detours.Add(new Detour(typeof(VehicleManager).GetMethod("CreateVehicle",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort).MakeByRefType(),
									typeof (Randomizer).MakeByRefType(),
									typeof (VehicleInfo),
									typeof (Vector3),
									typeof (TransferManager.TransferReason),
									typeof (bool),
									typeof (bool)
							},
							null),
							typeof(CustomVehicleManager).GetMethod("CustomCreateVehicle")));
				} catch (Exception) {
					Log.Error("Could not redirect VehicleManager::CreateVehicle calls");
					detourFailed = true;
				}

				Log.Info("Redirecting TramBaseAI Calculate Segment Calls (2)");
				try {
					Detours.Add(new Detour(typeof(TramBaseAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte),
								typeof (Vector3).MakeByRefType(),
								typeof (Vector3).MakeByRefType(),
								typeof (float).MakeByRefType()
							},
							null),
							typeof(CustomTramBaseAI).GetMethod("CustomCalculateSegmentPositionPathFinder")));
				} catch (Exception) {
					Log.Error("Could not redirect TramBaseAI::CalculateSegmentPosition (2).");
					detourFailed = true;
				}

				Log.Info("Redirecting RoadBaseAI::GetTrafficLightNodeState calls");
				try {
					Detours.Add(new Detour(typeof(RoadBaseAI).GetMethod("GetTrafficLightNodeState",
							BindingFlags.Public | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetNode).MakeByRefType(),
									typeof (ushort),
									typeof (NetSegment).MakeByRefType(),
									typeof (NetNode.Flags).MakeByRefType(),
									typeof (Color).MakeByRefType()
							},
							null),
							typeof(CustomRoadAI).GetMethod("CustomGetTrafficLightNodeState")));
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::GetTrafficLightNodeState");
					detourFailed = true;
				}

				//public static void CustomGetTrafficLightNodeState(ushort nodeID, ref NetNode nodeData, ushort segmentID, ref NetSegment segmentData, ref NetNode.Flags flags, ref Color color) {

				Log.Info("Redirecting RoadBaseAI.SimulationStep for nodes");
				try {
					Detours.Add(new Detour(typeof(RoadBaseAI).GetMethod("SimulationStep", new[] { typeof(ushort), typeof(NetNode).MakeByRefType() }),
						typeof(CustomRoadAI).GetMethod("CustomNodeSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirecting RoadBaseAI.SimulationStep for segments");
				try {
					Detours.Add(new Detour(typeof(RoadBaseAI).GetMethod("SimulationStep", new[] { typeof(ushort), typeof(NetSegment).MakeByRefType() }),
						typeof(CustomRoadAI).GetMethod("CustomSegmentSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::SimulationStep.");
				}

				Log.Info("Redirection BuildingAI::GetColor calls");
				try {
					Detours.Add(new Detour(typeof(BuildingAI).GetMethod("GetColor",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Building).MakeByRefType(),
								typeof (InfoManager.InfoMode)
							},
							null), typeof(CustomBuildingAI).GetMethod("CustomGetColor")));
				} catch (Exception) {
					Log.Error("Could not redirect BuildingAI::GetColor");
					detourFailed = true;
				}

				Log.Info("Redirecting CarAI::TrySpawn Calls");
				try {
					Detours.Add(new Detour(typeof(CarAI).GetMethod("TrySpawn",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
								}),
								typeof(CustomCarAI).GetMethod("TrySpawn")));
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::TrySpawn.");
					detourFailed = true;
				}

				/*Log.Info("Redirection CarAI::InvalidPath calls");
				try {
					Detours.Add(new Detour(typeof(CarAI).GetMethod("InvalidPath",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (ushort),
								typeof (Vehicle).MakeByRefType()
							},
							null), typeof(CustomCarAI).GetMethod("InvalidPath")));
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::InvalidPath");
					detourFailed = true;
				}*/

				Log.Info("Redirecting CarAI Simulation Step Calls");
				try {
					Detours.Add(new Detour(typeof(CarAI).GetMethod("SimulationStep",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3)
								}),
								typeof(CustomCarAI).GetMethod("CustomSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirecting CommonBuildingAI::SimulationStep Calls");
				try {
					Detours.Add(new Detour(typeof(CommonBuildingAI).GetMethod("SimulationStep",
								new[] {
									typeof (ushort),
									typeof (Building).MakeByRefType()
								}),
								typeof(CustomCommonBuildingAI).GetMethod("CustomSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect CommonBuildingAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirecting HumanAI Simulation Step Calls");
				try {
					Detours.Add(new Detour(typeof(HumanAI).GetMethod("SimulationStep",
								new[] {
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Vector3)
								}),
								typeof(CustomHumanAI).GetMethod("CustomSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect HumanAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirecting HumanAI::CheckTrafficLights Calls");
				try {
					Detours.Add(new Detour(typeof(HumanAI).GetMethod("CheckTrafficLights",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[] { typeof(ushort), typeof(ushort) },
							null),
							typeof(CustomHumanAI).GetMethod("CustomCheckTrafficLights")));
				} catch (Exception) {
					Log.Error("Could not redirect HumanAI::CheckTrafficLights.");
					detourFailed = true;
				}

				/*Log.Info("Redirecting HumanAI::ArriveAtTarget Calls");
				try {
					Detours.Add(new Detour(typeof(HumanAI).GetMethod("ArriveAtTarget",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[] {
								typeof(ushort),
								typeof(CitizenInstance).MakeByRefType()
							},
							null),
							typeof(CustomHumanAI).GetMethod("CustomArriveAtTarget")));
				} catch (Exception) {
					Log.Error("Could not redirect HumanAI::ArriveAtTarget.");
					detourFailed = true;
				}*/

				Log.Info("Redirection ResidentAI::GetVehicleInfo calls");
				try {
					Detours.Add(new Detour(typeof(ResidentAI).GetMethod("GetVehicleInfo",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (CitizenInstance).MakeByRefType(),
								typeof (bool)
							},
							null), typeof(CustomResidentAI).GetMethod("CustomGetVehicleInfo")));
				} catch (Exception) {
					Log.Error("Could not redirect ResidentAI::GetVehicleInfo");
					detourFailed = true;
				}

				Log.Info("Redirection TouristAI::GetVehicleInfo calls");
				try {
					Detours.Add(new Detour(typeof(TouristAI).GetMethod("GetVehicleInfo",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (CitizenInstance).MakeByRefType(),
								typeof (bool)
							},
							null), typeof(CustomTouristAI).GetMethod("CustomGetVehicleInfo")));
				} catch (Exception) {
					Log.Error("Could not redirect TouristAI::GetVehicleInfo");
					detourFailed = true;
				}

				Log.Info("Redirecting PassengerCarAI Simulation Step Calls");
				try {
					Detours.Add(new Detour(typeof(PassengerCarAI).GetMethod("SimulationStep",
							new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) }),
							typeof(CustomPassengerCarAI).GetMethod("CustomSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect PassengerCarAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirection PassengerCarAI::ParkVehicle calls");
				try {
					Detours.Add(new Detour(typeof(PassengerCarAI).GetMethod("ParkVehicle",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (int),
								typeof (byte).MakeByRefType()
							},
							null), typeof(CustomPassengerCarAI).GetMethod("CustomParkVehicle")));
				} catch (Exception) {
					Log.Error("Could not redirect PassengerCarAI::ParkVehicle");
					detourFailed = true;
				}

				Log.Info("Redirection PassengerCarAI::GetLocalizedStatus calls");
				try {
					Detours.Add(new Detour(typeof(PassengerCarAI).GetMethod("GetLocalizedStatus",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (InstanceID).MakeByRefType()
							},
							null), typeof(CustomPassengerCarAI).GetMethod("CustomGetLocalizedStatus")));
				} catch (Exception) {
					Log.Error("Could not redirect PassengerCarAI::GetLocalizedStatus");
					detourFailed = true;
				}

				Log.Info("Redirection ResidentAI::GetLocalizedStatus calls");
				try {
					Detours.Add(new Detour(typeof(ResidentAI).GetMethod("GetLocalizedStatus",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (CitizenInstance).MakeByRefType(),
								typeof (InstanceID).MakeByRefType()
							},
							null), typeof(CustomResidentAI).GetMethod("CustomGetLocalizedStatus")));
				} catch (Exception) {
					Log.Error("Could not redirect ResidentAI::GetLocalizedStatus");
					detourFailed = true;
				}

				Log.Info("Redirection TouristAI::GetLocalizedStatus calls");
				try {
					Detours.Add(new Detour(typeof(TouristAI).GetMethod("GetLocalizedStatus",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (CitizenInstance).MakeByRefType(),
								typeof (InstanceID).MakeByRefType()
							},
							null), typeof(CustomTouristAI).GetMethod("CustomGetLocalizedStatus")));
				} catch (Exception) {
					Log.Error("Could not redirect TouristAI::GetLocalizedStatus");
					detourFailed = true;
				}

				Log.Info("Redirecting CargoTruckAI Simulation Step Calls");
				try {
					Detours.Add(new Detour(typeof(CargoTruckAI).GetMethod("SimulationStep",
								new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) }),
								typeof(CustomCargoTruckAI).GetMethod("CustomSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect CargoTruckAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirecting TrainAI Simulation Step Calls");
				try {
					Detours.Add(new Detour(typeof(TrainAI).GetMethod("SimulationStep",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3)
								}),
								typeof(CustomTrainAI).GetMethod("TrafficManagerSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirecting TrainAI::TrySpawn Calls");
				try {
					Detours.Add(new Detour(typeof(TrainAI).GetMethod("TrySpawn",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
								}),
								typeof(CustomTrainAI).GetMethod("TrySpawn")));
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::TrySpawn.");
					detourFailed = true;
				}

				Log.Info("Redirection TramBaseAI::SimulationStep calls");
				try {
					Detours.Add(new Detour(typeof(TramBaseAI).GetMethod("SimulationStep",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
							},
							null), typeof(CustomTramBaseAI).GetMethod("CustomSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect TramBaseAI::SimulationStep");
					detourFailed = true;
				}

				Log.Info("Redirecting TramBaseAI::TrySpawn Calls");
				try {
					Detours.Add(new Detour(typeof(TramBaseAI).GetMethod("TrySpawn",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
								}),
								typeof(CustomTramBaseAI).GetMethod("TrySpawn")));
				} catch (Exception) {
					Log.Error("Could not redirect TramBaseAI::TrySpawn.");
					detourFailed = true;
				}

				Log.Info("Redirecting Car AI Calculate Segment Calls");
				try {
					Detours.Add(new Detour(typeof(CarAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (PathUnit.Position),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte),
								typeof (int),
								typeof (Vector3).MakeByRefType(),
								typeof (Vector3).MakeByRefType(),
								typeof (float).MakeByRefType()
							},
							null),
							typeof(CustomCarAI).GetMethod("CustomCalculateSegmentPosition")));
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::CalculateSegmentPosition.");
					detourFailed = true;
				}

				Log.Info("Redirection TramBaseAI Calculate Segment Position calls");
				try {
					Detours.Add(new Detour(typeof(TramBaseAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (PathUnit.Position),
									typeof (PathUnit.Position),
									typeof (uint),
									typeof (byte),
									typeof (PathUnit.Position),
									typeof (uint),
									typeof (byte),
									typeof (int),
									typeof (Vector3).MakeByRefType(),
									typeof (Vector3).MakeByRefType(),
									typeof (float).MakeByRefType()
							},
							null),
							typeof(CustomTramBaseAI).GetMethod("CustomCalculateSegmentPosition")));
				} catch (Exception) {
					Log.Error("Could not redirect TramBaseAI::CalculateSegmentPosition");
					detourFailed = true;
				}


				Log.Info("Redirection PathFind::CalculatePath calls for non-Traffic++");
				try {
					Detours.Add(new Detour(typeof(PathFind).GetMethod("CalculatePath",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (uint),
								typeof (bool)
							},
							null),
							typeof(CustomPathFind).GetMethod("CalculatePath")));
				} catch (Exception) {
					Log.Error("Could not redirect PathFind::CalculatePath");
					detourFailed = true;
				}

				Log.Info("Redirection PathManager::ReleasePath calls for non-Traffic++");
				try {
					Detours.Add(new Detour(typeof(PathManager).GetMethod("ReleasePath",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (uint)
							},
							null),
							typeof(CustomPathManager).GetMethod("ReleasePath")));
				} catch (Exception) {
					Log.Error("Could not redirect PathManager::ReleasePath");
					detourFailed = true;
				}

				Log.Info("Redirection CarAI Calculate Segment Position calls for non-Traffic++");
				try {
					Detours.Add(new Detour(typeof(CarAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte),
								typeof (Vector3).MakeByRefType(),
								typeof (Vector3).MakeByRefType(),
								typeof (float).MakeByRefType()
							},
							null),
							typeof(CustomCarAI).GetMethod("CustomCalculateSegmentPositionPathFinder")));
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::CalculateSegmentPosition");
					detourFailed = true;
				}

				Log.Info("Redirection TrainAI Calculate Segment Position calls for non-Traffic++");
				try {
					Detours.Add(new Detour(typeof(TrainAI).GetMethod("CalculateSegmentPosition",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (PathUnit.Position),
								typeof (uint),
								typeof (byte),
								typeof (Vector3).MakeByRefType(),
								typeof (Vector3).MakeByRefType(),
								typeof (float).MakeByRefType()
							},
							null),
							typeof(CustomTrainAI).GetMethod("TmCalculateSegmentPositionPathFinder")));
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::CalculateSegmentPosition (2)");
					detourFailed = true;
				}

				Log.Info("Redirection AmbulanceAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(AmbulanceAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomAmbulanceAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect AmbulanceAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection BusAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(BusAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomBusAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect BusAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection CarAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(CarAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomCarAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection CargoTruckAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(CargoTruckAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomCargoTruckAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect CargoTruckAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection FireTruckAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(FireTruckAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomFireTruckAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect FireTruckAI::StartPathFind");
					detourFailed = true;
				}

				/*Log.Info("Redirection PassengerCarAI::FindParkingSpaceBuilding calls");
				try {
					Detours.Add(new Detour(typeof(PassengerCarAI).GetMethod("FindParkingSpaceBuilding",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
								typeof (ushort),
								typeof (ushort),
								typeof (ushort),
								typeof (Building).MakeByRefType(),
								typeof (Vector3),
								typeof (float),
								typeof (float),
								typeof (float).MakeByRefType(),
								typeof (Vector3).MakeByRefType(),
								typeof (Quaternion).MakeByRefType()
							},
							null),
							typeof(CustomPassengerCarAI).GetMethod("FindParkingSpaceBuilding",
							new[] {
								typeof (ushort),
								typeof (ushort),
								typeof (ushort),
								typeof (Building).MakeByRefType(),
								typeof (Vector3),
								typeof (float),
								typeof (float),
								typeof (float).MakeByRefType(),
								typeof (Vector3).MakeByRefType(),
								typeof (Quaternion).MakeByRefType()
							})));
				} catch (Exception) {
					Log.Error("Could not redirect PassengerCarAI::FindParkingSpaceBuilding");
					detourFailed = true;
				}*/

				Log.Info("Redirection PassengerCarAI::StartPathFind(1) calls");
				try {
					Detours.Add(new Detour(typeof(PassengerCarAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomPassengerCarAI).GetMethod("CustomStartPathFind",
							new[] {
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							})));
				} catch (Exception) {
					Log.Error("Could not redirect PassengerCarAI::StartPathFind(1)");
					detourFailed = true;
				}

				/*Log.Info("Redirection PassengerCarAI::StartPathFind(2) calls");
				try {
					Detours.Add(new Detour(typeof(PassengerCarAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType()
							},
							null),
							typeof(CustomPassengerCarAI).GetMethod("CustomStartPathFind",
							new[] {
								typeof (ushort),
								typeof (Vehicle).MakeByRefType()
							})));
				} catch (Exception) {
					Log.Error("Could not redirect PassengerCarAI::StartPathFind(2)");
					detourFailed = true;
				}*/

				Log.Info("Redirection PoliceCarAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(PoliceCarAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomPoliceCarAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect PoliceCarAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection TaxiAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(TaxiAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomTaxiAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect TaxiAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection TrainAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(TrainAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomTrainAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection ShipAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(ShipAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomShipAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect ShipAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection CitizenAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(CitizenAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (CitizenInstance).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (VehicleInfo)
							},
							null),
							typeof(CustomCitizenAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect CitizenAI::StartPathFind");
					detourFailed = true;
				}

				/*Log.Info("Redirection CitizenAI::InvalidPath calls");
				try {
					Detours.Add(new Detour(typeof(CitizenAI).GetMethod("InvalidPath",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (CitizenInstance).MakeByRefType()
							},
							null),
							typeof(CustomCitizenAI).GetMethod("CustomInvalidPath")));
				} catch (Exception) {
					Log.Error("Could not redirect CitizenAI::InvalidPath");
					detourFailed = true;
				}*/

				Log.Info("Redirection TransportLineAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(TransportLineAI).GetMethod("StartPathFind",
							BindingFlags.Public | BindingFlags.Static,
							null,
							new[]
							{
								typeof (ushort),
								typeof (NetSegment).MakeByRefType(),
								typeof (ItemClass.Service),
								typeof (VehicleInfo.VehicleType),
								typeof (bool)
							},
							null),
							typeof(CustomTransportLineAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect TransportLineAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection TramBaseAI::StartPathFind calls");
				try {
					Detours.Add(new Detour(typeof(TramBaseAI).GetMethod("StartPathFind",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (Vector3),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomTramBaseAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect TramBaseAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection RoadBaseAI::SetTrafficLightState calls");
				try {
					Detours.Add(new Detour(typeof(RoadBaseAI).GetMethod("SetTrafficLightState",
							BindingFlags.Public | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType(),
									typeof (uint),
									typeof (RoadBaseAI.TrafficLightState),
									typeof (RoadBaseAI.TrafficLightState),
									typeof (bool),
									typeof (bool)
							},
							null),
							typeof(CustomRoadAI).GetMethod("CustomSetTrafficLightState")));
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::SetTrafficLightState");
					detourFailed = true;
				}

				Log.Info("Redirection RoadBaseAI::UpdateLanes calls");
				try {
					Detours.Add(new Detour(typeof(RoadBaseAI).GetMethod("UpdateLanes",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType(),
									typeof (bool)
							},
							null),
							typeof(CustomRoadAI).GetMethod("CustomUpdateLanes")));
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::UpdateLanes");
					detourFailed = true;
				}

				Log.Info("Redirection TrainAI::CheckNextLane calls");
				try {
					Detours.Add(new Detour(typeof(TrainAI).GetMethod("CheckNextLane",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (float).MakeByRefType(),
									typeof (PathUnit.Position),
									typeof (uint),
									typeof (byte),
									typeof (PathUnit.Position),
									typeof (uint),
									typeof (byte),
									typeof (Bezier3)
							},
							null),
							typeof(CustomTrainAI).GetMethod("CustomCheckNextLane")));
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::CheckNextLane");
					detourFailed = true;
				}

				/*Log.Info("Redirection NetManager::FinalizeNode calls");
				try {
					Detours.Add(new Detour(typeof(NetManager).GetMethod("FinalizeNode",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetNode).MakeByRefType()
							},
							null),
							typeof(CustomNetManager).GetMethod("CustomFinalizeNode")));
				} catch (Exception) {
					Log.Error("Could not redirect NetManager::FinalizeNode");
					detourFailed = true;
				}*/

				Log.Info("Redirection NetManager::FinalizeSegment calls");
				try {
					Detours.Add(new Detour(typeof(NetManager).GetMethod("FinalizeSegment",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType()
							},
							null),
							typeof(CustomNetManager).GetMethod("CustomFinalizeSegment")));
				} catch (Exception) {
					Log.Error("Could not redirect NetManager::FinalizeSegment");
					detourFailed = true;
				}

				Log.Info("Redirection NetManager::UpdateSegment calls");
				try {
					Detours.Add(new Detour(typeof(NetManager).GetMethod("UpdateSegment",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (ushort),
									typeof (int),
							},
							null),
							typeof(CustomNetManager).GetMethod("CustomUpdateSegment")));
				} catch (Exception) {
					Log.Error("Could not redirect NetManager::UpdateSegment");
					detourFailed = true;
				}

				if (detourFailed) {
					Log.Info("Detours failed");
					UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Incompatibility Issue", "Traffic Manager: President Edition detected an incompatibility with another mod! You can continue playing but it's NOT recommended. Traffic Manager will not work as expected.", true);
				} else {
					Log.Info("Detours successful");
				}

				DetourInited = true;
			}
		}

		internal static bool IsGameLoaded() {
			return gameLoaded;
		}

		public override void OnCreated(ILoading loading) {
			//SelfDestruct.DestructOldInstances(this);

			base.OnCreated(loading);

			ToolMode = TrafficManagerMode.None;
			Detours = new List<Detour>();
			RegisteredManagers = new List<ICustomManager>();
			DetourInited = false;
			CustomPathManager = new CustomPathManager();

			RegisterCustomManagers();
		}

		private void RegisterCustomManagers() {
			RegisteredManagers.Add(CustomSegmentLightsManager.Instance);
			RegisteredManagers.Add(ExtBuildingManager.Instance);
			RegisteredManagers.Add(ExtCitizenInstanceManager.Instance);
			RegisteredManagers.Add(JunctionRestrictionsManager.Instance);
			RegisteredManagers.Add(LaneArrowManager.Instance);
			RegisteredManagers.Add(LaneConnectionManager.Instance);
			RegisteredManagers.Add(OptionsManager.Instance);
			RegisteredManagers.Add(SpeedLimitManager.Instance);
			RegisteredManagers.Add(TrafficLightManager.Instance);
			RegisteredManagers.Add(TrafficLightSimulationManager.Instance);
			RegisteredManagers.Add(TrafficMeasurementManager.Instance);
			RegisteredManagers.Add(TrafficPriorityManager.Instance);
			RegisteredManagers.Add(UtilityManager.Instance);
			RegisteredManagers.Add(VehicleRestrictionsManager.Instance);
			RegisteredManagers.Add(VehicleStateManager.Instance);
		}

		public override void OnReleased() {
			base.OnReleased();

			if (ToolMode != TrafficManagerMode.None) {
				ToolMode = TrafficManagerMode.None;
				DestroyTool();
			}
		}

		public override void OnLevelUnloading() {
			Log.Info("OnLevelUnloading");
			base.OnLevelUnloading();
			if (IsPathManagerReplaced) {
				CustomPathManager._instance.WaitForAllPaths();
			}

			/*Object.Destroy(BaseUI);
			BaseUI = null;
			Object.Destroy(TransportDemandUI);
			TransportDemandUI = null;*/

			try {
				foreach (ICustomManager manager in RegisteredManagers) {
					manager.OnLevelUnloading();
				}

				/*TrafficPriorityManager.Instance.OnLevelUnloading();
				TrafficMeasurementManager.Instance.OnLevelUnloading();
				CustomTrafficLightsManager.Instance.OnLevelUnloading();
				TrafficLightSimulationManager.Instance.OnLevelUnloading();
				VehicleRestrictionsManager.Instance.OnLevelUnloading();
				ExtCitizenInstanceManager.Instance.OnLevelUnloading();
				ExtBuildingManager.Instance.OnLevelUnloading();
				LaneConnectionManager.Instance.OnLevelUnloading();*/
				Flags.OnLevelUnloading();
				Translation.OnLevelUnloading();
				GlobalConfig.OnLevelUnloading();
#if TRACE
				Singleton<CodeProfiler>.instance.OnLevelUnloading();
#endif
			} catch (Exception e) {
				Log.Error("Exception unloading mod. " + e.Message);
				// ignored - prevents collision with other mods
			}

			revertDetours();
			gameLoaded = false;
		}

		public override void OnLevelLoaded(LoadMode mode) {
			SimulationManager.UpdateMode updateMode = SimulationManager.instance.m_metaData.m_updateMode;
			Log.Info($"OnLevelLoaded({mode}) called. updateMode={updateMode}");
			base.OnLevelLoaded(mode);

			Log._Debug("OnLevelLoaded Returned from base, calling custom code.");

			gameLoaded = false;
			switch (updateMode) {
				case SimulationManager.UpdateMode.NewGameFromMap:
				case SimulationManager.UpdateMode.NewGameFromScenario:
				case SimulationManager.UpdateMode.LoadGame:
					if (BuildConfig.applicationVersion != BuildConfig.VersionToString(TrafficManagerMod.GameVersion, false)) {
						string[] majorVersionElms = BuildConfig.applicationVersion.Split('-');
						string[] versionElms = majorVersionElms[0].Split('.');
						uint versionA = Convert.ToUInt32(versionElms[0]);
						uint versionB = Convert.ToUInt32(versionElms[1]);
						uint versionC = Convert.ToUInt32(versionElms[2]);

						bool isModTooOld = TrafficManagerMod.GameVersionA < versionA ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB < versionB)/* ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB == versionB && TrafficManagerMod.GameVersionC < versionC)*/;

						bool isModNewer = TrafficManagerMod.GameVersionA < versionA ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB > versionB)/* ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB == versionB && TrafficManagerMod.GameVersionC > versionC)*/;

						if (isModTooOld) {
							UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("TM:PE has not been updated yet", $"Traffic Manager: President Edition detected that you are running a newer game version ({BuildConfig.applicationVersion}) than TM:PE has been built for ({BuildConfig.VersionToString(TrafficManagerMod.GameVersion, false)}). Please be aware that TM:PE has not been updated for the newest game version yet and thus it is very likely it will not work as expected.", false);
						} else if (isModNewer) {
							UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Your game should be updated", $"Traffic Manager: President Edition has been built for game version {BuildConfig.VersionToString(TrafficManagerMod.GameVersion, false)}. You are running game version {BuildConfig.applicationVersion}. Some features of TM:PE will not work with older game versions. Please let Steam update your game.", false);
						}
					}
					gameLoaded = true;
					break;
				default:
					Log.Info($"OnLevelLoaded: Unsupported game mode {mode}");
					return;
			}

			IsRainfallLoaded = CheckRainfallIsLoaded();
			IsRushHourLoaded = CheckRushHourIsLoaded();

			if (!IsPathManagerReplaced) {
				try {
					Log.Info("Pathfinder Compatible. Setting up CustomPathManager and SimManager.");
					var pathManagerInstance = typeof(Singleton<PathManager>).GetField("sInstance", BindingFlags.Static | BindingFlags.NonPublic);

					var stockPathManager = PathManager.instance;
					Log._Debug($"Got stock PathManager instance {stockPathManager.GetName()}");

					CustomPathManager = stockPathManager.gameObject.AddComponent<CustomPathManager>();
					Log._Debug("Added CustomPathManager to gameObject List");

					if (CustomPathManager == null) {
						Log.Error("CustomPathManager null. Error creating it.");
						return;
					}

					CustomPathManager.UpdateWithPathManagerValues(stockPathManager);
					Log._Debug("UpdateWithPathManagerValues success");

					pathManagerInstance?.SetValue(null, CustomPathManager);

					Log._Debug("Getting Current SimulationManager");
					var simManager =
						typeof(SimulationManager).GetField("m_managers", BindingFlags.Static | BindingFlags.NonPublic)?
							.GetValue(null) as FastList<ISimulationManager>;

					Log._Debug("Removing Stock PathManager");
					simManager?.Remove(stockPathManager);

					Log._Debug("Adding Custom PathManager");
					simManager?.Add(CustomPathManager);

					Object.Destroy(stockPathManager, 10f);

					Log._Debug("Should be custom: " + Singleton<PathManager>.instance.GetType().ToString());

					IsPathManagerReplaced = true;
				} catch (Exception ex) {
					Log.Error($"Path manager replacement error: {ex.ToString()}");
					UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Incompatibility Issue", "Traffic Manager: President Edition detected an incompatibility with another mod! You can continue playing but it's NOT recommended. Traffic Manager will not work as expected.", true);
				}
			}

			Log.Info("Adding Controls to UI.");
			if (BaseUI == null) {
				Log._Debug("Adding UIBase instance.");
				BaseUI = ToolsModifierControl.toolController.gameObject.AddComponent<UIBase>();
			}

			// Init transport demand UI
			if (TransportDemandUI == null) {
				var uiView = UIView.GetAView();
				TransportDemandUI = (UITransportDemand)uiView.AddUIComponent(typeof(UITransportDemand));
			}

			initDetours();

			Log.Info("Fixing non-created nodes with problems...");
			FixNonCreatedNodeProblems();

			Log.Info("Notifying managers...");
			foreach (ICustomManager manager in RegisteredManagers) {
				manager.OnLevelLoading();
			}

			Log.Info("OnLevelLoaded complete.");
		}

		private void FixNonCreatedNodeProblems() {
			for (ushort nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				if ((NetManager.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
					NetManager.instance.m_nodes.m_buffer[nodeId].m_problems = Notification.Problem.None;
					NetManager.instance.m_nodes.m_buffer[nodeId].m_flags = NetNode.Flags.None;
				}
			}
		}

		private bool CheckRainfallIsLoaded() {
			return Check3rdPartyModLoaded("Rainfall");
		}

		private bool CheckRushHourIsLoaded() {
			return Check3rdPartyModLoaded("RushHour", true);
		}

		private bool Check3rdPartyModLoaded(string namespaceStr, bool printAll=false) {
			bool thirdPartyModLoaded = false;

			var loadingWrapperLoadingExtensionsField = typeof(LoadingWrapper).GetField("m_LoadingExtensions", BindingFlags.NonPublic | BindingFlags.Instance);
			List<ILoadingExtension> loadingExtensions = null;
			if (loadingWrapperLoadingExtensionsField != null) {
				loadingExtensions = (List<ILoadingExtension>)loadingWrapperLoadingExtensionsField.GetValue(Singleton<LoadingManager>.instance.m_LoadingWrapper);
			} else {
				Log._Debug("Could not get loading extensions field");
			}

			if (loadingExtensions != null) {
				foreach (ILoadingExtension extension in loadingExtensions) {
					if (printAll)
						Log.Info($"Detected extension: {extension.GetType().Name} in namespace {extension.GetType().Namespace}");
					if (extension.GetType().Namespace == null)
						continue;

					var nsStr = extension.GetType().Namespace.ToString();
					if (namespaceStr.Equals(nsStr)) {
						Log.Info($"The mod '{namespaceStr}' has been detected.");
						thirdPartyModLoaded = true;
						break;
					}
				}
			} else {
				Log._Debug("Could not get loading extensions");
			}

			return thirdPartyModLoaded;
		}

		public static void SetToolMode(TrafficManagerMode mode) {
			if (mode == ToolMode) return;

			ToolMode = mode;

			if (mode != TrafficManagerMode.None) {
				EnableTool();
			} else {
				DisableTool();
			}
		}

		public static void EnableTool() {
			if (TrafficManagerTool == null) {
				TrafficManagerTool = ToolsModifierControl.toolController.gameObject.GetComponent<TrafficManagerTool>() ??
								   ToolsModifierControl.toolController.gameObject.AddComponent<TrafficManagerTool>();
			}

			ToolsModifierControl.toolController.CurrentTool = TrafficManagerTool;
			ToolsModifierControl.SetTool<TrafficManagerTool>();
		}

		public static void DisableTool() {
			ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
			ToolsModifierControl.SetTool<DefaultTool>();
		}

		private static void DestroyTool() {
			if (ToolsModifierControl.toolController != null) {
				ToolsModifierControl.toolController.CurrentTool = ToolsModifierControl.GetTool<DefaultTool>();
				ToolsModifierControl.SetTool<DefaultTool>();

				if (TrafficManagerTool != null) {
					Object.Destroy(TrafficManagerTool);
					TrafficManagerTool = null;
				}
			} else
				Log.Warning("LoadingExtensions.DestroyTool: ToolsModifierControl.toolController is null!");
		}
	}
}
