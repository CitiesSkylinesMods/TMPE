using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.UI;
using TrafficManager.Util;
using static TrafficManager.State.Flags;

namespace TrafficManager.Manager {
	public class RoutingManager : AbstractSegmentGeometryObservingManager {
		public static readonly RoutingManager Instance = new RoutingManager();

		public const NetInfo.LaneType ROUTED_LANE_TYPES = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
		public const VehicleInfo.VehicleType ROUTED_VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram;
		public const VehicleInfo.VehicleType ARROW_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

		private const byte MAX_NUM_TRANSITIONS = 8;

		private static readonly Randomizer RNG = new Randomizer();
		private static readonly ushort[] POW2MASKS = new ushort[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768 };

		public enum LaneEndTransitionType {
			/// <summary>
			/// No connection
			/// </summary>
			Invalid,
			/// <summary>
			/// Lane arrow or regular lane connection
			/// </summary>
			Default,
			/// <summary>
			/// Custom lane connection
			/// </summary>
			LaneConnection,
			/// <summary>
			/// Relaxed connection for road vehicles [!] that do not have to follow lane arrows
			/// </summary>
			Relaxed
		}

		public struct SegmentRoutingData {
			public bool startNodeOutgoingOneWay;
			public bool endNodeOutgoingOneWay;
			public bool highway;

			public override string ToString() {
				return $"[SegmentRoutingData\n" +
					"\t" + $"startNodeOutgoingOneWay = {startNodeOutgoingOneWay}\n" +
					"\t" + $"endNodeOutgoingOneWay = {endNodeOutgoingOneWay}\n" +
					"\t" + $"highway = {highway}\n" +
					"SegmentRoutingData]";
			}

			public void Reset() {
				startNodeOutgoingOneWay = false;
				endNodeOutgoingOneWay = false;
				highway = false;
			}
		}

		public struct LaneEndRoutingData {
			public bool routed;

			public LaneTransitionData[] segment0Transitions;
			public LaneTransitionData[] segment1Transitions;
			public LaneTransitionData[] segment2Transitions;
			public LaneTransitionData[] segment3Transitions;
			public LaneTransitionData[] segment4Transitions;
			public LaneTransitionData[] segment5Transitions;
			public LaneTransitionData[] segment6Transitions;
			public LaneTransitionData[] segment7Transitions;

			public override string ToString() {
				return $"[LaneEndRoutingData\n" +
					"\t" + $"routed = {routed}\n" +
					"\t" + $"segment0Transitions = {(segment0Transitions == null ? "<null>" : segment0Transitions.ArrayToString())}\n" +
					"\t" + $"segment1Transitions = {(segment1Transitions == null ? "<null>" : segment1Transitions.ArrayToString())}\n" +
					"\t" + $"segment2Transitions = {(segment2Transitions == null ? "<null>" : segment2Transitions.ArrayToString())}\n" +
					"\t" + $"segment3Transitions = {(segment3Transitions == null ? "<null>" : segment3Transitions.ArrayToString())}\n" +
					"\t" + $"segment4Transitions = {(segment4Transitions == null ? "<null>" : segment4Transitions.ArrayToString())}\n" +
					"\t" + $"segment5Transitions = {(segment5Transitions == null ? "<null>" : segment5Transitions.ArrayToString())}\n" +
					"\t" + $"segment6Transitions = {(segment6Transitions == null ? "<null>" : segment6Transitions.ArrayToString())}\n" +
					"\t" + $"segment7Transitions = {(segment7Transitions == null ? "<null>" : segment7Transitions.ArrayToString())}\n" +
					"LaneEndRoutingData]";
			}

			public void Reset() {
				routed = false;
				segment0Transitions = null;
				segment1Transitions = null;
				segment2Transitions = null;
				segment3Transitions = null;
				segment4Transitions = null;
				segment5Transitions = null;
				segment6Transitions = null;
				segment7Transitions = null;
			}

			public LaneTransitionData[] GetTransitions(int index) {
				switch (index) {
					case 0:
						return segment0Transitions;
					case 1:
						return segment1Transitions;
					case 2:
						return segment2Transitions;
					case 3:
						return segment3Transitions;
					case 4:
						return segment4Transitions;
					case 5:
						return segment5Transitions;
					case 6:
						return segment6Transitions;
					case 7:
						return segment7Transitions;
				}
				return null;
			}

			public void SetTransitions(int index, LaneTransitionData[] transitions) {
				switch (index) {
					case 0:
						segment0Transitions = transitions;
						return;
					case 1:
						segment1Transitions = transitions;
						return;
					case 2:
						segment2Transitions = transitions;
						return;
					case 3:
						segment3Transitions = transitions;
						return;
					case 4:
						segment4Transitions = transitions;
						return;
					case 5:
						segment5Transitions = transitions;
						return;
					case 6:
						segment6Transitions = transitions;
						return;
					case 7:
						segment7Transitions = transitions;
						return;
				}
			}
		}

		public struct LaneTransitionData {
			public uint laneId;
			public byte laneIndex;
			public LaneEndTransitionType type;
			public byte distance;
			public ushort segmentId;

			public override string ToString() {
				return $"[LaneTransitionData\n" +
					"\t" + $"laneId = {laneId}\n" +
					"\t" + $"laneIndex = {laneIndex}\n" +
					"\t" + $"segmentId = {segmentId}\n" +
					"\t" + $"type = {type}\n" +
					"\t" + $"distance = {distance}\n" +
					"LaneTransitionData]";
			}

			public void Set(uint laneId, byte laneIndex, LaneEndTransitionType type, ushort segmentId, byte distance) {
				this.laneId = laneId;
				this.laneIndex = laneIndex;
				this.type = type;
				this.distance = distance;
				this.segmentId = segmentId;
			}

			public void Set(uint laneId, byte laneIndex, LaneEndTransitionType type, ushort segmentId) {
				Set(laneId, laneIndex, type, segmentId, 0);
			}
		}

		protected override bool AllowInvalidSegments {
			get {
				return true;
			}
		}

		/// <summary>
		/// Structs for path-finding that contain required segment-related routing data
		/// </summary>
		public SegmentRoutingData[] segmentRoutings = new SegmentRoutingData[NetManager.MAX_SEGMENT_COUNT];

		/// <summary>
		/// Structs for path-finding that contain required lane-end-related routing data.
		/// Index:
		///		[0 .. NetManager.MAX_LANE_COUNT-1]: lane ends at start node
		///		[NetManager.MAX_LANE_COUNT .. 2*NetManger.MAX_LANE_COUNT-1]: lane ends at end node
		/// </summary>
		public LaneEndRoutingData[] laneEndRoutings = new LaneEndRoutingData[(uint)NetManager.MAX_LANE_COUNT * 2u];

		protected bool segmentsUpdated = false;
		protected ulong[] updatedSegmentBuckets = new ulong[576];
		protected bool updateNotificationRequired = false;
		protected object updateLock = new object();

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			String buf = $"Segment routings:\n";
			for (int i = 0; i < segmentRoutings.Length; ++i) {
				if (! Services.NetService.IsSegmentValid((ushort)i)) {
					continue;
				}
				buf += $"Segment {i}: {segmentRoutings[i]}\n";
			}
			buf += $"Lane end routings:\n";
			for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
				if (!Services.NetService.IsLaneValid(laneId)) {
					continue;
				}
				buf += $"Lane {laneId} @ start: {laneEndRoutings[GetLaneEndRoutingIndex(laneId, true)]}\n";
				buf += $"Lane {laneId} @ end: {laneEndRoutings[GetLaneEndRoutingIndex(laneId, false)]}\n";
			}
			Log._Debug(buf);
		}

		private RoutingManager() {

		}

		public void SimulationStep() {
			if (Singleton<NetManager>.instance.m_segmentsUpdated || !segmentsUpdated) { // TODO maybe refactor NetManager use (however this could influence performance)
				return;
			}

			try {
				Monitor.Enter(updateLock);
				/*if (updateNotificationRequired) {
					UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(Translation.GetString("Please_wait") + "...", Translation.GetString("Recalculating_lane_routing") + "...", false);
				}*/ // TODO seems to crash the game for some users
				segmentsUpdated = false;

				int len = updatedSegmentBuckets.Length;
				for (int i = 0; i < len; i++) {
					ulong segMask = updatedSegmentBuckets[i];
					if (segMask != 0uL) {
						for (int m = 0; m < 64; m++) {
							if ((segMask & 1uL << m) != 0uL) {
								ushort segmentId = (ushort)(i << 6 | m);
								RecalculateSegment(segmentId);
							}
						}
						updatedSegmentBuckets[i] = 0;
					}
				}

				/*if (updateNotificationRequired) {
					UIView.library.Hide("ExceptionPanel");
				}*/
				updateNotificationRequired = false;
			} finally {
				Monitor.Exit(updateLock);
			}
		}

		public void RequestFullRecalculation(bool notify) {
			try {
				Monitor.Enter(updateLock);

				for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
					updatedSegmentBuckets[segmentId >> 6] |= 1uL << (int)segmentId;
				}
				Flags.clearHighwayLaneArrows();
				segmentsUpdated = true;
				updateNotificationRequired = notify;
			} finally {
				Monitor.Exit(updateLock);
			}

		}

		public void RequestRecalculation(ushort segmentId, bool propagate=true) {
			try {
				Monitor.Enter(updateLock);

				updatedSegmentBuckets[segmentId >> 6] |= 1uL << (int)segmentId;
				ResetIncomingHighwayLaneArrows(segmentId);
				segmentsUpdated = true;
				updateNotificationRequired = false;
			} finally {
				Monitor.Exit(updateLock);
			}

			if (propagate) {
				SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
				if (segGeo == null) {
					return;
				}

				foreach (ushort otherSegmentId in segGeo.GetConnectedSegments(true)) {
					if (otherSegmentId == 0) {
						continue;
					}

					RequestRecalculation(otherSegmentId, false);
				}

				foreach (ushort otherSegmentId in segGeo.GetConnectedSegments(false)) {
					if (otherSegmentId == 0) {
						continue;
					}

					RequestRecalculation(otherSegmentId, false);
				}
			}
		}

		protected void RecalculateAll() {
#if DEBUGROUTING
			Log._Debug($"RoutingManager.RecalculateAll: called");
#endif
			Flags.clearHighwayLaneArrows();
			for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
				try {
					RecalculateSegment(segmentId);
				} catch (Exception e) {
					Log.Error($"An error occurred while calculating routes for segment {segmentId}: {e}");
				}
			}
		}

		protected void RecalculateSegment(ushort segmentId) {
#if DEBUGROUTING
			Log._Debug($"RoutingManager.RecalculateSegment: called for seg. {segmentId}");
#endif
			if (! Services.NetService.IsSegmentValid(segmentId)) {
				return;
			}

			RecalculateSegmentRoutingData(segmentId);

			Services.NetService.IterateSegmentLanes(segmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segId, ref NetSegment segment, byte laneIndex) {
				RecalculateLaneEndRoutingData(segmentId, laneIndex, laneId, true);
				RecalculateLaneEndRoutingData(segmentId, laneIndex, laneId, false);

				return true;
			});
		}

		protected void ResetIncomingHighwayLaneArrows(ushort segmentId) {
			ushort[] nodeIds = new ushort[2];
			Services.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				nodeIds[0] = segment.m_startNode;
				nodeIds[1] = segment.m_endNode;
				return true;
			});

#if DEBUGROUTING
			Log._Debug($"RoutingManager.ResetRoutingData: Identify nodes connected to {segmentId}: nodeIds={nodeIds.ArrayToString()}");
#endif

			// reset highway lane arrows on all incoming lanes
			foreach (ushort nodeId in nodeIds) {
				if (nodeId == 0) {
					continue;
				}

				Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segId, ref NetSegment segment) {
					if (segId == segmentId) {
						return true;
					}

					Services.NetService.IterateSegmentLanes(segId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort sId, ref NetSegment seg, byte laneIndex) {
						if (IsIncomingLane(segId, seg.m_startNode == nodeId, laneIndex)) {
							Flags.removeHighwayLaneArrowFlags(laneId);
						}
						return true;
					});
					return true;
				});
			}
		}

		protected void ResetRoutingData(ushort segmentId) {
#if DEBUGROUTING
			Log._Debug($"RoutingManager.ResetRoutingData: called for segment {segmentId}");
#endif
			segmentRoutings[segmentId].Reset();

			ResetIncomingHighwayLaneArrows(segmentId);

			Services.NetService.IterateSegmentLanes(segmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segId, ref NetSegment segment, byte laneIndex) {
#if DEBUGROUTING
				Log._Debug($"RoutingManager.HandleInvalidSegment: Resetting lane {laneId}, idx {laneIndex} @ seg. {segmentId}");
#endif
				uint index = GetLaneEndRoutingIndex(laneId, true);
				laneEndRoutings[index].Reset();

				index = GetLaneEndRoutingIndex(laneId, false);
				laneEndRoutings[index].Reset();

				return true;
			});
		}

		protected void RecalculateSegmentRoutingData(ushort segmentId) {
#if DEBUGROUTING
			Log._Debug($"RoutingManager.RecalculateSegmentRoutingData: called for seg. {segmentId}");
#endif

			segmentRoutings[segmentId].Reset();

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				return;
			}

			segmentRoutings[segmentId].highway = segGeo.IsHighway();
			segmentRoutings[segmentId].startNodeOutgoingOneWay = segGeo.IsOutgoingOneWay(true);
			segmentRoutings[segmentId].endNodeOutgoingOneWay = segGeo.IsOutgoingOneWay(false);

#if DEBUGROUTING
			if (GlobalConfig.Instance.DebugSwitches[8])
				Log._Debug($"RoutingManager.RecalculateSegmentRoutingData: Calculated routing data for segment {segmentId}: {segmentRoutings[segmentId]}");
#endif
		}

		protected void RecalculateLaneEndRoutingData(ushort segmentId, int laneIndex, uint laneId, bool startNode) {
#if DEBUGROUTING
			Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: called for seg. {segmentId}, lane {laneId}, idx {laneIndex}, start {startNode}");
#endif

			uint index = GetLaneEndRoutingIndex(laneId, startNode);
			laneEndRoutings[index].Reset();

			if (! IsOutgoingLane(segmentId, startNode, laneIndex)) {
				return;
			}

			NetInfo prevSegmentInfo = null;
			bool prevSegIsInverted = false;
			ItemClass prevConnectionClass = null;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort prevSegId, ref NetSegment segment) {
				prevSegmentInfo = segment.Info;
				prevConnectionClass = prevSegmentInfo.GetConnectionClass();
				prevSegIsInverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
				return true;
			});

			bool leftHandDrive = Constants.ServiceFactory.SimulationService.LeftHandDrive;

			SegmentGeometry prevSegGeo = SegmentGeometry.Get(segmentId);
			if (prevSegGeo == null) {
				Log.Warning($"RoutingManager.RecalculateLaneEndRoutingData: prevSegGeo for segment {segmentId} is null");
				return;
			}
			SegmentEndGeometry prevEndGeo = prevSegGeo.GetEnd(startNode);
			if (prevEndGeo == null) {
				return;
			}

			ushort prevSegmentId = segmentId;
			int prevLaneIndex = laneIndex;
			uint prevLaneId = laneId;
			ushort nextNodeId = prevEndGeo.NodeId();

			NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[prevLaneIndex];
			if (! prevLaneInfo.CheckType(ROUTED_LANE_TYPES, ROUTED_VEHICLE_TYPES)) {
				return;
			}
			LaneEndRoutingData routing = new LaneEndRoutingData();
			routing.routed = true;

			int prevSimilarLaneCount = prevLaneInfo.m_similarLaneCount;
			int prevInnerSimilarLaneIndex = CalcInnerLaneSimilarIndex(prevSegmentId, prevLaneIndex);
			int prevOuterSimilarLaneIndex = CalcOuterLaneSimilarIndex(prevSegmentId, prevLaneIndex);
			bool prevHasBusLane = prevSegGeo.HasBusLane();

			bool nextIsJunction = false;
			bool nextIsEndOrOneWayOut = false;
			bool nextHasTrafficLights = false;
			Constants.ServiceFactory.NetService.ProcessNode(nextNodeId, delegate (ushort nodeId, ref NetNode node) {
				nextIsJunction = (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
				nextHasTrafficLights = (node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
				nextIsEndOrOneWayOut = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
				return true;
			});

			bool nextIsSimpleJunction = false;
			if (Options.highwayRules && !nextHasTrafficLights) {
				// determine if junction is a simple junction (highway rules only apply to simple junctions)
				nextIsSimpleJunction = NodeGeometry.Get(nextNodeId).IsSimpleJunction;
			}
			bool isNextRealJunction = prevSegGeo.CountOtherSegments(startNode) > 1;
			bool nextAreOnlyOneWayHighways = prevEndGeo.OnlyHighways;

			// determine if highway rules should be applied
			bool applyHighwayRules = Options.highwayRules && nextIsSimpleJunction && nextAreOnlyOneWayHighways && prevEndGeo.OutgoingOneWay && prevSegGeo.IsHighway();
			bool applyHighwayRulesAtJunction = applyHighwayRules && isNextRealJunction;
			bool iterateViaGeometry = applyHighwayRulesAtJunction && prevLaneInfo.CheckType(ROUTED_LANE_TYPES, ARROW_VEHICLE_TYPES);
			ushort nextSegmentId = iterateViaGeometry ? segmentId : (ushort)0; // start with u-turns at highway junctions, TODO: why?

#if DEBUGROUTING
			if (GlobalConfig.Instance.DebugSwitches[8])
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: prevSegment={segmentId}. Exploring nextSegment={nextSegmentId} -- applyHighwayRules={applyHighwayRules} applyHighwayRulesAtJunction={applyHighwayRulesAtJunction} Options.highwayRules={Options.highwayRules} nextIsSimpleJunction={nextIsSimpleJunction} nextAreOnlyOneWayHighways={nextAreOnlyOneWayHighways} prevEndGeo.OutgoingOneWay={prevEndGeo.OutgoingOneWay} prevSegGeo.IsHighway()={prevSegGeo.IsHighway()}");
#endif

			int totalIncomingLanes = 0; // running number of next incoming lanes (number is updated at each segment iteration)
			int totalOutgoingLanes = 0; // running number of next outgoing lanes (number is updated at each segment iteration)

			for (int k = 0; k < 8; ++k) {
				if (!iterateViaGeometry) {
					Constants.ServiceFactory.NetService.ProcessNode(nextNodeId, delegate (ushort nId, ref NetNode node) {
						nextSegmentId = node.GetSegment(k);
						return true;
					});

					if (nextSegmentId == 0) {
						continue;
					}
				}

				int outgoingVehicleLanes = 0;
				int incomingVehicleLanes = 0;

				bool isNextStartNodeOfNextSegment = false;
				bool nextSegIsInverted = false;
				NetInfo nextSegmentInfo = null;
				uint nextFirstLaneId = 0;
				ItemClass nextConnectionClass = null;
				Constants.ServiceFactory.NetService.ProcessSegment(nextSegmentId, delegate (ushort nextSegId, ref NetSegment segment) {
					isNextStartNodeOfNextSegment = segment.m_startNode == nextNodeId;
					/*segment.UpdateLanes(nextSegmentId, true);
					if (isNextStartNodeOfNextSegment) {
						segment.UpdateStartSegments(nextSegmentId);
					} else {
						segment.UpdateEndSegments(nextSegmentId);
					}*/
					nextSegmentInfo = segment.Info;
					nextConnectionClass = nextSegmentInfo.GetConnectionClass();
					nextSegIsInverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
					nextFirstLaneId = segment.m_lanes;
					return true;
				});
				bool nextIsHighway = SegmentGeometry.calculateIsHighway(nextSegmentInfo);
				bool nextHasBusLane = SegmentGeometry.calculateHasBusLane(nextSegmentInfo);

				if (nextConnectionClass.m_service == prevConnectionClass.m_service) {
					// determine next segment direction by evaluating the geometry information
					ArrowDirection nextIncomingDir = ArrowDirection.None;
					bool isNextValid = true;

					if (nextSegmentId != prevSegmentId) {
						for (int j = 0; j < prevEndGeo.IncomingStraightSegments.Length; ++j) {
							if (prevEndGeo.IncomingStraightSegments[j] == 0) {
								break;
							}
							if (prevEndGeo.IncomingStraightSegments[j] == nextSegmentId) {
								nextIncomingDir = ArrowDirection.Forward;
								break;
							}
						}

						if (nextIncomingDir == ArrowDirection.None) {
							for (int j = 0; j < prevEndGeo.IncomingRightSegments.Length; ++j) {
								if (prevEndGeo.IncomingRightSegments[j] == 0) {
									break;
								}
								if (prevEndGeo.IncomingRightSegments[j] == nextSegmentId) {
									nextIncomingDir = ArrowDirection.Right;
									break;
								}
							}

							if (nextIncomingDir == ArrowDirection.None) {
								for (int j = 0; j < prevEndGeo.IncomingLeftSegments.Length; ++j) {
									if (prevEndGeo.IncomingLeftSegments[j] == 0) {
										break;
									}
									if (prevEndGeo.IncomingLeftSegments[j] == nextSegmentId) {
										nextIncomingDir = ArrowDirection.Left;
										break;
									}
								}

								if (nextIncomingDir == ArrowDirection.None) {
									isNextValid = false;
								}
							}
						}
					} else {
						nextIncomingDir = ArrowDirection.Turn;
					}

#if DEBUGROUTING
					if (GlobalConfig.Instance.DebugSwitches[8])
						Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: prevSegment={segmentId}. Exploring nextSegment={nextSegmentId} -- nextFirstLaneId={nextFirstLaneId} -- nextIncomingDir={nextIncomingDir} valid={isNextValid}");
#endif


					NetInfo.Direction nextDir = isNextStartNodeOfNextSegment ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
					NetInfo.Direction nextDir2 = !nextSegIsInverted ? nextDir : NetInfo.InvertDirection(nextDir);

					LaneTransitionData[] nextRelaxedTransitionDatas = null;
					byte numNextRelaxedTransitionDatas = 0;
					LaneTransitionData[] nextCompatibleTransitionDatas = null;
					int[] nextCompatibleOuterSimilarIndices = null;
					byte numNextCompatibleTransitionDatas = 0;
					LaneTransitionData[] nextForcedTransitionDatas = null;
					byte numNextForcedTransitionDatas = 0;
					int[] nextCompatibleTransitionDataIndices = null;
					byte numNextCompatibleTransitionDataIndices = 0;

					if (isNextValid) {
						nextRelaxedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
						nextCompatibleTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
						nextForcedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
						nextCompatibleOuterSimilarIndices = new int[MAX_NUM_TRANSITIONS];
						nextCompatibleTransitionDataIndices = new int[MAX_NUM_TRANSITIONS];
					}

					uint nextLaneId = nextFirstLaneId;
					byte nextLaneIndex = 0;
					//ushort compatibleLaneIndicesMask = 0;
					bool hasLaneConnections = false; // true if any lanes are connected by the lane connection tool
													 //LaneEndTransition[] nextCompatibleIncomingTransitions = new LaneEndTransition[nextSegmentInfo.m_lanes.Length];
													 //int nextCompatibleLaneCount = 0;

					while (nextLaneIndex < nextSegmentInfo.m_lanes.Length && nextLaneId != 0u) {
						// determine valid lanes based on lane arrows
						NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];

#if DEBUGROUTING
						if (GlobalConfig.Instance.DebugSwitches[8])
							Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: prevSegment={segmentId}. Exploring nextSegment={nextSegmentId}, lane {nextLaneId}, idx {nextLaneIndex}");
#endif

						if (nextLaneInfo.CheckType(ROUTED_LANE_TYPES, ROUTED_VEHICLE_TYPES)) { // is compatible lane
							if (isNextValid && (nextLaneInfo.m_finalDirection & nextDir2) != NetInfo.Direction.None) { // is incoming lane
								++incomingVehicleLanes;

								// calculate current similar lane index starting from outer lane
								int nextOuterSimilarLaneIndex = CalcOuterLaneSimilarIndex(nextSegmentId, nextLaneIndex);
								//int nextInnerSimilarLaneIndex = CalcInnerLaneSimilarIndex(nextSegmentId, nextLaneIndex);
								bool isCompatibleLane = false;
								LaneEndTransitionType transitionType = LaneEndTransitionType.Invalid;

								// check for lane connections
								bool nextHasOutgoingConnections = false;
								int nextNumOutgoingConnections = 0;
								bool nextIsConnectedWithPrev = true;
								if (Options.laneConnectorEnabled) {
									nextNumOutgoingConnections = LaneConnectionManager.Instance.CountConnections(nextLaneId, isNextStartNodeOfNextSegment);
									nextHasOutgoingConnections = nextNumOutgoingConnections > 0;
									if (nextHasOutgoingConnections) {
										hasLaneConnections = true;
										nextIsConnectedWithPrev = LaneConnectionManager.Instance.AreLanesConnected(prevLaneId, nextLaneId, startNode);
									}
								}

								if (nextHasOutgoingConnections) {
									// check for lane connections
									if (nextIsConnectedWithPrev) {
										isCompatibleLane = true;
										transitionType = LaneEndTransitionType.LaneConnection;
									}
								} else if (nextLaneInfo.CheckType(ROUTED_LANE_TYPES, ARROW_VEHICLE_TYPES)) {
									// check for lane arrows
									bool hasLeftArrow = false;
									bool hasRightArrow = false;
									bool hasForwardArrow = false;
									if (!nextHasOutgoingConnections) {
										LaneArrows nextLaneArrows = LaneArrowManager.Instance.GetFinalLaneArrows(nextLaneId);
										hasLeftArrow = (nextLaneArrows & LaneArrows.Left) != LaneArrows.None;
										hasRightArrow = (nextLaneArrows & LaneArrows.Right) != LaneArrows.None;
										hasForwardArrow = (nextLaneArrows & LaneArrows.Forward) != LaneArrows.None || (nextLaneArrows & LaneArrows.LeftForwardRight) == LaneArrows.None;
									}

									if (applyHighwayRules || // highway rules enabled
											(nextIncomingDir == ArrowDirection.Right && hasLeftArrow) || // valid incoming right
											(nextIncomingDir == ArrowDirection.Left && hasRightArrow) || // valid incoming left
											(nextIncomingDir == ArrowDirection.Forward && hasForwardArrow) || // valid incoming straight
											(nextIncomingDir == ArrowDirection.Turn && (nextIsEndOrOneWayOut || ((leftHandDrive && hasRightArrow) || (!leftHandDrive && hasLeftArrow))))) { // valid turning lane
										isCompatibleLane = true;
										transitionType = LaneEndTransitionType.Default;
									} else {
										// lane can be used by all vehicles that may disregard lane arrows
										transitionType = LaneEndTransitionType.Relaxed;
										if (numNextRelaxedTransitionDatas < MAX_NUM_TRANSITIONS) {
											nextRelaxedTransitionDatas[numNextRelaxedTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType, nextSegmentId, GlobalConfig.Instance.IncompatibleLaneDistance);
										} else {
											Log.Warning($"nextTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
										}
									}
								} else {
									// routed vehicle that does not follow lane arrows (trains, trams, metros)
									transitionType = LaneEndTransitionType.Default;

									if (numNextForcedTransitionDatas < MAX_NUM_TRANSITIONS) {
										nextForcedTransitionDatas[numNextForcedTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType, nextSegmentId);
									} else {
										Log.Warning($"nextForcedTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
									}
								}

								if (isCompatibleLane) {
									if (numNextCompatibleTransitionDatas < MAX_NUM_TRANSITIONS) {
										nextCompatibleOuterSimilarIndices[numNextCompatibleTransitionDatas] = nextOuterSimilarLaneIndex;
										//compatibleLaneIndicesMask |= POW2MASKS[numNextCompatibleTransitionDatas];
										nextCompatibleTransitionDatas[numNextCompatibleTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType, nextSegmentId);
									} else {
										Log.Warning($"nextCompatibleTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
									}
								}
							} else if ((nextLaneInfo.m_finalDirection & NetInfo.InvertDirection(nextDir2)) != NetInfo.Direction.None) {
								++outgoingVehicleLanes;
							}
						}

						Constants.ServiceFactory.NetService.ProcessLane(nextLaneId, delegate (uint lId, ref NetLane lane) {
							nextLaneId = lane.m_nextLane;
							return true;
						});
						++nextLaneIndex;
					} // foreach lane


#if DEBUGROUTING
					if (GlobalConfig.Instance.DebugSwitches[8])
						Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: Compatible lanes: " + nextCompatibleTransitionDatas?.ArrayToString());
#endif

					if (isNextValid) {
						int nextCompatibleLaneCount = numNextCompatibleTransitionDatas;
						if (nextCompatibleLaneCount > 0) {
							// we found compatible lanes

							int[] tmp = new int[nextCompatibleLaneCount];
							Array.Copy(nextCompatibleOuterSimilarIndices, tmp, nextCompatibleLaneCount);
							nextCompatibleOuterSimilarIndices = tmp;

							int[] compatibleLaneIndicesSortedByOuterSimilarIndex = nextCompatibleOuterSimilarIndices.Select((x, i) => new KeyValuePair<int, int>(x, i)).OrderBy(p => p.Key).Select(p => p.Value).ToArray();

							// enable highway rules only at junctions or at simple lane merging/splitting points
							int laneDiff = nextCompatibleLaneCount - prevSimilarLaneCount;
							bool applyHighwayRulesAtSegment = applyHighwayRules && (applyHighwayRulesAtJunction || Math.Abs(laneDiff) == 1);

							if (!hasLaneConnections && applyHighwayRulesAtSegment) {
								// apply highway rules at transitions & junctions

								if (applyHighwayRulesAtJunction) {
									// we reached a highway junction where more than two segments are connected to each other
									int nextTransitionIndex = -1;

									int numLanesSeen = Math.Max(totalIncomingLanes, totalOutgoingLanes); // number of lanes that were processed in earlier segment iterations (either all incoming or all outgoing)
									int nextInnerSimilarIndex;

									if (totalOutgoingLanes > 0) {
										// lane splitting at junction
										nextInnerSimilarIndex = prevInnerSimilarLaneIndex + numLanesSeen;
									} else {
										// lane merging at junction
										nextInnerSimilarIndex = prevInnerSimilarLaneIndex - numLanesSeen;
									}


#if DEBUGROUTING
									if (GlobalConfig.Instance.DebugSwitches[8])
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: applying highway rules at junction: nextInnerSimilarIndex={nextInnerSimilarIndex} prevInnerSimilarLaneIndex={prevInnerSimilarLaneIndex} prevOuterSimilarLaneIndex={prevOuterSimilarLaneIndex} numLanesSeen={numLanesSeen} nextCompatibleLaneCount={nextCompatibleLaneCount}");
#endif

									if (nextInnerSimilarIndex >= 0 && nextInnerSimilarIndex < nextCompatibleLaneCount) {
										// enough lanes available
										nextTransitionIndex = FindLaneByInnerIndex(nextCompatibleTransitionDatas, numNextCompatibleTransitionDatas, nextSegmentId, nextInnerSimilarIndex);
#if DEBUGROUTING
										if (GlobalConfig.Instance.DebugSwitches[8])
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: highway rules at junction finshed (A): nextTransitionIndex={nextTransitionIndex}");
#endif
									} else {
										// Highway lanes "failed". Too few lanes at prevSegment or nextSegment.
										if (nextInnerSimilarIndex < 0) {
											// lane merging failed (too many incoming lanes)
											if (totalIncomingLanes >= prevSimilarLaneCount) {
												// there have already been explored more incoming lanes than outgoing lanes on the previous segment. Allow the current segment to also join the big merging party. What a fun!
												nextTransitionIndex = FindLaneByOuterIndex(nextCompatibleTransitionDatas, numNextCompatibleTransitionDatas, nextSegmentId, prevOuterSimilarLaneIndex);
#if DEBUGROUTING
												if (GlobalConfig.Instance.DebugSwitches[8])
													Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: highway rules at junction finshed (B): nextTransitionIndex={nextTransitionIndex}");
#endif
											}
										} else if (totalOutgoingLanes >= nextCompatibleLaneCount) {
											// there have already been explored more outgoing lanes than incoming lanes on the previous segment. Also allow vehicles to go to the current segment.
											nextTransitionIndex = FindLaneByOuterIndex(nextCompatibleTransitionDatas, numNextCompatibleTransitionDatas, nextSegmentId, 0);
#if DEBUGROUTING
											if (GlobalConfig.Instance.DebugSwitches[8])
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: highway rules at junction finshed (C): nextTransitionIndex={nextTransitionIndex}");
#endif
										}
									}

									// If nextTransitionIndex is still -1 here, then highways rules really cannot handle this situation (that's ok).

									if (nextTransitionIndex >= 0) {
										// go to matched lane

										UpdateHighwayLaneArrows(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, isNextStartNodeOfNextSegment, nextIncomingDir);

										if (numNextCompatibleTransitionDataIndices < MAX_NUM_TRANSITIONS) {
											nextCompatibleTransitionDataIndices[numNextCompatibleTransitionDataIndices++] = nextTransitionIndex;
										} else {
											Log.Warning($"nextCompatibleTransitionDataIndices overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
										}
									}
								} else {
									/* we reached a simple highway transition where lane splits or merges take place.
										this is guaranteed to be a simple lane splitting/merging point: the number of lanes is guaranteed to differ by 1
										due to:
										applyHighwayRulesAtSegment := applyHighwayRules && (applyHighwayRulesAtJunction || Math.Abs(laneDiff) == 1) [see above],
										applyHighwayRules == true,
										applyHighwayRulesAtSegment == true,
										applyHighwayRulesAtJunction == false
										=>
										true && (false || Math.Abs(laneDiff) == 1) == Math.Abs(laneDiff) == 1
									*/

									int minNextCompatibleOuterSimilarIndex = -1;
									int maxNextCompatibleOuterSimilarIndex = -1;

									if (laneDiff == 1) {
										// simple lane merge
										if (prevOuterSimilarLaneIndex == 0) {
											// merge outer lane
											minNextCompatibleOuterSimilarIndex = 0;
											maxNextCompatibleOuterSimilarIndex = 1;
										} else {
											// other lanes stay + 1
											minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = (short)(prevOuterSimilarLaneIndex + 1);
										}
									} else { // diff == -1
											 // simple lane split
										if (prevOuterSimilarLaneIndex <= 1) {
											// split outer lane
											minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = 0;
										} else {
											// other lanes stay - 1
											minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = (short)(prevOuterSimilarLaneIndex - 1);
										}
									}

									// explore lanes
									for (int nextCompatibleOuterSimilarIndex = minNextCompatibleOuterSimilarIndex; nextCompatibleOuterSimilarIndex <= maxNextCompatibleOuterSimilarIndex; ++nextCompatibleOuterSimilarIndex) {
										int nextTransitionIndex = FindLaneWithMaxOuterIndex(compatibleLaneIndicesSortedByOuterSimilarIndex, nextCompatibleOuterSimilarIndex);

										if (nextTransitionIndex < 0) {
											continue;
										}

										UpdateHighwayLaneArrows(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, isNextStartNodeOfNextSegment, nextIncomingDir);

										if (numNextCompatibleTransitionDataIndices < MAX_NUM_TRANSITIONS) {
											nextCompatibleTransitionDataIndices[numNextCompatibleTransitionDataIndices++] = nextTransitionIndex;
										} else {
											Log.Warning($"nextCompatibleTransitionDataIndices overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
										}
									}
								}
							} else {
								/*
								 * This is
								 *    1. a highway junction or lane splitting/merging point with lane connections or
								 *    2. a city or highway lane continuation point (simple transition with equal number of lanes or flagged city transition)
								 *    3. a city junction
								 *  with multiple or a single target lane: Perform lane matching
								 */

								// min/max compatible outer similar lane indices
								int minNextCompatibleOuterSimilarIndex = -1;
								int maxNextCompatibleOuterSimilarIndex = -1;
								if (nextIncomingDir == ArrowDirection.Turn) {
									// force u-turns to happen on the innermost lane
									minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
								} else if (isNextRealJunction) {
									// at junctions: try to match distinct lanes
									if (nextCompatibleLaneCount > prevSimilarLaneCount && prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
										// merge inner lanes
										minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
										maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
									} else {
										// 1-to-n (split inner lane) or 1-to-1 (direct lane matching)
										minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
										maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
									}

									bool mayChangeLanes = nextIncomingDir == ArrowDirection.Forward && Flags.getStraightLaneChangingAllowed(nextSegmentId, isNextStartNodeOfNextSegment);

									if (!mayChangeLanes) {
										if (nextHasBusLane && !prevHasBusLane) {
											// allow vehicles on the bus lane AND on the next lane to merge on this lane
											maxNextCompatibleOuterSimilarIndex = Math.Min(nextCompatibleLaneCount - 1, maxNextCompatibleOuterSimilarIndex + 1);
										} else if (!nextHasBusLane && prevHasBusLane) {
											// allow vehicles to enter the bus lane
											minNextCompatibleOuterSimilarIndex = Math.Max(0, minNextCompatibleOuterSimilarIndex - 1);
										}
									} else {
										// vehicles may change lanes when going straight
										minNextCompatibleOuterSimilarIndex = Math.Max(0, minNextCompatibleOuterSimilarIndex - 1);
										maxNextCompatibleOuterSimilarIndex = Math.Min(nextCompatibleLaneCount - 1, maxNextCompatibleOuterSimilarIndex + 1);
									}
								} else {
									// lane continuation point: lane merging/splitting

									bool sym1 = (prevSimilarLaneCount & 1) == 0; // mod 2 == 0
									bool sym2 = (nextCompatibleLaneCount & 1) == 0; // mod 2 == 0
									if (prevSimilarLaneCount < nextCompatibleLaneCount) {
										// lane merging
										if (sym1 == sym2) {
											// merge outer lanes
											int a = (nextCompatibleLaneCount - prevSimilarLaneCount) >> 1; // nextCompatibleLaneCount - prevSimilarLaneCount is always > 0
											if (prevSimilarLaneCount == 1) {
												minNextCompatibleOuterSimilarIndex = 0;
												maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1; // always >=0
											} else if (prevOuterSimilarLaneIndex == 0) {
												minNextCompatibleOuterSimilarIndex = 0;
												maxNextCompatibleOuterSimilarIndex = a;
											} else if (prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
												minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
												maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1; // always >=0
											} else {
												minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
											}
										} else {
											// criss-cross merge
											int a = (nextCompatibleLaneCount - prevSimilarLaneCount - 1) >> 1; // nextCompatibleLaneCount - prevSimilarLaneCount - 1 is always >= 0
											int b = (nextCompatibleLaneCount - prevSimilarLaneCount + 1) >> 1; // nextCompatibleLaneCount - prevSimilarLaneCount + 1 is always >= 2
											if (prevSimilarLaneCount == 1) {
												minNextCompatibleOuterSimilarIndex = 0;
												maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1; // always >=0
											} else if (prevOuterSimilarLaneIndex == 0) {
												minNextCompatibleOuterSimilarIndex = 0;
												maxNextCompatibleOuterSimilarIndex = b;
											} else if (prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
												minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
												maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1; // always >=0
											} else if (RNG.Int32(0, 1) == 0) {
												minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
											} else {
												minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + b;
											}
										}
									} else if (prevSimilarLaneCount == nextCompatibleLaneCount) {
										minNextCompatibleOuterSimilarIndex = 0;
										maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
										//minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
									} else {
										// at lane splits: distribute traffic evenly (1-to-n, n-to-n)										
										// prevOuterSimilarIndex is always > nextCompatibleLaneCount
										if (sym1 == sym2) {
											// split outer lanes
											int a = (prevSimilarLaneCount - nextCompatibleLaneCount) >> 1; // prevSimilarLaneCount - nextCompatibleLaneCount is always > 0
											minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex - a; // a is always <= prevSimilarLaneCount
										} else {
											// split outer lanes, criss-cross inner lanes 
											int a = (prevSimilarLaneCount - nextCompatibleLaneCount - 1) >> 1; // prevSimilarLaneCount - nextCompatibleLaneCount - 1 is always >= 0

											minNextCompatibleOuterSimilarIndex = (a - 1 >= prevOuterSimilarLaneIndex) ? 0 : prevOuterSimilarLaneIndex - a - 1;
											maxNextCompatibleOuterSimilarIndex = (a >= prevOuterSimilarLaneIndex) ? 0 : prevOuterSimilarLaneIndex - a;
										}
									}

									minNextCompatibleOuterSimilarIndex = Math.Max(0, Math.Min(minNextCompatibleOuterSimilarIndex, nextCompatibleLaneCount - 1));
									maxNextCompatibleOuterSimilarIndex = Math.Max(0, Math.Min(maxNextCompatibleOuterSimilarIndex, nextCompatibleLaneCount - 1));

									if (minNextCompatibleOuterSimilarIndex > maxNextCompatibleOuterSimilarIndex) {
										minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex;
									}
								}

								// find best matching lane(s)
								int minIndex = minNextCompatibleOuterSimilarIndex;
								int maxIndex = maxNextCompatibleOuterSimilarIndex;
								if (hasLaneConnections) {
									minIndex = 0;
									maxIndex = nextCompatibleLaneCount - 1;
								}

								for (int nextCompatibleOuterSimilarIndex = minIndex; nextCompatibleOuterSimilarIndex <= maxIndex; ++nextCompatibleOuterSimilarIndex) {
									int nextTransitionIndex = FindLaneWithMaxOuterIndex(compatibleLaneIndicesSortedByOuterSimilarIndex, nextCompatibleOuterSimilarIndex);

									if (nextTransitionIndex < 0) {
										continue;
									}

									if (hasLaneConnections) {
										int nextNumConnections = LaneConnectionManager.Instance.CountConnections(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, isNextStartNodeOfNextSegment);
										bool nextIsConnectedWithPrev = LaneConnectionManager.Instance.AreLanesConnected(prevLaneId, nextCompatibleTransitionDatas[nextTransitionIndex].laneId, startNode);
										if (nextCompatibleOuterSimilarIndex < minNextCompatibleOuterSimilarIndex || nextCompatibleOuterSimilarIndex > maxNextCompatibleOuterSimilarIndex) {
											if (nextNumConnections == 0 || !nextIsConnectedWithPrev) {
												continue; // disregard lane since it is not connected to previous lane
											}
										} else {
											if (nextNumConnections != 0 && !nextIsConnectedWithPrev) {
												continue; // disregard lane since it is not connected to previous lane but has outgoing connections
											}
										}
									}

									byte compatibleLaneDist = 0;
									if (nextIncomingDir == ArrowDirection.Turn) {
										compatibleLaneDist = (byte)GlobalConfig.Instance.UturnLaneDistance;
									} else if (!hasLaneConnections && !isNextRealJunction) {
										//if ((compatibleLaneIndicesMask & POW2MASKS[nextTransitionIndex]) != 0) { // TODO this is always true since we are iterating over compatible lanes only
										if (nextCompatibleLaneCount == prevSimilarLaneCount) {
											int relLaneDist = nextCompatibleOuterSimilarIndices[nextTransitionIndex] - prevOuterSimilarLaneIndex; // relative lane distance (positive: change to more outer lane, negative: change to more inner lane)
											compatibleLaneDist = (byte)Math.Abs(relLaneDist);
										}
										//} else {
										//	compatibleLaneDist = GlobalConfig.Instance.IncompatibleLaneDistance;
										//}
									}

									nextCompatibleTransitionDatas[nextTransitionIndex].distance = compatibleLaneDist;
									if (numNextCompatibleTransitionDataIndices < MAX_NUM_TRANSITIONS) {
										nextCompatibleTransitionDataIndices[numNextCompatibleTransitionDataIndices++] = nextTransitionIndex;
									} else {
										Log.Warning($"nextCompatibleTransitionDataIndices overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
									}
								} // foreach lane
							} // highway/city rules if/else
						} // compatible lanes found

						// build final array
						LaneTransitionData[] nextTransitionDatas = new LaneTransitionData[numNextRelaxedTransitionDatas + numNextCompatibleTransitionDataIndices + numNextForcedTransitionDatas];
						int j = 0;
						for (int i = 0; i < numNextCompatibleTransitionDataIndices; ++i) {
							nextTransitionDatas[j++] = nextCompatibleTransitionDatas[nextCompatibleTransitionDataIndices[i]];
						}

						for (int i = 0; i < numNextRelaxedTransitionDatas; ++i) {
							nextTransitionDatas[j++] = nextRelaxedTransitionDatas[i];
						}

						for (int i = 0; i < numNextForcedTransitionDatas; ++i) {
							nextTransitionDatas[j++] = nextForcedTransitionDatas[i];
						}

						routing.SetTransitions(k, nextTransitionDatas);
					} // valid segment

					if (nextSegmentId != prevSegmentId) {
						totalIncomingLanes += incomingVehicleLanes;
						totalOutgoingLanes += outgoingVehicleLanes;
					}
				} // compatible connection class

				if (iterateViaGeometry) {
					Constants.ServiceFactory.NetService.ProcessSegment(nextSegmentId, delegate (ushort nextSegId, ref NetSegment segment) {
						if (Constants.ServiceFactory.SimulationService.LeftHandDrive) {
							nextSegmentId = segment.GetLeftSegment(nextNodeId);
						} else {
							nextSegmentId = segment.GetRightSegment(nextNodeId);
						}
						return true;
					});

					if (nextSegmentId == prevSegmentId || nextSegmentId == 0) {
						// we reached the first segment again
						break;
					}
				}
			} // foreach segment

			laneEndRoutings[index] = routing;

#if DEBUGROUTING
			if (GlobalConfig.Instance.DebugSwitches[8])
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: Calculated routing data for lane {laneId}, idx {laneIndex}, seg. {segmentId}, start {startNode}, array index {index}: {laneEndRoutings[index]}");
#endif
		}

		private void UpdateHighwayLaneArrows(uint laneId, bool startNode, ArrowDirection dir) {
			bool nextHasLaneConnections = LaneConnectionManager.Instance.CountConnections(laneId, startNode) > 0;

			// update highway mode lane arrows
			/*if (nextHasLaneConnections) {
#if DEBUGROUTING
				if (GlobalConfig.Instance.DebugSwitches[8])
					Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: highway rules -- next lane {nextCompatibleTransitionDatas[nextTransitionIndex].laneId} has lane connections. Removing highway lane arrows");
#endif
				Flags.removeHighwayLaneArrowFlags(nextCompatibleTransitionDatas[nextTransitionIndex].laneId);
			} else*/
			if (!nextHasLaneConnections) {
				Flags.LaneArrows? prevHighwayArrows = Flags.getHighwayLaneArrowFlags(laneId);
				Flags.LaneArrows newHighwayArrows = Flags.LaneArrows.None;
				if (prevHighwayArrows != null)
					newHighwayArrows = (Flags.LaneArrows)prevHighwayArrows;
				if (dir == ArrowDirection.Right)
					newHighwayArrows |= Flags.LaneArrows.Left;
				else if (dir == ArrowDirection.Left)
					newHighwayArrows |= Flags.LaneArrows.Right;
				else if (dir == ArrowDirection.Forward)
					newHighwayArrows |= Flags.LaneArrows.Forward;

#if DEBUGROUTING
				if (GlobalConfig.Instance.DebugSwitches[8])
					Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: highway rules -- next lane {laneId} obeys highway rules. Setting highway lane arrows to {newHighwayArrows}. prevHighwayArrows={prevHighwayArrows}");
#endif

				if (newHighwayArrows != prevHighwayArrows && newHighwayArrows != Flags.LaneArrows.None) {
					Flags.setHighwayLaneArrowFlags(laneId, newHighwayArrows, false);
				}
			}
		}

		/*private int GetSegmentNodeIndex(ushort nodeId, ushort segmentId) {
			int i = -1;
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segId, ref NetSegment segment) {
				++i;
				if (segId == segmentId) {
					return false;
				}
				return true;
			});
			return i;
		}*/

		internal uint GetLaneEndRoutingIndex(uint laneId, bool startNode) {
			return (uint)(laneId + (startNode ? 0u : (uint)NetManager.MAX_LANE_COUNT));
		}

		public int CalcInnerLaneSimilarIndex(ushort segmentId, int laneIndex) {
			int ret = -1;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				NetInfo.Lane laneInfo = segment.Info.m_lanes[laneIndex];
				// note: m_direction is correct here
				ret = (byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0 ? laneInfo.m_similarLaneIndex : laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1;
				return true;
			});

			return ret;
		}

		public int CalcOuterLaneSimilarIndex(ushort segmentId, int laneIndex) {
			int ret = -1;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				NetInfo.Lane laneInfo = segment.Info.m_lanes[laneIndex];
				// note: m_direction is correct here
				ret = (byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0 ? laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1 : laneInfo.m_similarLaneIndex;
				return true;
			});

			return ret;
		}

		protected int FindLaneWithMaxOuterIndex(int[] indicesSortedByOuterIndex, int targetOuterLaneIndex) {
			return indicesSortedByOuterIndex[Math.Max(0, Math.Min(targetOuterLaneIndex, indicesSortedByOuterIndex.Length - 1))];
		}

		protected int FindLaneByOuterIndex(LaneTransitionData[] laneTransitions, int num, ushort segmentId, int targetOuterLaneIndex) {
			for (int i = 0; i < num; ++i) {
				int outerIndex = CalcOuterLaneSimilarIndex(segmentId, laneTransitions[i].laneIndex);
				if (outerIndex == targetOuterLaneIndex) {
					return i;
				}
			}
			return -1;
		}

		protected int FindLaneByInnerIndex(LaneTransitionData[] laneTransitions, int num, ushort segmentId, int targetInnerLaneIndex) {
			for (int i = 0; i < num; ++i) {
				int innerIndex = CalcInnerLaneSimilarIndex(segmentId, laneTransitions[i].laneIndex);
				if (innerIndex == targetInnerLaneIndex) {
					return i;
				}
			}
			return -1;
		}

		protected bool IsOutgoingLane(ushort segmentId, bool startNode, int laneIndex) {
			return IsIncomingOutgoingLane(segmentId, startNode, laneIndex, false);
		}

		protected bool IsIncomingLane(ushort segmentId, bool startNode, int laneIndex) {
			return IsIncomingOutgoingLane(segmentId, startNode, laneIndex, true);
		}

		protected bool IsIncomingOutgoingLane(ushort segmentId, bool startNode, int laneIndex, bool incoming) {
			bool segIsInverted = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				segIsInverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
				return true;
			});

			NetInfo.Direction dir = startNode ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
			dir = incoming ^ segIsInverted ? NetInfo.InvertDirection(dir) : dir;

			NetInfo.Direction finalDir = NetInfo.Direction.None;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				finalDir = segment.Info.m_lanes[laneIndex].m_finalDirection;
				return true;
			});

			return (finalDir & dir) != NetInfo.Direction.None;
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			Flags.removeHighwayLaneArrowFlagsAtSegment(geometry.SegmentId);
			ResetRoutingData(geometry.SegmentId);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			ResetRoutingData(geometry.SegmentId);
			RequestRecalculation(geometry.SegmentId);
		}

		public override void OnAfterLoadData() {
			base.OnAfterLoadData();

			RecalculateAll();
			for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
				SubscribeToSegmentGeometry(segmentId);
			}
		}
	}
}
