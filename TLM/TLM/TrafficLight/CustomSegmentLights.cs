#define DEBUGHKx
#define DEBUGGETx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Custom.AI;
using TrafficManager.Traffic;
using TrafficManager.Manager;

namespace TrafficManager.TrafficLight {
	/// <summary>
	/// Represents the set of custom traffic lights located at a node
	/// </summary>
	public class CustomSegmentLights : ICloneable {
		//private ushort nodeId;
		private bool startNode;
		private ushort segmentId;

		private static readonly ExtVehicleType[] singleLaneVehicleTypes = new ExtVehicleType[] { ExtVehicleType.Tram, ExtVehicleType.Service, ExtVehicleType.CargoTruck, ExtVehicleType.RoadPublicTransport | ExtVehicleType.Service };
		private const ExtVehicleType mainVehicleType = ExtVehicleType.None;

		public bool StartNode {
			get { return startNode; }
		}

		[Obsolete]
		public ushort NodeId {
			get {
				SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
				if (startNode)
					return segGeo.StartNodeId();
				else
					return segGeo.EndNodeId();
			}
		}

		public ushort SegmentId {
			get { return segmentId; }
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

		public RoadBaseAI.TrafficLightState AutoPedestrianLightState = RoadBaseAI.TrafficLightState.Green;

		public RoadBaseAI.TrafficLightState? PedestrianLightState {
			get {
				if (InvalidPedestrianLight || pedestrianLightState == null)
					return RoadBaseAI.TrafficLightState.Green; // no pedestrian crossing at this point

				if (ManualPedestrianMode && pedestrianLightState != null)
					return (RoadBaseAI.TrafficLightState)pedestrianLightState;
				else {
					return AutoPedestrianLightState;
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

		public bool ManualPedestrianMode {
			get { return manualPedestrianMode; }
			set {
				if (! manualPedestrianMode && value) {
					PedestrianLightState = AutoPedestrianLightState;
				}
				manualPedestrianMode = value;
			}
		}

		private bool manualPedestrianMode = false;

		internal RoadBaseAI.TrafficLightState? pedestrianLightState = null;
		protected CustomSegmentLight mainSegmentLight = null;

		public ICustomSegmentLightsManager LightsManager {
			get {
				return lightsManager;
			}
			set {
				lightsManager = value;
				OnChange();
			}
		}
		private ICustomSegmentLightsManager lightsManager;

		internal void Relocate(ushort segmentId, bool startNode) {
			this.segmentId = segmentId;
			this.startNode = startNode;
			housekeeping(true, true);
		}

		public override string ToString() {
			String ret = $"InvalidPedestrianLight={InvalidPedestrianLight} PedestrianLightState={PedestrianLightState} ManualPedestrianMode={ManualPedestrianMode}\n";
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				ret += $"\tVehicleType={e.Key} Light={e.Value.ToString()}\n";
			}
			return ret;
		}

		[Obsolete]
		protected CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort nodeId, ushort segmentId, bool calculateAutoPedLight)
			: this(lightsManager, segmentId, nodeId == SegmentGeometry.Get(segmentId).StartNodeId(), calculateAutoPedLight) {

		}

		protected CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight) {
			this.startNode = startNode;
			this.lightsManager = lightsManager;
			this.segmentId = segmentId;
			OnChange();
		}

		[Obsolete]
		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort nodeId, ushort segmentId, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState)
			: this(lightsManager, segmentId, nodeId == SegmentGeometry.Get(segmentId).StartNodeId(), calculateAutoPedLight, mainState) {

		}

		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState)
			: this(lightsManager, segmentId, startNode, calculateAutoPedLight, mainState, mainState, mainState, mainState == RoadBaseAI.TrafficLightState.Green ? RoadBaseAI.TrafficLightState.Red	: RoadBaseAI.TrafficLightState.Green) {
			
		}

		[Obsolete]
		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort nodeId, ushort segmentId, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState, RoadBaseAI.TrafficLightState leftState, RoadBaseAI.TrafficLightState rightState, RoadBaseAI.TrafficLightState pedState)
			: this(lightsManager, segmentId, nodeId == SegmentGeometry.Get(segmentId).StartNodeId(), calculateAutoPedLight, mainState, leftState, rightState, pedState)  {
			
		}

		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState, RoadBaseAI.TrafficLightState leftState, RoadBaseAI.TrafficLightState rightState, RoadBaseAI.TrafficLightState pedState) {
			this.lightsManager = lightsManager;
			this.startNode = startNode;
			this.segmentId = segmentId;

			housekeeping(false, calculateAutoPedLight, mainState, leftState, rightState, pedState);
		}

		public bool IsAnyGreen() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				if (light.IsAnyGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyInTransition() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				if (light.IsAnyInTransition())
					return true;
			}
			return false;
		}

		public bool IsAnyLeftGreen() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				if (light.IsLeftGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyMainGreen() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				if (light.IsMainGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyRightGreen() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				if (light.IsRightGreen())
					return true;
			}
			return false;
		}

		public bool IsAllLeftRed() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				if (! light.IsLeftRed())
					return false;
			}
			return true;
		}

		public bool IsAllMainRed() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				if (! light.IsMainRed())
					return false;
			}
			return true;
		}

		public bool IsAllRightRed() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				if (! light.IsRightRed())
					return false;
			}
			return true;
		}

		public void UpdateVisuals() {
			if (mainSegmentLight == null)
				return;

			mainSegmentLight.UpdateVisuals();
		}
		
		public object Clone() {
			CustomSegmentLights clone = new CustomSegmentLights(LightsManager, segmentId, startNode, false);
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				clone.CustomLights.Add(e.Key, (CustomSegmentLight)e.Value.Clone());
			}
			clone.pedestrianLightState = pedestrianLightState;
			clone.manualPedestrianMode = manualPedestrianMode;
			clone.VehicleTypes = new LinkedList<ExtVehicleType>(VehicleTypes);
			clone.LastChangeFrame = LastChangeFrame;
			clone.mainSegmentLight = mainSegmentLight;
			clone.AutoPedestrianLightState = AutoPedestrianLightState;
			clone.housekeeping(false, false);
			return clone;
		}

		internal CustomSegmentLight GetCustomLight(byte laneIndex) {
			if (!VehicleTypeByLaneIndex.ContainsKey(laneIndex)) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type found for lane index");
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
				return mainSegmentLight;
			}
			CustomSegmentLight light = CustomLights[vehicleType];
#if DEBUGGET
			Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Returning custom light for vehicle type {vehicleType}");
#endif
			return light;
		}

		internal CustomSegmentLight GetCustomLight(ExtVehicleType vehicleType) {
			CustomSegmentLight ret = null;
			if (CustomLights.ContainsKey(vehicleType))
				ret = CustomLights[vehicleType];
			else
				ret = mainSegmentLight;

			return ret;

			/*if (vehicleType != ExtVehicleType.None)
				Log._Debug($"No traffic light for vehicle type {vehicleType} defined at segment {segmentId}, node {nodeId}.");*/
		}

		internal void MakeRed() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				light.MakeRed();
			}
		}

		internal void MakeRedOrGreen() {
			foreach (CustomSegmentLight light in CustomLights.Values) {
				light.MakeRedOrGreen();
			}
		}

		internal void SetLights(RoadBaseAI.TrafficLightState lightState) {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				e.Value.SetStates(lightState, lightState, lightState, false);
			}
			CalculateAutoPedestrianLightState();
		}

		internal void SetLights(CustomSegmentLights otherLights) {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in otherLights.CustomLights) {
				CustomSegmentLight ourLight = null;
				if (!CustomLights.TryGetValue(e.Key, out ourLight)) {
					continue;
				}

				ourLight.SetStates(e.Value.LightMain, e.Value.LightLeft, e.Value.LightRight, false);
				//ourLight.LightPedestrian = e.Value.LightPedestrian;
			}
			pedestrianLightState = otherLights.pedestrianLightState;
			manualPedestrianMode = otherLights.manualPedestrianMode;
			AutoPedestrianLightState = otherLights.AutoPedestrianLightState;
		}

		public void ChangeLightPedestrian() {
			if (PedestrianLightState != null) {
				var invertedLight = PedestrianLightState == RoadBaseAI.TrafficLightState.Green
					? RoadBaseAI.TrafficLightState.Red
					: RoadBaseAI.TrafficLightState.Green;

				PedestrianLightState = invertedLight;
				UpdateVisuals();
			}
		}

		private static uint getCurrentFrame() {
			return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
		}

		public uint LastChange() {
			return getCurrentFrame() - LastChangeFrame;
		}

		public void OnChange(bool calculateAutoPedLight=true) {
			LastChangeFrame = getCurrentFrame();

			if (calculateAutoPedLight)
				CalculateAutoPedestrianLightState();
		}

		internal void CalculateAutoPedestrianLightState(bool propagate=true) {
			//Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Calculating pedestrian light state of node {NodeId}");
			SegmentGeometry segGeo = SegmentGeometry.Get(SegmentId);
			SegmentEndGeometry segmentEndGeometry = StartNode ? segGeo.StartNodeGeometry : segGeo.EndNodeGeometry;

			if (segmentEndGeometry == null) {
				Log._Debug($"Could not get SegmentEndGeometry for segment {SegmentId} @ {NodeId}.");
				AutoPedestrianLightState = RoadBaseAI.TrafficLightState.Green;
				return;
			}
			ushort nodeId = segmentEndGeometry.NodeId();

			if (propagate) {
				foreach (ushort otherSegmentId in segmentEndGeometry.ConnectedSegments) {
					if (otherSegmentId == 0)
						continue;

					CustomSegmentLights otherLights = LightsManager.GetSegmentLights(nodeId, otherSegmentId);
					if (otherLights == null) {
						//Log._Debug($"Expected other (propagate) CustomSegmentLights at segment {otherSegmentId} @ {NodeId} but there was none. Original segment id: {SegmentId}");
						continue;
					}

					otherLights.CalculateAutoPedestrianLightState(false);
				}
			}

			if (IsAnyGreen()) {
				//Log._Debug($"Any green at seg. {SegmentId} @ {NodeId}");
				AutoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
				return;
			}

			//Log._Debug($"Querying incoming segments at seg. {SegmentId} @ {NodeId}");
			RoadBaseAI.TrafficLightState autoPedestrianLightState = RoadBaseAI.TrafficLightState.Green;

			if (!segmentEndGeometry.IncomingOneWay) {
				// query straight segments
				foreach (ushort otherSegmentId in segmentEndGeometry.IncomingStraightSegments) {
					if (otherSegmentId == 0)
						continue;
					//Log._Debug($"Checking incoming straight segment {otherSegmentId} at seg. {SegmentId} @ {NodeId}");

					CustomSegmentLights otherLights = LightsManager.GetSegmentLights(nodeId, otherSegmentId);
					if (otherLights == null) {
						//Log._Debug($"Expected other (straight) CustomSegmentLights at segment {otherSegmentId} @ {NodeId} but there was none. Original segment id: {SegmentId}");
						continue;
					}

					if (!otherLights.IsAllMainRed()) {
						//Log._Debug($"Not all main red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
						autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
						break;
					}
				}

				// query left/right segments
				if (autoPedestrianLightState == RoadBaseAI.TrafficLightState.Green) {
					bool lhd = TrafficPriorityManager.IsLeftHandDrive();
					foreach (ushort otherSegmentId in lhd ? segmentEndGeometry.IncomingLeftSegments : segmentEndGeometry.IncomingRightSegments) {
						if (otherSegmentId == 0)
							continue;

						//Log._Debug($"Checking left/right segment {otherSegmentId} at seg. {SegmentId} @ {NodeId}");

						CustomSegmentLights otherLights = LightsManager.GetSegmentLights(nodeId, otherSegmentId);
						if (otherLights == null) {
							//Log._Debug($"Expected other (left/right) CustomSegmentLights at segment {otherSegmentId} @ {NodeId} but there was none. Original segment id: {SegmentId}");
							continue;
						}

						if ((lhd && !otherLights.IsAllRightRed()) || (!lhd && !otherLights.IsAllLeftRed())) {
							//Log._Debug($"Not all left red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
							autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
							break;
						}
					}
				}
			}

			AutoPedestrianLightState = autoPedestrianLightState;
			//Log.Warning($"Calculated AutoPedestrianLightState for segment {SegmentId} @ {NodeId}: {AutoPedestrianLightState}");
		}

		internal void housekeeping(bool mayDelete, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState = RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState leftState = RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState rightState = RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState pedState = RoadBaseAI.TrafficLightState.Red) {
			// we intentionally never delete vehicle types (because we may want to retain traffic light states if a segment is upgraded or replaced)

			ushort nodeId = NodeId;
			HashSet<ExtVehicleType> setupLights = new HashSet<ExtVehicleType>();
			Dictionary<byte, ExtVehicleType> allAllowedTypes = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypesAsDict(segmentId, nodeId);
			ExtVehicleType allAllowedMask = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(segmentId, nodeId);
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
							CustomLights.Add(mask, new TrafficLight.CustomSegmentLight(this, mainState, leftState, rightState));
							VehicleTypes.AddFirst(mask);
						}
						VehicleTypeByLaneIndex[laneIndex] = mask;
						laneIndicesWithoutSeparateLights.Remove(laneIndex);
						++numLights;
						addPedestrianLight = true;
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
					CustomLights.Add(mainVehicleType, new TrafficLight.CustomSegmentLight(this, mainState, leftState, rightState));
					VehicleTypes.AddFirst(mainVehicleType);
				}
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

			OnChange(calculateAutoPedLight);
#if DEBUGHK
			Log._Debug($"CustomSegmentLights: housekeeping @ seg. {segmentId}, node {nodeId}: Housekeeping complete. VehicleTypeByLaneIndex={string.Join("; ", VehicleTypeByLaneIndex.Select(x => x.Key + "=" + x.Value).ToArray())} CustomLights={string.Join("; ", CustomLights.Select(x => x.Key.ToString()).ToArray())}");
#endif
		}
	}
}
