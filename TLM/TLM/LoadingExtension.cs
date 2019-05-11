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
using CSUtil.Commons;
using TrafficManager.Custom.Data;
using TrafficManager.Manager.Impl;

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
		//public static TrafficManagerMode ToolMode { get; set; }
		//public static TrafficManagerTool TrafficManagerTool { get; set; }
#if !TAM
		public static UIBase BaseUI { get; private set; }
#endif

		public static UITransportDemand TransportDemandUI { get; private set; }

		public static List<ICustomManager> RegisteredManagers { get; private set; }

		public static bool IsGameLoaded { get; private set; } = false;

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
			// TODO realize detouring with annotations
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

#if DEBUGBUSBUG
				// TODO remove
				Log.Info("Reverse-Redirection CustomNetManager::FinalizeNode calls");
				try {
					Detours.Add(new Detour(typeof(CustomNetManager).GetMethod("FinalizeNode",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetNode).MakeByRefType(),
							},
							null),
							typeof(NetManager).GetMethod("FinalizeNode",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (NetNode).MakeByRefType(),
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomNetManager::FinalizeNode");
					detourFailed = true;
				}

				// TODO remove
				Log.Info("Reverse-Redirection CustomNetManager::InitializeNode calls");
				try {
					Detours.Add(new Detour(typeof(CustomNetManager).GetMethod("InitializeNode",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetNode).MakeByRefType(),
							},
							null),
							typeof(NetManager).GetMethod("InitializeNode",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (NetNode).MakeByRefType(),
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomNetManager::InitializeNode");
					detourFailed = true;
				}

				// TODO remove
				Log.Info("Reverse-Redirection CustomNetManager::InitializeSegment calls");
				try {
					Detours.Add(new Detour(typeof(CustomNetManager).GetMethod("InitializeSegment",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType(),
							},
							null),
							typeof(NetManager).GetMethod("InitializeSegment",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType(),
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomNetManager::InitializeSegment");
					detourFailed = true;
				}
#endif

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

				Log.Info("Reverse-Redirection CustomCitizenManager::ReleaseCitizenImplementation calls");
				try {
					Detours.Add(new Detour(typeof(CustomCitizenManager).GetMethod("ReleaseCitizenImplementation",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (uint),
									typeof (Citizen).MakeByRefType(),
							},
							null),
							typeof(CitizenManager).GetMethod("ReleaseCitizenImplementation",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (uint),
									typeof (Citizen).MakeByRefType(),
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomCitizenManager::ReleaseCitizenImplementation");
					detourFailed = true;
				}

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

				Log.Info("Reverse-Redirection ResidentAI::GetElectricCarProbability calls");
				try {
					Detours.Add(new Detour(typeof(CustomResidentAI).GetMethod("GetElectricCarProbability",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Citizen.AgePhase)
							},
							null),
							typeof(ResidentAI).GetMethod("GetElectricCarProbability",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (Citizen.AgePhase)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect ResidentAI::GetElectricCarProbability");
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

				Log.Info("Reverse-Redirection TouristAI::GetElectricCarProbability calls");
				try {
					Detours.Add(new Detour(
							typeof(CustomTouristAI).GetMethod("GetElectricCarProbability", BindingFlags.NonPublic | BindingFlags.Instance),
							typeof(TouristAI).GetMethod("GetElectricCarProbability", BindingFlags.NonPublic | BindingFlags.Instance)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect TouristAI::GetElectricCarProbability");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection TouristAI::GetCamperProbability calls");
				try {
					Detours.Add(new Detour(typeof(CustomTouristAI).GetMethod("GetCamperProbability",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (Citizen.Wealth)
							},
							null),
							typeof(TouristAI).GetMethod("GetCamperProbability",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (Citizen.Wealth)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect TouristAI::GetCamperProbability");
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

				Log.Info("Reverse-Redirection HumanAI::WaitTouristVehicle calls");
				try {
					Detours.Add(new Detour(typeof(CustomHumanAI).GetMethod("WaitTouristVehicle",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (ushort)
							},
							null),
							typeof(HumanAI).GetMethod("WaitTouristVehicle",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (CitizenInstance).MakeByRefType(),
									typeof (ushort)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect HumanAI::WaitTouristVehicle");
					detourFailed = true;
				}

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

				/*Log.Info("Reverse-Redirection PassengerCarAI::FindParkingSpaceBuilding calls");
				try {
					Detours.Add(new Detour(typeof(CustomPassengerCarAI).GetMethod("FindParkingSpaceBuilding",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (ushort),
									typeof (Vector3),
									typeof (float),
									typeof (float),
									typeof (float),
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
									typeof (Vector3),
									typeof (float),
									typeof (float),
									typeof (float),
									typeof (Vector3).MakeByRefType(),
									typeof (Quaternion).MakeByRefType()
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect PassengerCarAI::FindParkingSpaceBuilding");
					detourFailed = true;
				}*/

				Log.Info("Reverse-Redirection PassengerCarAI::FindParkingSpace calls");
				try {
					Detours.Add(new Detour(typeof(CustomPassengerCarAI).GetMethod("FindParkingSpace",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (bool),
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
									typeof (bool),
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
									typeof (bool),
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
									typeof (bool),
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

				Log.Info("Reverse-Redirection CarAI::CheckOtherVehicle calls");
				try {
					Detours.Add(new Detour(typeof(CustomCarAI).GetMethod("CheckOtherVehicle",
							BindingFlags.NonPublic | BindingFlags.Static),
							typeof(CarAI).GetMethod("CheckOtherVehicle",
								BindingFlags.NonPublic | BindingFlags.Static)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CarAI::CheckOtherVehicle");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CarAI::CheckCitizen calls");
				try {
					Detours.Add(new Detour(typeof(CustomCarAI).GetMethod("CheckCitizen",
							BindingFlags.NonPublic | BindingFlags.Static),
							typeof(CarAI).GetMethod("CheckCitizen",
								BindingFlags.NonPublic | BindingFlags.Static)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CarAI::CheckCitizen");
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

				Log.Info("Reverse-Redirection TramBaseAI::UpdatePathTargetPositions calls");
				MethodInfo sourceMethod = null, targetMethod = null;
				try {
					sourceMethod = typeof(CustomTramBaseAI).GetMethod("InvokeUpdatePathTargetPositions",
							BindingFlags.Public | BindingFlags.Static,
								null,
								new[]
								{
									typeof (TramBaseAI),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3),
									typeof (Vector3),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (int).MakeByRefType(),
									typeof (int),
									typeof (int),
									typeof (float),
									typeof (float)
								},
								null);

					targetMethod = typeof(TramBaseAI).GetMethod("UpdatePathTargetPositions",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3),
									typeof (Vector3),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (int).MakeByRefType(),
									typeof (int),
									typeof (int),
									typeof (float),
									typeof (float)
								},
								null);

					Detours.Add(new Detour(sourceMethod, targetMethod));
				} catch (Exception e) {
					Log.Error($"Could not reverse-redirect TramBaseAI::UpdatePathTargetPositions: {e} sourceMethod={sourceMethod} targetMethod={targetMethod}");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTramBaseAI::GetMaxSpeed calls");
				try {
					Detours.Add(new Detour(typeof(CustomTramBaseAI).GetMethod("GetMaxSpeed",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
							},
							null), typeof(TramBaseAI).GetMethod("GetMaxSpeed",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomTramBaseAI::GetMaxSpeed");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTramBaseAI::CalculateMaxSpeed calls");
				try {
					Detours.Add(new Detour(typeof(CustomTramBaseAI).GetMethod("CalculateMaxSpeed",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (float),
									typeof (float),
									typeof (float)
							},
							null), typeof(TramBaseAI).GetMethod("CalculateMaxSpeed",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (float),
									typeof (float),
									typeof (float)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomTramBaseAI::CalculateMaxSpeed");
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
					Log.Error("Could not reverse-redirect CustomTrainAI::CheckOverlap (2)");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection TrainAI::UpdatePathTargetPositions calls");
				try {
					Detours.Add(new Detour(typeof(CustomTrainAI).GetMethod("InvokeUpdatePathTargetPositions",
							BindingFlags.Public | BindingFlags.Static,
							null,
							new[]
							{
									typeof (TrainAI),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3),
									typeof (Vector3),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (int).MakeByRefType(),
									typeof (int),
									typeof (int),
									typeof (float),
									typeof (float)
							},
							null),
							typeof(TrainAI).GetMethod("UpdatePathTargetPositions",
								BindingFlags.NonPublic | BindingFlags.Instance,
								null,
								new[]
								{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3),
									typeof (Vector3),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (int).MakeByRefType(),
									typeof (int),
									typeof (int),
									typeof (float),
									typeof (float)
								},
								null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect TrainAI::UpdatePathTargetPositions");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTrainAI::Reverse calls");
				try {
					Detours.Add(new Detour(typeof(CustomTrainAI).GetMethod("Reverse",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
							},
							null), typeof(TrainAI).GetMethod("Reverse",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomTrainAI::Reverse");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTrainAI::GetMaxSpeed calls");
				try {
					Detours.Add(new Detour(typeof(CustomTrainAI).GetMethod("GetMaxSpeed",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
							},
							null), typeof(TrainAI).GetMethod("GetMaxSpeed",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType()
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomTrainAI::GetMaxSpeed");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTrainAI::CalculateMaxSpeed calls");
				try {
					Detours.Add(new Detour(typeof(CustomTrainAI).GetMethod("CalculateMaxSpeed",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (float),
									typeof (float),
									typeof (float)
							},
							null), typeof(TrainAI).GetMethod("CalculateMaxSpeed",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (float),
									typeof (float),
									typeof (float)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomTrainAI::CalculateMaxSpeed");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTransportLineAI::GetStopLane calls");
				try {
					Detours.Add(new Detour(typeof(CustomTransportLineAI).GetMethod("GetStopLane",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (PathUnit.Position).MakeByRefType(),
									typeof (VehicleInfo.VehicleType)
							},
							null), typeof(TransportLineAI).GetMethod("GetStopLane",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (PathUnit.Position).MakeByRefType(),
									typeof (VehicleInfo.VehicleType)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomTransportLineAI::GetStopLane");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTransportLineAI::CheckSegmentProblems calls");
				try {
					Detours.Add(new Detour(typeof(CustomTransportLineAI).GetMethod("CheckSegmentProblems",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType()
							},
							null), typeof(TransportLineAI).GetMethod("CheckSegmentProblems",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetSegment).MakeByRefType()
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomTransportLineAI::CheckSegmentProblems");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomTransportLineAI::CheckNodeProblems calls");
				try {
					Detours.Add(new Detour(typeof(CustomTransportLineAI).GetMethod("CheckNodeProblems",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetNode).MakeByRefType()
							},
							null), typeof(TransportLineAI).GetMethod("CheckNodeProblems",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetNode).MakeByRefType()
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomTransportLineAI::CheckNodeProblems");
					detourFailed = true;
				}

				Log.Info("Reverse-Redirection CustomVehicleAI::FindBestLane calls");
				try {
					Detours.Add(new Detour(typeof(CustomVehicleAI).GetMethod("FindBestLane",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (PathUnit.Position)
							},
							null), typeof(VehicleAI).GetMethod("FindBestLane",
							BindingFlags.NonPublic | BindingFlags.Static,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (PathUnit.Position)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not reverse-redirect CustomVehicleAI::FindBestLane");
					detourFailed = true;
				}

				// FORWARD REDIRECTION

				/*Log.Info("Redirecting NetAI::AfterSplitOrMove");
				try {
					Detours.Add(new Detour(
							typeof(NetAI).GetMethod("AfterSplitOrMove"),
							typeof(CustomNetAI).GetMethod("CustomAfterSplitOrMove")));
				} catch (Exception) {
					Log.Error("Could not redirect NetAI::AfterSplitOrMove.");
					detourFailed = true;
				}*/

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

				Log.Info("Redirecting VehicleAI::UpdatePathTargetPositions calls");
				try {
					Detours.Add(new Detour(typeof(VehicleAI).GetMethod("UpdatePathTargetPositions",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (int).MakeByRefType(),
								typeof (int),
								typeof (float),
								typeof (float)
							},
							null),
							typeof(CustomVehicleAI).GetMethod("CustomUpdatePathTargetPositions",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
								typeof (int).MakeByRefType(),
								typeof (int),
								typeof (float),
								typeof (float)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not redirect VehicleAI::UpdatePathTargetPositions.");
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

				Log.Info("Redirection CitizenManager::ReleaseCitizen calls");
				try {
					Detours.Add(new Detour(typeof(CitizenManager).GetMethod("ReleaseCitizen",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (uint)
							},
							null),
							typeof(CustomCitizenManager).GetMethod("CustomReleaseCitizen")));
				} catch (Exception) {
					Log.Error("Could not redirect CitizenManager::ReleaseCitizen");
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

				Log.Info("Redirecting RoadBaseAI::ClickNodeButton calls");
				try {
					Detours.Add(new Detour(
							typeof(RoadBaseAI).GetMethod("ClickNodeButton"),
							typeof(CustomRoadAI).GetMethod("CustomClickNodeButton")));
				} catch (Exception) {
					Log.Error("Could not redirect RoadBaseAI::ClickNodeButton");
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

				/*Log.Info("Redirecting CarAI::CheckOtherVehicles Calls");
				try {
					Detours.Add(new Detour(typeof(CarAI).GetMethod("CheckOtherVehicles",
								BindingFlags.Public | BindingFlags.Static),
								typeof(CustomCarAI).GetMethod("CustomCheckOtherVehicles",
								BindingFlags.Public | BindingFlags.Static
								)));
				} catch (Exception) {
					Log.Error("Could not redirect CarAI::CheckOtherVehicles.");
					detourFailed = true;
				}*/

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

				Log.Info("Redirecting HumanAI::ArriveAtDestination Calls");
				try {
					Detours.Add(new Detour(typeof(HumanAI).GetMethod("ArriveAtDestination",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[] {
								typeof (ushort),
								typeof (CitizenInstance).MakeByRefType(),
								typeof (bool)
							},
							null),
							typeof(CustomHumanAI).GetMethod("CustomArriveAtDestination",
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
					Log.Error("Could not redirect HumanAI::ArriveAtDestination.");
					detourFailed = true;
				}

				Log.Info("Redirection ResidentAI::GetVehicleInfo calls");
				try {
					Detours.Add(new Detour(typeof(ResidentAI).GetMethod("GetVehicleInfo",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (CitizenInstance).MakeByRefType(),
								typeof (bool),
								typeof (VehicleInfo).MakeByRefType()
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
								typeof (bool),
								typeof (VehicleInfo).MakeByRefType()
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

				Log.Info("Redirecting PassengerCarAI UpdateParkedVehicle Calls");
				try {
					Detours.Add(new Detour(typeof(PassengerCarAI).GetMethod("UpdateParkedVehicle",
								new[] {
									typeof (ushort),
									typeof (VehicleParked).MakeByRefType()
								}),
								typeof(CustomPassengerCarAI).GetMethod("CustomUpdateParkedVehicle")));
				} catch (Exception) {
					Log.Error("Could not redirect PassengerCarAI::UpdateParkedVehicle.");
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

				Log.Info("Redirecting CargoTruckAI::SimulationStep calls");
				try {
					Detours.Add(new Detour(typeof(CargoTruckAI).GetMethod("SimulationStep",
								new[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vector3) }),
								typeof(CustomCargoTruckAI).GetMethod("CustomSimulationStep")));
				} catch (Exception) {
					Log.Error("Could not redirect CargoTruckAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirecting TrainAI::SimulationStep calls");
				try {
					Detours.Add(new Detour(typeof(TrainAI).GetMethod("SimulationStep",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3)
								}),
								typeof(CustomTrainAI).GetMethod("CustomSimulationStep",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vector3)
								})));
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::SimulationStep.");
					detourFailed = true;
				}

				Log.Info("Redirecting TrainAI::SimulationStep (2) calls");
				try {
					Detours.Add(new Detour(typeof(TrainAI).GetMethod("SimulationStep",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vehicle.Frame).MakeByRefType(),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (int)
								}),
								typeof(CustomTrainAI).GetMethod("CustomSimulationStep",
								new[] {
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (Vehicle.Frame).MakeByRefType(),
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (int)
								})));
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::SimulationStep (2).");
					detourFailed = true;
				}

				/*Log.Info("Redirecting TrainAI::TrySpawn Calls");
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
				}*/

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
							null), typeof(CustomTramBaseAI).GetMethod("CustomSimulationStep",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vector3),
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not redirect TramBaseAI::SimulationStep");
					detourFailed = true;
				}

				Log.Info("Redirection TramBaseAI::SimulationStep (2) calls");
				try {
					Detours.Add(new Detour(typeof(TramBaseAI).GetMethod("SimulationStep",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vehicle.Frame).MakeByRefType(),
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (int)
							},
							null), typeof(CustomTramBaseAI).GetMethod("CustomSimulationStep",
							BindingFlags.Public | BindingFlags.Instance,
							null,
							new[]
							{
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (Vehicle.Frame).MakeByRefType(),
								typeof (ushort),
								typeof (Vehicle).MakeByRefType(),
								typeof (int)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not redirect TramBaseAI::SimulationStep (2)");
					detourFailed = true;
				}

				/*Log.Info("Redirection TramBaseAI::ResetTargets calls");
				try {
					Detours.Add(new Detour(typeof(TramBaseAI).GetMethod("ResetTargets",
							BindingFlags.NonPublic | BindingFlags.Static),
							typeof(CustomTramBaseAI).GetMethod("CustomResetTargets",
							BindingFlags.NonPublic | BindingFlags.Static)));
				} catch (Exception) {
					Log.Error("Could not redirect TramBaseAI::ResetTargets");
					detourFailed = true;
				}*/

				/*Log.Info("Redirecting TramBaseAI::TrySpawn Calls");
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
				}*/

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
#if PF2
							typeof(CustomPathFind2).GetMethod("CalculatePath")));
#else
							typeof(CustomPathFind).GetMethod("CalculatePath")));
#endif
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
								typeof (VehicleInfo),
								typeof (bool),
								typeof (bool)
							},
							null),
							typeof(CustomCitizenAI).GetMethod("CustomStartPathFind")));
				} catch (Exception) {
					Log.Error("Could not redirect CitizenAI::StartPathFind");
					detourFailed = true;
				}

				Log.Info("Redirection CitizenAI::FindPathPosition calls");
				try {
					Detours.Add(new Detour(typeof(CitizenAI).GetMethod("FindPathPosition",
							BindingFlags.Public | BindingFlags.Instance),
							typeof(CustomCitizenAI).GetMethod("CustomFindPathPosition")));
				} catch (Exception) {
					Log.Error("Could not redirect CitizenAI::FindPathPosition");
					detourFailed = true;
				}

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

				Log.Info("Redirection TrainAI::ForceTrafficLights calls");
				try {
					Detours.Add(new Detour(typeof(TrainAI).GetMethod("ForceTrafficLights",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (bool)
							},
							null),
							typeof(CustomTrainAI).GetMethod("CustomForceTrafficLights",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (Vehicle).MakeByRefType(),
									typeof (bool)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not redirect TrainAI::CheckNextLane");
					detourFailed = true;
				}

				// TODO remove
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

#if DEBUGBUSBUG
				// TODO remove
				Log.Info("Redirection NetManager::MoveNode calls");
				try {
					Detours.Add(new Detour(typeof(NetManager).GetMethod("MoveNode",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetNode).MakeByRefType(),
									typeof (Vector3)
							},
							null),
							typeof(CustomNetManager).GetMethod("CustomMoveNode",
							BindingFlags.NonPublic | BindingFlags.Instance,
							null,
							new[]
							{
									typeof (ushort),
									typeof (NetNode).MakeByRefType(),
									typeof (Vector3)
							},
							null)));
				} catch (Exception) {
					Log.Error("Could not redirect NetManager::MoveNode");
					detourFailed = true;
				}
#endif

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

				Log.Info("Redirection Vehicle::Spawn calls");
				try {
					Detours.Add(new Detour(typeof(Vehicle).GetMethod("Spawn", BindingFlags.Public | BindingFlags.Instance), typeof(CustomVehicle).GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static)));
				} catch (Exception) {
					Log.Error("Could not redirect Vehicle::Spawn");
					detourFailed = true;
				}

				Log.Info("Redirection Vehicle::Unspawn calls");
				try {
					Detours.Add(new Detour(typeof(Vehicle).GetMethod("Unspawn", BindingFlags.Public | BindingFlags.Instance), typeof(CustomVehicle).GetMethod("Unspawn", BindingFlags.Public | BindingFlags.Static)));
				} catch (Exception) {
					Log.Error("Could not redirect Vehicle::Unspawn");
					detourFailed = true;
				}

				if (detourFailed) {
					Log.Info("Detours failed");
					Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
						UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("TM:PE failed to load", "Traffic Manager: President Edition failed to load. You can continue playing but it's NOT recommended. Traffic Manager will not work as expected.", true);
					});
				} else {
					Log.Info("Detours successful");
				}

				DetourInited = true;
			}
		}

		public override void OnCreated(ILoading loading) {
			//SelfDestruct.DestructOldInstances(this);

			base.OnCreated(loading);

			Detours = new List<Detour>();
			RegisteredManagers = new List<ICustomManager>();
			DetourInited = false;
			CustomPathManager = new CustomPathManager();

			RegisterCustomManagers();
		}

		private void RegisterCustomManagers() {
			// TODO represent data dependencies differently
			RegisteredManagers.Add(GeometryManager.Instance);
			RegisteredManagers.Add(AdvancedParkingManager.Instance);
			RegisteredManagers.Add(CustomSegmentLightsManager.Instance);
			RegisteredManagers.Add(ExtBuildingManager.Instance);
			RegisteredManagers.Add(ExtCitizenInstanceManager.Instance);
			RegisteredManagers.Add(ExtCitizenManager.Instance);
			RegisteredManagers.Add(TurnOnRedManager.Instance);
			RegisteredManagers.Add(LaneArrowManager.Instance);
			RegisteredManagers.Add(LaneConnectionManager.Instance);
			RegisteredManagers.Add(OptionsManager.Instance);
			RegisteredManagers.Add(ParkingRestrictionsManager.Instance);
			RegisteredManagers.Add(RoutingManager.Instance);
			RegisteredManagers.Add(SegmentEndManager.Instance);
			RegisteredManagers.Add(SpeedLimitManager.Instance);
			RegisteredManagers.Add(TrafficLightManager.Instance);
			RegisteredManagers.Add(TrafficLightSimulationManager.Instance);
			RegisteredManagers.Add(TrafficMeasurementManager.Instance);
			RegisteredManagers.Add(TrafficPriorityManager.Instance);
			RegisteredManagers.Add(UtilityManager.Instance);
			RegisteredManagers.Add(VehicleRestrictionsManager.Instance);
			RegisteredManagers.Add(VehicleStateManager.Instance);
			RegisteredManagers.Add(JunctionRestrictionsManager.Instance); // depends on TurnOnRedManager, TrafficLightManager, TrafficLightSimulationManager
		}

		public override void OnReleased() {
			base.OnReleased();

			UIBase.ReleaseTool();
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
					Log.Info($"OnLevelUnloading: {manager.GetType().Name}");
					manager.OnLevelUnloading();
				}
				Flags.OnLevelUnloading();
				Translation.OnLevelUnloading();
				GlobalConfig.OnLevelUnloading();

				// remove vehicle button
				var removeVehicleButtonExtender = UIView.GetAView().gameObject.GetComponent<RemoveVehicleButtonExtender>();
				if (removeVehicleButtonExtender != null) {
					Object.Destroy(removeVehicleButtonExtender, 10f);
				}

				// remove citizen instance button
				var removeCitizenInstanceButtonExtender = UIView.GetAView().gameObject.GetComponent<RemoveCitizenInstanceButtonExtender>();
				if (removeCitizenInstanceButtonExtender != null) {
					Object.Destroy(removeCitizenInstanceButtonExtender, 10f);
				}
#if TRACE
				Singleton<CodeProfiler>.instance.OnLevelUnloading();
#endif
			} catch (Exception e) {
				Log.Error("Exception unloading mod. " + e.Message);
				// ignored - prevents collision with other mods
			}

			revertDetours();
			IsGameLoaded = false;
		}

		public override void OnLevelLoaded(LoadMode mode) {
			SimulationManager.UpdateMode updateMode = SimulationManager.instance.m_metaData.m_updateMode;
			Log.Info($"OnLevelLoaded({mode}) called. updateMode={updateMode}");
			base.OnLevelLoaded(mode);

			Log._Debug("OnLevelLoaded Returned from base, calling custom code.");

			IsGameLoaded = false;
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

						Log.Info($"Detected game version v{BuildConfig.applicationVersion}");

						bool isModTooOld = TrafficManagerMod.GameVersionA < versionA ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB < versionB)/* ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB == versionB && TrafficManagerMod.GameVersionC < versionC)*/;

						bool isModNewer = TrafficManagerMod.GameVersionA < versionA ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB > versionB)/* ||
							(TrafficManagerMod.GameVersionA == versionA && TrafficManagerMod.GameVersionB == versionB && TrafficManagerMod.GameVersionC > versionC)*/;

						if (isModTooOld) {
							string msg = $"Traffic Manager: President Edition detected that you are running a newer game version ({BuildConfig.applicationVersion}) than TM:PE has been built for ({BuildConfig.VersionToString(TrafficManagerMod.GameVersion, false)}). Please be aware that TM:PE has not been updated for the newest game version yet and thus it is very likely it will not work as expected.";
							Log.Error(msg);
							Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
								UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("TM:PE has not been updated yet", msg, false);
							});
						} else if (isModNewer) {
							string msg = $"Traffic Manager: President Edition has been built for game version {BuildConfig.VersionToString(TrafficManagerMod.GameVersion, false)}. You are running game version {BuildConfig.applicationVersion}. Some features of TM:PE will not work with older game versions. Please let Steam update your game.";
							Log.Error(msg);
							Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
								UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Your game should be updated", msg, false);
							});
						}
					}
					IsGameLoaded = true;
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
					string error = "Traffic Manager: President Edition failed to load. You can continue playing but it's NOT recommended. Traffic Manager will not work as expected.";
					Log.Error(error);
					Log.Error($"Path manager replacement error: {ex.ToString()}");
					Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(() => {
						UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("TM:PE failed to load", error, true);
					});
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

			// add "remove vehicle" button
			UIView.GetAView().gameObject.AddComponent<RemoveVehicleButtonExtender>();

			// add "remove citizen instance" button
			UIView.GetAView().gameObject.AddComponent<RemoveCitizenInstanceButtonExtender>();
			
			initDetours();

			//Log.Info("Fixing non-created nodes with problems...");
			//FixNonCreatedNodeProblems();

			Log.Info("Notifying managers...");
			foreach (ICustomManager manager in RegisteredManagers) {
				Log.Info($"OnLevelLoading: {manager.GetType().Name}");
				manager.OnLevelLoading();
			}

			//InitTool();
			//Log._Debug($"Current tool: {ToolManager.instance.m_properties.CurrentTool}");

			Log.Info("OnLevelLoaded complete.");
		}

		/*private void FixNonCreatedNodeProblems() {
			for (int nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				if ((NetManager.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
					NetManager.instance.m_nodes.m_buffer[nodeId].m_problems = Notification.Problem.None;
					NetManager.instance.m_nodes.m_buffer[nodeId].m_flags = NetNode.Flags.None;
				}
			}
		}*/

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
				Log.Warning("Could not get loading extensions field");
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
	}
}
