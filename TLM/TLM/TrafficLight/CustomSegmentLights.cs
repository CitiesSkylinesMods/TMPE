#define DEBUGHKx
#define DEBUGGETx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;
using UnityEngine;
using TrafficManager.Custom.AI;
using System.Linq;

namespace TrafficManager.TrafficLight {
	public class CustomSegmentLights : ICloneable {
		private ushort nodeId;
		private ushort segmentId;

		private static readonly ExtVehicleType mainVehicleType = ExtVehicleType.None;

		public ushort NodeId {
			get { return nodeId; }
			private set { nodeId = value; }
		}

		public ushort SegmentId {
			get { return segmentId; }
			set { segmentId = value; housekeeping(true); }
		}

		public uint LastChangeFrame;

		public bool InvalidPedestrianLight = false;

		public Dictionary<ExtVehicleType, CustomSegmentLight> CustomLights {
			get; private set;
		} = new Dictionary<ExtVehicleType, CustomSegmentLight>();

		public LinkedList<ExtVehicleType> VehicleTypes {
			get; private set;
		} = new LinkedList<ExtVehicleType>();

		public Dictionary<byte, ExtVehicleType> VehicleTypeByLaneIndex {
			get; private set;
		} = new Dictionary<byte, ExtVehicleType>();

		/// <summary>
		/// Vehicles types that have their own traffic light
		/// </summary>
		public ExtVehicleType SeparateVehicleTypes {
			get; private set;
		} = ExtVehicleType.None;

		public RoadBaseAI.TrafficLightState? PedestrianLightState {
			get {
				if (pedestrianLightState == null)
					return RoadBaseAI.TrafficLightState.Green; // no pedestrian crossing at this point

				if (ManualPedestrianMode && pedestrianLightState != null)
					return (RoadBaseAI.TrafficLightState)pedestrianLightState;
				else {
					return GetAutoPedestrianLightState();
				}
			}
			set {
				if (pedestrianLightState == null) {
#if DEBUGHK
					Log._Debug($"CustomSegmentLights: Refusing to change pedestrian light at segment {segmentId}");
#endif
                    return;
				}
				//Log._Debug($"CustomSegmentLights: Setting pedestrian light at segment {segmentId}");
				pedestrianLightState = value;
			}
		}

		public RoadBaseAI.TrafficLightState GetAutoPedestrianLightState() {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.GetAutoPedestrianLightState");
#endif
			if (InvalidPedestrianLight) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.GetAutoPedestrianLightState");
#endif
				return RoadBaseAI.TrafficLightState.Green;
			}

			RoadBaseAI.TrafficLightState pedState = RoadBaseAI.TrafficLightState.Green;
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				RoadBaseAI.TrafficLightState vehState = e.Value.GetVisualLightState();

				if (vehState == RoadBaseAI.TrafficLightState.Red)
					continue;

				if (vehState == RoadBaseAI.TrafficLightState.Green) {
					pedState = RoadBaseAI.TrafficLightState.Red;
					break;
				}

				if (vehState == RoadBaseAI.TrafficLightState.RedToGreen) {
					pedState = RoadBaseAI.TrafficLightState.GreenToRed;
					continue;
				}

				if (vehState == RoadBaseAI.TrafficLightState.GreenToRed &&
					pedState != RoadBaseAI.TrafficLightState.GreenToRed) {
					pedState = RoadBaseAI.TrafficLightState.RedToGreen;
				}
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.GetAutoPedestrianLightState");
#endif
			return pedState;
		}

		public bool ManualPedestrianMode {
			get; set;
		} = false;

		protected RoadBaseAI.TrafficLightState? pedestrianLightState = null;
		private ExtVehicleType autoPedestrianVehicleType = ExtVehicleType.None;
		protected CustomSegmentLight mainSegmentLight = null;

		protected CustomSegmentLights(ushort nodeId, ushort segmentId) {
			this.nodeId = nodeId;
			this.segmentId = segmentId;
			PedestrianLightState = null;
			OnChange();
		}

		public CustomSegmentLights(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState mainState) {
			this.nodeId = nodeId;
			this.segmentId = segmentId;
			PedestrianLightState = null;
			OnChange();

			RoadBaseAI.TrafficLightState pedState = mainState == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;
			housekeeping(false, mainState, mainState, mainState, pedState);
		}

		public CustomSegmentLights(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState mainState, RoadBaseAI.TrafficLightState leftState, RoadBaseAI.TrafficLightState rightState, RoadBaseAI.TrafficLightState pedState) {
			this.nodeId = nodeId;
			this.segmentId = segmentId;
			PedestrianLightState = null;
			OnChange();

			housekeeping(false, mainState, leftState, rightState, pedState);
		}

		private static ExtVehicleType[] singleLaneVehicleTypes = new ExtVehicleType[] { ExtVehicleType.Tram, ExtVehicleType.Service, ExtVehicleType.CargoTruck, ExtVehicleType.RoadPublicTransport | ExtVehicleType.Service };

		internal void housekeeping(bool mayDelete, RoadBaseAI.TrafficLightState mainState = RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState leftState = RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState rightState = RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState pedState = RoadBaseAI.TrafficLightState.Red) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.housekeeping");
#endif
			// we intentionally never delete vehicle types (because we may want to retain traffic light states if a segment is upgraded or replaced)

			HashSet<ExtVehicleType> setupLights = new HashSet<ExtVehicleType>();
			Dictionary<byte, ExtVehicleType> allAllowedTypes = VehicleRestrictionsManager.GetAllowedVehicleTypesAsDict(segmentId, nodeId);
			ExtVehicleType allAllowedMask = VehicleRestrictionsManager.GetAllowedVehicleTypes(segmentId, nodeId);
			SeparateVehicleTypes = ExtVehicleType.None;
#if DEBUGHK
			Log._Debug($"CustomSegmentLights: housekeeping @ seg. {segmentId}, node {nodeId}, allAllowedTypes={string.Join(", ", allAllowedTypes.Select(x => x.ToString()).ToArray())}");
#endif
			bool addPedestrianLight = false;
			uint numLights = 0;
			VehicleTypeByLaneIndex.Clear();
			HashSet<byte> laneIndicesWithoutSeparateLights = new HashSet<byte>(allAllowedTypes.Keys);
			foreach (KeyValuePair<byte, ExtVehicleType> e in allAllowedTypes) {
				byte laneIndex = e.Key;
				ExtVehicleType allowedTypes = e.Value;

				foreach (ExtVehicleType mask in singleLaneVehicleTypes) {
					if (setupLights.Contains(mask))
						break;

					if ((allowedTypes & mask) != ExtVehicleType.None && (allowedTypes & ~(mask | ExtVehicleType.Emergency)) == ExtVehicleType.None) {
#if DEBUGHK
						Log._Debug($"CustomSegmentLights: housekeeping @ seg. {segmentId}, node {nodeId}: adding {mask} light");
#endif

						if (!CustomLights.ContainsKey(mask)) {
							CustomLights.Add(mask, new TrafficLight.CustomSegmentLight(this, nodeId, segmentId, mainState, leftState, rightState));
							VehicleTypes.AddFirst(mask);
						}
						VehicleTypeByLaneIndex[laneIndex] = mask;
						laneIndicesWithoutSeparateLights.Remove(laneIndex);
						++numLights;
						addPedestrianLight = true;
						autoPedestrianVehicleType = mask;
						mainSegmentLight = CustomLights[mask];
						setupLights.Add(mask);
						SeparateVehicleTypes |= mask;
						break;
					}
				}
			}

			if (allAllowedTypes.Count > numLights) {
#if DEBUGHK
				Log._Debug($"CustomSegmentLights: housekeeping @ seg. {segmentId}, node {nodeId}: adding main vehicle light: {mainVehicleType}");
#endif

				// traffic lights for cars
				if (!CustomLights.ContainsKey(mainVehicleType)) {
					CustomLights.Add(mainVehicleType, new TrafficLight.CustomSegmentLight(this, nodeId, segmentId, mainState, leftState, rightState));
					VehicleTypes.AddFirst(mainVehicleType);
				}
				autoPedestrianVehicleType = mainVehicleType;
				mainSegmentLight = CustomLights[mainVehicleType];
				foreach (byte laneIndex in laneIndicesWithoutSeparateLights)
					VehicleTypeByLaneIndex[laneIndex] = ExtVehicleType.None;
				addPedestrianLight = allAllowedMask == ExtVehicleType.None || (allAllowedMask & ~ExtVehicleType.RailVehicle) != ExtVehicleType.None;
			} else {
				addPedestrianLight = true;
			}

#if DEBUGHK
			if (addPedestrianLight) {
				Log._Debug($"CustomSegmentLights: housekeeping @ seg. {segmentId}, node {nodeId}: adding ped. light");
			}
#endif

			if (mayDelete) {
				// delete traffic lights for non-existing configurations
				HashSet<ExtVehicleType> vehicleTypesToDelete = new HashSet<ExtVehicleType>();
				foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
					if (e.Key == mainVehicleType)
						continue;
					if (!setupLights.Contains(e.Key))
						vehicleTypesToDelete.Add(e.Key);
				}

				foreach (ExtVehicleType vehicleType in vehicleTypesToDelete) {
#if DEBUGHK
					Log._Debug($"Deleting traffic light for {vehicleType} at segment {segmentId}, node {nodeId}");
#endif
					CustomLights.Remove(vehicleType);
					VehicleTypes.Remove(vehicleType);
				}
			}

			if (CustomLights.ContainsKey(mainVehicleType) && VehicleTypes.First.Value != mainVehicleType) {
				VehicleTypes.Remove(mainVehicleType);
				VehicleTypes.AddFirst(mainVehicleType);
			}

			if (addPedestrianLight) {
#if DEBUGHK
				Log._Debug($"CustomSegmentLights: housekeeping @ seg. {segmentId}, node {nodeId}: adding pedestrian light");
#endif
				if (pedestrianLightState == null)
					pedestrianLightState = pedState;
			} else {
				pedestrianLightState = null;
			}

#if DEBUGHK
			Log._Debug($"CustomSegmentLights: housekeeping @ seg. {segmentId}, node {nodeId}: Housekeeping complete. VehicleTypeByLaneIndex={string.Join("; ", VehicleTypeByLaneIndex.Select(x => x.Key + "=" + x.Value).ToArray())} CustomLights={string.Join("; ", CustomLights.Select(x => x.Key.ToString()).ToArray())}");
#endif
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.housekeeping");
#endif
		}

		public void UpdateVisuals() {
			if (mainSegmentLight == null)
				return;
			/*CustomSegmentLight visualLight = null;
			if (!CustomLights.TryGetValue(ExtVehicleType.RoadVehicle, out visualLight) && !CustomLights.TryGetValue(ExtVehicleType.RailVehicle, out visualLight))
				return;*/

			mainSegmentLight.UpdateVisuals();
		}
		
		public object Clone() {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.Clone");
#endif
			CustomSegmentLights clone = new CustomSegmentLights(nodeId, segmentId);
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				clone.CustomLights.Add(e.Key, (CustomSegmentLight)e.Value.Clone());
			}
			clone.pedestrianLightState = pedestrianLightState;
			clone.ManualPedestrianMode = ManualPedestrianMode;
			clone.VehicleTypes = new LinkedList<ExtVehicleType>(VehicleTypes);
			clone.LastChangeFrame = LastChangeFrame;
			clone.autoPedestrianVehicleType = autoPedestrianVehicleType;
			//if (autoPedestrianVehicleType != ExtVehicleType.None) {
			clone.CustomLights.TryGetValue(clone.autoPedestrianVehicleType, out clone.mainSegmentLight);
			//clone.mainSegmentLight = clone.CustomLights[autoPedestrianVehicleType];
			//}
			clone.housekeeping(false);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.Clone");
#endif
			return clone;
		}

		internal CustomSegmentLight GetCustomLight(byte laneIndex) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.GetCustomLight(laneIndex)");
#endif
			if (!VehicleTypeByLaneIndex.ContainsKey(laneIndex)) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type found for lane index");
#endif
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.GetCustomLight(laneIndex)");
#endif
				return mainSegmentLight;
			}
			ExtVehicleType vehicleType = VehicleTypeByLaneIndex[laneIndex];
#if DEBUGGET
			Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Vehicle type is {vehicleType}");
#endif
			if (!CustomLights.ContainsKey(vehicleType)) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No custom light found for vehicle type {vehicleType}");
#endif
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.GetCustomLight(laneIndex)");
#endif
				return mainSegmentLight;
			}
			CustomSegmentLight light = CustomLights[vehicleType];
#if DEBUGGET
			Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Returning custom light for vehicle type {vehicleType}");
#endif

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.GetCustomLight(laneIndex)");
#endif
			return light;
		}

		internal CustomSegmentLight GetCustomLight(ExtVehicleType vehicleType) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.GetCustomLight(vehicleType)");
#endif

			CustomSegmentLight ret = null;
			if (CustomLights.ContainsKey(vehicleType))
				ret = CustomLights[vehicleType];
			else
				ret = mainSegmentLight;

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.GetCustomLight(vehicleType)");
#endif

			return ret;

			/*if (vehicleType != ExtVehicleType.None)
				Log._Debug($"No traffic light for vehicle type {vehicleType} defined at segment {segmentId}, node {nodeId}.");*/
		}

		internal void MakeRed() {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.MakeRed");
#endif
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				e.Value.MakeRed();
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.MakeRed");
#endif
		}

		internal void MakeRedOrGreen() {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.MakeRedOrGreen");
#endif
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				e.Value.MakeRedOrGreen();
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.MakeRedOrGreen");
#endif
		}

		internal void SetLights(CustomSegmentLights otherLights) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.SetLights");
#endif
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in otherLights.CustomLights) {
				CustomSegmentLight ourLight = null;
				if (!CustomLights.TryGetValue(e.Key, out ourLight))
					continue;

				ourLight.LightMain = e.Value.LightMain;
				ourLight.LightLeft = e.Value.LightLeft;
				ourLight.LightRight = e.Value.LightRight;
				//ourLight.LightPedestrian = e.Value.LightPedestrian;
			}
			pedestrianLightState = otherLights.pedestrianLightState;
			ManualPedestrianMode = otherLights.ManualPedestrianMode;
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.SetLights");
#endif
		}

		internal void ChangeLightPedestrian() {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomSegmentLights.ChangeLightPedestrian");
#endif
			if (PedestrianLightState != null) {
				var invertedLight = PedestrianLightState == RoadBaseAI.TrafficLightState.Green
					? RoadBaseAI.TrafficLightState.Red
					: RoadBaseAI.TrafficLightState.Green;

				PedestrianLightState = invertedLight;
				UpdateVisuals();
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomSegmentLights.ChangeLightPedestrian");
#endif
		}

		private static uint getCurrentFrame() {
			return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
		}

		public uint LastChange() {
			return getCurrentFrame() - LastChangeFrame;
		}

		public void OnChange() {
			LastChangeFrame = getCurrentFrame();
		}
	}
}
