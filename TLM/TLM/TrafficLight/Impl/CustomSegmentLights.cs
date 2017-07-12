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
using TrafficManager.State;
using TrafficManager.Geometry.Impl;

namespace TrafficManager.TrafficLight.Impl {
	/// <summary>
	/// Represents the set of custom traffic lights located at a node
	/// </summary>
	public class CustomSegmentLights : SegmentEndId, ICustomSegmentLights {
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

		public bool InvalidPedestrianLight { get; set; } = false; // TODO improve & remove

		public IDictionary<ExtVehicleType, ICustomSegmentLight> CustomLights {
			get; private set;
		} = new TinyDictionary<ExtVehicleType, ICustomSegmentLight>();

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

		public RoadBaseAI.TrafficLightState AutoPedestrianLightState { get; set; } = RoadBaseAI.TrafficLightState.Green; // TODO set should be private

		public RoadBaseAI.TrafficLightState? PedestrianLightState {
			get {
				if (InvalidPedestrianLight || InternalPedestrianLightState == null)
					return RoadBaseAI.TrafficLightState.Green; // no pedestrian crossing at this point

				if (ManualPedestrianMode && InternalPedestrianLightState != null)
					return (RoadBaseAI.TrafficLightState)InternalPedestrianLightState;
				else {
					return AutoPedestrianLightState;
				}
			}
			set {
				if (InternalPedestrianLightState == null) {
#if DEBUGHK
					Log._Debug($"CustomSegmentLights: Refusing to change pedestrian light at segment {SegmentId}");
#endif
					return;
				}
				//Log._Debug($"CustomSegmentLights: Setting pedestrian light at segment {segmentId}");
				InternalPedestrianLightState = value;
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

		public RoadBaseAI.TrafficLightState? InternalPedestrianLightState { get; private set; } = null;
		private ExtVehicleType mainVehicleType = ExtVehicleType.None;
		protected ICustomSegmentLight MainSegmentLight {
			get {
				ICustomSegmentLight res = null;
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
			"\t" + $"pedestrianLightState: {InternalPedestrianLightState}\n" +
			"\t" + $"MainSegmentLight: {MainSegmentLight}\n" +
			"CustomSegmentLights]";
		}

		public bool Relocate(ushort segmentId, bool startNode, ICustomSegmentLightsManager lightsManager) {
			if (Relocate(segmentId, startNode)) {
				this.lightsManager = lightsManager;
				Housekeeping(true, true);
				return true;
			}
			return false;
		}

		[Obsolete]
		protected CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort nodeId, ushort segmentId, bool calculateAutoPedLight)
			: this(lightsManager, segmentId, nodeId == SegmentGeometry.Get(segmentId)?.StartNodeId(), calculateAutoPedLight) {

		}

		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight) : this(lightsManager, segmentId, startNode, calculateAutoPedLight, true) {
			
		}

		public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight, bool performHousekeeping) : base(segmentId, startNode) {
			this.lightsManager = lightsManager;
			if (performHousekeeping) {
				Housekeeping(false, calculateAutoPedLight);
			}
		}

		public bool IsAnyGreen() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				if (e.Value.IsAnyGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyInTransition() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				if (e.Value.IsAnyInTransition())
					return true;
			}
			return false;
		}

		public bool IsAnyLeftGreen() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				if (e.Value.IsLeftGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyMainGreen() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				if (e.Value.IsMainGreen())
					return true;
			}
			return false;
		}

		public bool IsAnyRightGreen() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				if (e.Value.IsRightGreen())
					return true;
			}
			return false;
		}

		public bool IsAllLeftRed() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				if (!e.Value.IsLeftRed())
					return false;
			}
			return true;
		}

		public bool IsAllMainRed() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				if (!e.Value.IsMainRed())
					return false;
			}
			return true;
		}

		public bool IsAllRightRed() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
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

		public ICustomSegmentLights Clone(ICustomSegmentLightsManager newLightsManager, bool performHousekeeping=true) {
			CustomSegmentLights clone = new CustomSegmentLights(newLightsManager != null ? newLightsManager : LightsManager, SegmentId, StartNode, false, false);
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				clone.CustomLights.Add(e.Key, (ICustomSegmentLight)e.Value.Clone());
			}
			clone.InternalPedestrianLightState = InternalPedestrianLightState;
			clone.manualPedestrianMode = manualPedestrianMode;
			clone.VehicleTypes = new LinkedList<ExtVehicleType>(VehicleTypes);
			clone.LastChangeFrame = LastChangeFrame;
			clone.mainVehicleType = mainVehicleType;
			clone.AutoPedestrianLightState = AutoPedestrianLightState;
			if (performHousekeeping) {
				clone.Housekeeping(false, false);
			}
			return clone;
		}

		public ICustomSegmentLight GetCustomLight(byte laneIndex) {
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
			ICustomSegmentLight light;
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

		public ICustomSegmentLight GetCustomLight(ExtVehicleType vehicleType) {
			ICustomSegmentLight ret = null;
			if (!CustomLights.TryGetValue(vehicleType, out ret)) {
				ret = MainSegmentLight;
			}

			return ret;

			/*if (vehicleType != ExtVehicleType.None)
				Log._Debug($"No traffic light for vehicle type {vehicleType} defined at segment {segmentId}, node {nodeId}.");*/
		}

		public void MakeRed() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				e.Value.MakeRed();
			}
		}

		public void MakeRedOrGreen() {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				e.Value.MakeRedOrGreen();
			}
		}

		public void SetLights(RoadBaseAI.TrafficLightState lightState) {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
				e.Value.SetStates(lightState, lightState, lightState, false);
			}
			CalculateAutoPedestrianLightState();
		}

		public void SetLights(ICustomSegmentLights otherLights) {
			foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in otherLights.CustomLights) {
				ICustomSegmentLight ourLight = null;
				if (!CustomLights.TryGetValue(e.Key, out ourLight)) {
					continue;
				}

				ourLight.SetStates(e.Value.LightMain, e.Value.LightLeft, e.Value.LightRight, false);
				//ourLight.LightPedestrian = e.Value.LightPedestrian;
			}
			InternalPedestrianLightState = otherLights.InternalPedestrianLightState;
			manualPedestrianMode = otherLights.ManualPedestrianMode;
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

		public void CalculateAutoPedestrianLightState(bool propagate=true) {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.DebugNodeId == NodeId;
#endif

#if DEBUGTTL
			if (debug)
				Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Calculating pedestrian light state of seg. {SegmentId} @ node {NodeId}");
#endif

			SegmentEndGeometry segmentEndGeometry = SegmentGeometry.Get(SegmentId)?.GetEnd(StartNode);

			if (segmentEndGeometry == null) {
				Log._Debug($"Could not get SegmentEndGeometry for segment {SegmentId} @ {NodeId}.");
				AutoPedestrianLightState = RoadBaseAI.TrafficLightState.Green;
				return;
			}

			ushort nodeId = segmentEndGeometry.NodeId();
			if (nodeId != NodeId) {
				Log.Warning($"CustomSegmentLights.CalculateAutoPedestrianLightState: Node id mismatch! segment end node is {nodeId} but we are node {NodeId}. segmentEndGeometry={segmentEndGeometry} this={this}");
				return;
			}

			if (propagate) {
				foreach (ushort otherSegmentId in segmentEndGeometry.ConnectedSegments) {
					if (otherSegmentId == 0)
						continue;

					ICustomSegmentLights otherLights = LightsManager.GetSegmentLights(nodeId, otherSegmentId);
					if (otherLights == null) {
#if DEBUGTTL
						if (debug)
							Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Expected other (propagate) CustomSegmentLights at segment {otherSegmentId} @ {NodeId} but there was none. Original segment id: {SegmentId}");
#endif
						continue;
					}

					otherLights.CalculateAutoPedestrianLightState(false);
				}
			}

			if (IsAnyGreen()) {
#if DEBUGTTL
				if (debug)
					Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Any green at seg. {SegmentId} @ {NodeId}");
#endif
				AutoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
				return;
			}

#if DEBUGTTL
			if (debug)
				Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Querying incoming segments at seg. {SegmentId} @ {NodeId}");
#endif
			RoadBaseAI.TrafficLightState autoPedestrianLightState = RoadBaseAI.TrafficLightState.Green;

			if (!segmentEndGeometry.IncomingOneWay) {
				// query straight segments
				foreach (ushort otherSegmentId in segmentEndGeometry.IncomingStraightSegments) {
					if (otherSegmentId == 0)
						continue;
#if DEBUGTTL
					if (debug)
						Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Checking incoming straight segment {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
#endif

					ICustomSegmentLights otherLights = LightsManager.GetSegmentLights(nodeId, otherSegmentId);
					if (otherLights == null) {
#if DEBUGTTL
						if (debug)
							Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Expected other (straight) CustomSegmentLights at segment {otherSegmentId} @ {NodeId} but there was none. Original segment id: {SegmentId}");
#endif
						continue;
					}

					if (!otherLights.IsAllMainRed()) {
#if DEBUGTTL
						if (debug)
							Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Not all main red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
#endif
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

#if DEBUGTTL
						if (debug)
							Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Checking left/right segment {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
#endif

						ICustomSegmentLights otherLights = LightsManager.GetSegmentLights(nodeId, otherSegmentId);
						if (otherLights == null) {
#if DEBUGTTL
							if (debug)
								Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Expected other (left/right) CustomSegmentLights at segment {otherSegmentId} @ {NodeId} but there was none. Original segment id: {SegmentId}");
#endif
							continue;
						}

						if ((lhd && !otherLights.IsAllRightRed()) || (!lhd && !otherLights.IsAllLeftRed())) {
#if DEBUGTTL
							if (debug)
								Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Not all left red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
#endif
							autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
							break;
						}
					}
				}
			}

			AutoPedestrianLightState = autoPedestrianLightState;
#if DEBUGTTL
			if (debug)
				Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Calculated AutoPedestrianLightState for segment {SegmentId} @ {NodeId}: {AutoPedestrianLightState}");
#endif
		}

		// TODO improve & remove
		public void Housekeeping(bool mayDelete, bool calculateAutoPedLight) {
			// we intentionally never delete vehicle types (because we may want to retain traffic light states if a segment is upgraded or replaced)

			ICustomSegmentLight mainLight = MainSegmentLight;
			ushort nodeId = NodeId;
			HashSet<ExtVehicleType> setupLights = new HashSet<ExtVehicleType>(); // TODO improve
			IDictionary<byte, ExtVehicleType> allAllowedTypes = Constants.ManagerFactory.VehicleRestrictionsManager.GetAllowedVehicleTypesAsDict(SegmentId, nodeId, RestrictionMode.Restricted); // TODO improve
			ExtVehicleType allAllowedMask = Constants.ManagerFactory.VehicleRestrictionsManager.GetAllowedVehicleTypes(SegmentId, nodeId, RestrictionMode.Restricted);
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

						ICustomSegmentLight segmentLight;
						if (!CustomLights.TryGetValue(mask, out segmentLight)) {
							segmentLight = new CustomSegmentLight(this, RoadBaseAI.TrafficLightState.Red);
							if (mainLight != null) {
								segmentLight.CurrentMode = mainLight.CurrentMode;
								segmentLight.SetStates(mainLight.LightMain, mainLight.LightLeft, mainLight.LightRight, false);
							}
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
				ICustomSegmentLight defaultSegmentLight;
				if (!CustomLights.TryGetValue(DEFAULT_MAIN_VEHICLETYPE, out defaultSegmentLight)) {
					defaultSegmentLight = new CustomSegmentLight(this, RoadBaseAI.TrafficLightState.Red);
					if (mainLight != null) {
						defaultSegmentLight.CurrentMode = mainLight.CurrentMode;
						defaultSegmentLight.SetStates(mainLight.LightMain, mainLight.LightLeft, mainLight.LightRight, false);
					}
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
				foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
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
				if (InternalPedestrianLightState == null) {
					InternalPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
				}
			} else {
				InternalPedestrianLightState = null;
			}

			OnChange(calculateAutoPedLight);
#if DEBUGHK
			Log._Debug($"CustomSegmentLights: housekeeping @ seg. {SegmentId}, node {nodeId}: Housekeeping complete. VehicleTypeByLaneIndex={string.Join("; ", VehicleTypeByLaneIndex.Select(x => x == null ? "null" : x.ToString()).ToArray())} CustomLights={string.Join("; ", CustomLights.Select(x => x.Key.ToString()).ToArray())}");
#endif
		}
	}
}
