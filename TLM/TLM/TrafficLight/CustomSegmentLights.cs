#define DEBUGGETx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Custom.AI;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using System.Linq;
using TrafficManager.Util;
using CSUtil.Commons;
using static TrafficManager.Manager.VehicleRestrictionsManager;

namespace TrafficManager.TrafficLight {
	/// <summary>
	/// Represents the set of custom traffic lights located at a node
	/// </summary>
	public class CustomSegmentLights : SegmentEndId, ICloneable {
		private static readonly ExtVehicleType[] SINGLE_LANE_VEHICLETYPES = new ExtVehicleType[] { ExtVehicleType.Tram, ExtVehicleType.Service, ExtVehicleType.CargoTruck, ExtVehicleType.RoadPublicTransport | ExtVehicleType.Service, ExtVehicleType.RailVehicle };
		private const ExtVehicleType DEFAULT_MAIN_VEHICLETYPE = ExtVehicleType.None;

		[Obsolete]
		public ushort NodeId {
			get {
				SegmentGeometry segGeo = SegmentGeometry.Get(SegmentId);

				if (segGeo == null) {
					Log.Info($"CustomSegmentLights.NodeId: No geometry information available for segment {SegmentId}");
					return 0;
				}

				if (StartNode)
					return segGeo.StartNodeId();
				else
					return segGeo.EndNodeId();
			}
		}

		public short ClockwiseIndex {
			get {
				return LightsManager.ClockwiseIndexOfSegmentEnd(this);
			}
		}

		public uint LastChangeFrame;

		public bool InvalidPedestrianLight = false;

		public IDictionary<ExtVehicleType, CustomSegmentLight> CustomLights {
			get; private set;
		} = new TinyDictionary<ExtVehicleType, CustomSegmentLight>();

		public LinkedList<ExtVehicleType> VehicleTypes { // TODO replace collection
			get; private set;
		} = new LinkedList<ExtVehicleType>();

		public ExtVehicleType?[] VehicleTypeByLaneIndex {
			get; private set;
		} = new ExtVehicleType?[0];

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
					Log._Debug($"CustomSegmentLights: Refusing to change pedestrian light at segment {SegmentId}");
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
				if (!manualPedestrianMode && value) {
					PedestrianLightState = AutoPedestrianLightState;
				}
				manualPedestrianMode = value;
			}
		}

		private bool manualPedestrianMode = false;

		internal RoadBaseAI.TrafficLightState? pedestrianLightState = null;
		private ExtVehicleType mainVehicleType = ExtVehicleType.None;
		protected CustomSegmentLight MainSegmentLight {
			get {
				CustomSegmentLight res = null;
				CustomLights.TryGetValue(mainVehicleType, out res);
				return res;
			}
		}

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

		public override string ToString() {
			return $"[CustomSegmentLights {base.ToString()} @ node {NodeId}\n" +
			"\t" + $"LastChangeFrame: {LastChangeFrame}\n" +
			"\t" + $"InvalidPedestrianLight: {InvalidPedestrianLight}\n" +
			"\t" + $"CustomLights: {CustomLights}\n" +
			"\t" + $"VehicleTypes: {VehicleTypes.CollectionToString()}\n" +
			"\t" + $"VehicleTypeByLaneIndex: {VehicleTypeByLaneIndex.ArrayToString()}\n" +
			"\t" + $"SeparateVehicleTypes: {SeparateVehicleTypes}\n" +
			"\t" + $"AutoPedestrianLightState: {AutoPedestrianLightState}\n" +
			"\t" + $"PedestrianLightState: {PedestrianLightState}\n" +
			"\t" + $"ManualPedestrianMode: {ManualPedestrianMode}\n" +
			"\t" + $"manualPedestrianMode: {manualPedestrianMode}\n" +
			"\t" + $"pedestrianLightState: {pedestrianLightState}\n" +
			"\t" + $"MainSegmentLight: {MainSegmentLight}\n" +
			"CustomSegmentLights]";
		}

		internal bool Relocate(ushort segmentId, bool startNode, ICustomSegmentLightsManager lightsManager) {
			if (Relocate(segmentId, startNode)) {
				this.lightsManager = lightsManager;
				housekeeping(true, true);
				return true;
			}
			return false;
		}

		[Obsolete]
		protected CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort nodeId, ushort segmentId, bool calculateAutoPedLight)
			: this(lightsManager, segmentId, nodeId == SegmentGeometry.Get(segmentId)?.StartNodeId(), calculateAutoPedLight) {

		}

		protected CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight) : base(segmentId, startNode) {
			this.lightsManager = lightsManager;
			OnChange(calculateAutoPedLight);
		}

		[Obsolete]
		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort nodeId, ushort segmentId, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState)
			: this(lightsManager, segmentId, nodeId == SegmentGeometry.Get(segmentId)?.StartNodeId(), calculateAutoPedLight, mainState) {

		}

		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState)
			: this(lightsManager, segmentId, startNode, calculateAutoPedLight, mainState, mainState, mainState, mainState == RoadBaseAI.TrafficLightState.Green ? RoadBaseAI.TrafficLightState.Red	: RoadBaseAI.TrafficLightState.Green) {
			
		}

		[Obsolete]
		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort nodeId, ushort segmentId, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState, RoadBaseAI.TrafficLightState leftState, RoadBaseAI.TrafficLightState rightState, RoadBaseAI.TrafficLightState pedState)
			: this(lightsManager, segmentId, nodeId == SegmentGeometry.Get(segmentId)?.StartNodeId(), calculateAutoPedLight, mainState, leftState, rightState, pedState)  {
			
		}

		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight, RoadBaseAI.TrafficLightState mainState, RoadBaseAI.TrafficLightState leftState, RoadBaseAI.TrafficLightState rightState, RoadBaseAI.TrafficLightState pedState) : base(segmentId, startNode) {
			this.lightsManager = lightsManager;

			housekeeping(false, calculateAutoPedLight, mainState, leftState, rightState, pedState);
		}

		public bool IsAnyGreen() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				if (e.Value.IsAnyGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyInTransition() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				if (e.Value.IsAnyInTransition())
					return true;
			}
			return false;
		}

		public bool IsAnyLeftGreen() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				if (e.Value.IsLeftGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyMainGreen() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				if (e.Value.IsMainGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyRightGreen() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				if (e.Value.IsRightGreen())
					return true;
			}
			return false;
		}

		public bool IsAllLeftRed() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				if (!e.Value.IsLeftRed())
					return false;
			}
			return true;
		}

		public bool IsAllMainRed() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				if (!e.Value.IsMainRed())
					return false;
			}
			return true;
		}

		public bool IsAllRightRed() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				if (!e.Value.IsRightRed())
					return false;
			}
			return true;
		}

		public void UpdateVisuals() {
			if (MainSegmentLight == null)
				return;

			MainSegmentLight.UpdateVisuals();
		}
		
		public object Clone() {
			return Clone(LightsManager, true);
		}

		public CustomSegmentLights Clone(ICustomSegmentLightsManager newLightsManager, bool performHousekeeping=true) {
			CustomSegmentLights clone = new CustomSegmentLights(newLightsManager != null ? newLightsManager : LightsManager, SegmentId, StartNode, false);
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				clone.CustomLights.Add(e.Key, (CustomSegmentLight)e.Value.Clone());
			}
			clone.pedestrianLightState = pedestrianLightState;
			clone.manualPedestrianMode = manualPedestrianMode;
			clone.VehicleTypes = new LinkedList<ExtVehicleType>(VehicleTypes);
			clone.LastChangeFrame = LastChangeFrame;
			clone.mainVehicleType = mainVehicleType;
			clone.AutoPedestrianLightState = AutoPedestrianLightState;
			if (performHousekeeping) {
				clone.housekeeping(false, false);
			}
			return clone;
		}

		internal CustomSegmentLight GetCustomLight(byte laneIndex) {
			if (laneIndex >= VehicleTypeByLaneIndex.Length) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type found for lane index");
#endif
				return MainSegmentLight;
			}

			ExtVehicleType? vehicleType = VehicleTypeByLaneIndex[laneIndex];

			if (vehicleType == null) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type found for lane index: lane is invalid");
#endif
				return MainSegmentLight;
			}

#if DEBUGGET
			Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Vehicle type is {vehicleType}");
#endif
			CustomSegmentLight light;
			if (!CustomLights.TryGetValue((ExtVehicleType)vehicleType, out light)) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No custom light found for vehicle type {vehicleType}");
#endif
				return MainSegmentLight;
			}
#if DEBUGGET
			Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Returning custom light for vehicle type {vehicleType}");
#endif
			return light;
		}

		internal CustomSegmentLight GetCustomLight(ExtVehicleType vehicleType) {
			CustomSegmentLight ret = null;
			if (!CustomLights.TryGetValue(vehicleType, out ret)) {
				ret = MainSegmentLight;
			}

			return ret;

			/*if (vehicleType != ExtVehicleType.None)
				Log._Debug($"No traffic light for vehicle type {vehicleType} defined at segment {segmentId}, node {nodeId}.");*/
		}

		internal void MakeRed() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				e.Value.MakeRed();
			}
		}

		internal void MakeRedOrGreen() {
			foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
				e.Value.MakeRedOrGreen();
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
			SegmentEndGeometry segmentEndGeometry = SegmentGeometry.Get(SegmentId)?.GetEnd(StartNode);

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
					bool lhd = Constants.ServiceFactory.SimulationService.LeftHandDrive;
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
			HashSet<ExtVehicleType> setupLights = new HashSet<ExtVehicleType>(); // TODO improve
			IDictionary<byte, ExtVehicleType> allAllowedTypes = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypesAsDict(SegmentId, nodeId, RestrictionMode.Restricted); // TODO improve
			ExtVehicleType allAllowedMask = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(SegmentId, nodeId, RestrictionMode.Restricted);
			SeparateVehicleTypes = ExtVehicleType.None;
#if DEBUGHK
			Log._Debug($"CustomSegmentLights: housekeeping @ seg. {SegmentId}, node {nodeId}, allAllowedTypes={string.Join(", ", allAllowedTypes.Select(x => x.ToString()).ToArray())}, allAllowedMask={allAllowedMask}");
#endif
			bool addPedestrianLight = false;
			uint numLights = 0;
			Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segId, ref NetSegment segment) {
				VehicleTypeByLaneIndex = new ExtVehicleType?[segment.Info.m_lanes.Length];
				return true;
			});
			HashSet<byte> laneIndicesWithoutSeparateLights = new HashSet<byte>(allAllowedTypes.Keys); // TODO improve
			foreach (KeyValuePair<byte, ExtVehicleType> e in allAllowedTypes) {
				byte laneIndex = e.Key;
				ExtVehicleType allowedTypes = e.Value;

				foreach (ExtVehicleType mask in SINGLE_LANE_VEHICLETYPES) {
					if (setupLights.Contains(mask)) {
						++numLights;
						break;
					}

					if ((allowedTypes & mask) != ExtVehicleType.None && (allowedTypes & ~(mask | ExtVehicleType.Emergency)) == ExtVehicleType.None) {
#if DEBUGHK
						Log._Debug($"CustomSegmentLights: housekeeping @ seg. {SegmentId}, node {nodeId}: adding {mask} light");
#endif

						CustomSegmentLight segmentLight;
						if (!CustomLights.TryGetValue(mask, out segmentLight)) {
							segmentLight = new TrafficLight.CustomSegmentLight(this, mainState, leftState, rightState);
							CustomLights.Add(mask, segmentLight);
							VehicleTypes.AddFirst(mask);
						}
						mainVehicleType = mask;
						VehicleTypeByLaneIndex[laneIndex] = mask;
						laneIndicesWithoutSeparateLights.Remove(laneIndex);
						++numLights;
						addPedestrianLight = true;
						setupLights.Add(mask);
						SeparateVehicleTypes |= mask;
						break;
					}
				}
			}

			if (allAllowedTypes.Count > numLights) {
#if DEBUGHK
				Log._Debug($"CustomSegmentLights: housekeeping @ seg. {SegmentId}, node {nodeId}: adding default main vehicle light: {DEFAULT_MAIN_VEHICLETYPE}");
#endif

				// traffic lights for cars
				CustomSegmentLight defaultSegmentLight;
				if (!CustomLights.TryGetValue(DEFAULT_MAIN_VEHICLETYPE, out defaultSegmentLight)) {
					defaultSegmentLight = new TrafficLight.CustomSegmentLight(this, mainState, leftState, rightState);
					CustomLights.Add(DEFAULT_MAIN_VEHICLETYPE, defaultSegmentLight);
					VehicleTypes.AddFirst(DEFAULT_MAIN_VEHICLETYPE);
				}
				mainVehicleType = DEFAULT_MAIN_VEHICLETYPE;

				foreach (byte laneIndex in laneIndicesWithoutSeparateLights) {
					VehicleTypeByLaneIndex[laneIndex] = ExtVehicleType.None;
				}
				addPedestrianLight = true;
			} else {
				addPedestrianLight = allAllowedMask == ExtVehicleType.None || (allAllowedMask & ~ExtVehicleType.RailVehicle) != ExtVehicleType.None;
			}

			if (mayDelete) {
				// delete traffic lights for non-existing configurations
				HashSet<ExtVehicleType> vehicleTypesToDelete = new HashSet<ExtVehicleType>();
				foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
					if (e.Key == DEFAULT_MAIN_VEHICLETYPE)
						continue;
					if (!setupLights.Contains(e.Key))
						vehicleTypesToDelete.Add(e.Key);
				}

				foreach (ExtVehicleType vehicleType in vehicleTypesToDelete) {
#if DEBUGHK
					Log._Debug($"Deleting traffic light for {vehicleType} at segment {SegmentId}, node {nodeId}");
#endif
					CustomLights.Remove(vehicleType);
					VehicleTypes.Remove(vehicleType);
				}
			}

			if (CustomLights.ContainsKey(DEFAULT_MAIN_VEHICLETYPE) && VehicleTypes.First.Value != DEFAULT_MAIN_VEHICLETYPE) {
				VehicleTypes.Remove(DEFAULT_MAIN_VEHICLETYPE);
				VehicleTypes.AddFirst(DEFAULT_MAIN_VEHICLETYPE);
			}

			if (addPedestrianLight) {
#if DEBUGHK
				Log._Debug($"CustomSegmentLights: housekeeping @ seg. {SegmentId}, node {nodeId}: adding pedestrian light");
#endif
				if (pedestrianLightState == null)
					pedestrianLightState = pedState;
			} else {
				pedestrianLightState = null;
			}

			OnChange(calculateAutoPedLight);
#if DEBUGHK
			Log._Debug($"CustomSegmentLights: housekeeping @ seg. {SegmentId}, node {nodeId}: Housekeeping complete. VehicleTypeByLaneIndex={string.Join("; ", VehicleTypeByLaneIndex.Select(x => x == null ? "null" : x.ToString()).ToArray())} CustomLights={string.Join("; ", CustomLights.Select(x => x.Key.ToString()).ToArray())}");
#endif
		}
	}
}
