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
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.UI;
using TrafficManager.Util;
using static TrafficManager.State.Flags;

namespace TrafficManager.Manager.Impl {
	public class RoutingManager : AbstractSegmentGeometryObservingManager, IRoutingManager {
		public static readonly RoutingManager Instance = new RoutingManager();

		public const NetInfo.LaneType ROUTED_LANE_TYPES = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
		public const VehicleInfo.VehicleType ROUTED_VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Monorail;
		public const VehicleInfo.VehicleType ARROW_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

		private const byte MAX_NUM_TRANSITIONS = 64;

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
		/// Structs for path-finding that contain required lane-end-related backward routing data.
		/// Index:
		///		[0 .. NetManager.MAX_LANE_COUNT-1]: lane ends at start node
		///		[NetManager.MAX_LANE_COUNT .. 2*NetManger.MAX_LANE_COUNT-1]: lane ends at end node
		/// </summary>
		public LaneEndRoutingData[] laneEndBackwardRoutings = new LaneEndRoutingData[(uint)NetManager.MAX_LANE_COUNT * 2u];

		/// <summary>
		/// Structs for path-finding that contain required lane-end-related forward routing data.
		/// Index:
		///		[0 .. NetManager.MAX_LANE_COUNT-1]: lane ends at start node
		///		[NetManager.MAX_LANE_COUNT .. 2*NetManger.MAX_LANE_COUNT-1]: lane ends at end node
		/// </summary>
		public LaneEndRoutingData[] laneEndForwardRoutings = new LaneEndRoutingData[(uint)NetManager.MAX_LANE_COUNT * 2u];

		protected bool segmentsUpdated = false;
		protected ulong[] updatedSegmentBuckets = new ulong[576];
		protected bool updateNotificationRequired = false;
		protected object updateLock = new object();

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			String buf = $"Segment routings:\n";
			for (int i = 0; i < segmentRoutings.Length; ++i) {
				if (!Services.NetService.IsSegmentValid((ushort)i)) {
					continue;
				}
				buf += $"Segment {i}: {segmentRoutings[i]}\n";
			}
			buf += $"\nLane end backward routings:\n";
			for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
				if (!Services.NetService.IsLaneValid(laneId)) {
					continue;
				}
				buf += $"Lane {laneId} @ start: {laneEndBackwardRoutings[GetLaneEndRoutingIndex(laneId, true)]}\n";
				buf += $"Lane {laneId} @ end: {laneEndBackwardRoutings[GetLaneEndRoutingIndex(laneId, false)]}\n";
			}
			buf += $"\nLane end forward routings:\n";
			for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
				if (!Services.NetService.IsLaneValid(laneId)) {
					continue;
				}
				buf += $"Lane {laneId} @ start: {laneEndForwardRoutings[GetLaneEndRoutingIndex(laneId, true)]}\n";
				buf += $"Lane {laneId} @ end: {laneEndForwardRoutings[GetLaneEndRoutingIndex(laneId, false)]}\n";
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

				for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
					updatedSegmentBuckets[segmentId >> 6] |= 1uL << (int)segmentId;
				}
				Flags.clearHighwayLaneArrows();
				segmentsUpdated = true;
				updateNotificationRequired = notify;
			} finally {
				Monitor.Exit(updateLock);
			}

		}

		public void RequestRecalculation(ushort segmentId, bool propagate = true) {
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
			for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
				try {
					RecalculateSegment((ushort)segmentId);
				} catch (Exception e) {
					Log.Error($"An error occurred while calculating routes for segment {segmentId}: {e}");
				}
			}
		}

		protected void RecalculateSegment(ushort segmentId) {
#if DEBUGROUTING
			bool debug = GlobalConfig.Instance.Debug.Switches[8] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || GlobalConfig.Instance.Debug.SegmentId == segmentId);
			if (debug)
				Log._Debug($"RoutingManager.RecalculateSegment: called for seg. {segmentId}");
#endif
			if (!Services.NetService.IsSegmentValid(segmentId)) {
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
			bool debug = GlobalConfig.Instance.Debug.Switches[8] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || GlobalConfig.Instance.Debug.SegmentId == segmentId);
			if (debug)
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
			bool debug = GlobalConfig.Instance.Debug.Switches[8] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || GlobalConfig.Instance.Debug.SegmentId == segmentId);
			if (debug)
				Log._Debug($"RoutingManager.ResetRoutingData: called for segment {segmentId}");
#endif
			segmentRoutings[segmentId].Reset();

			ResetIncomingHighwayLaneArrows(segmentId);

			Services.NetService.IterateSegmentLanes(segmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segId, ref NetSegment segment, byte laneIndex) {
#if DEBUGROUTING
				if (debug)
					Log._Debug($"RoutingManager.HandleInvalidSegment: Resetting lane {laneId}, idx {laneIndex} @ seg. {segmentId}");
#endif
				ResetLaneRoutings(laneId, true);
				ResetLaneRoutings(laneId, false);

				return true;
			});
		}

		protected void RecalculateSegmentRoutingData(ushort segmentId) {
#if DEBUGROUTING
			bool debug = GlobalConfig.Instance.Debug.Switches[8] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || GlobalConfig.Instance.Debug.SegmentId == segmentId);
			if (debug)
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
			if (debug)
				Log._Debug($"RoutingManager.RecalculateSegmentRoutingData: Calculated routing data for segment {segmentId}: {segmentRoutings[segmentId]}");
#endif
		}

		protected void RecalculateLaneEndRoutingData(ushort segmentId, int laneIndex, uint laneId, bool startNode) {
#if DEBUGROUTING
			bool debug = GlobalConfig.Instance.Debug.Switches[8] && (GlobalConfig.Instance.Debug.SegmentId <= 0 || GlobalConfig.Instance.Debug.SegmentId == segmentId);
			if (debug)
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}) called");
#endif

			ResetLaneRoutings(laneId, startNode);

			if (!IsOutgoingLane(segmentId, startNode, laneIndex)) {
				return;
			}

			NetInfo prevSegmentInfo = null;
			bool prevSegIsInverted = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort prevSegId, ref NetSegment segment) {
				prevSegmentInfo = segment.Info;
				prevSegIsInverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
				return true;
			});

			bool leftHandDrive = Constants.ServiceFactory.SimulationService.LeftHandDrive;

			SegmentGeometry prevSegGeo = SegmentGeometry.Get(segmentId);
			if (prevSegGeo == null) {
				//Log.Warning($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevSegGeo for segment {segmentId} is null");
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
			if (!prevLaneInfo.CheckType(ROUTED_LANE_TYPES, ROUTED_VEHICLE_TYPES)) {
				return;
			}
			LaneEndRoutingData backwardRouting = new LaneEndRoutingData();
			backwardRouting.routed = true;

			int prevSimilarLaneCount = prevLaneInfo.m_similarLaneCount;
			int prevInnerSimilarLaneIndex = CalcInnerSimilarLaneIndex(prevSegmentId, prevLaneIndex);
			int prevOuterSimilarLaneIndex = CalcOuterSimilarLaneIndex(prevSegmentId, prevLaneIndex);
			bool prevHasBusLane = prevSegGeo.HasBusLane();

			bool nextIsJunction = false;
			bool nextIsTransition = false;
			bool nextIsEndOrOneWayOut = false;
			bool nextHasTrafficLights = false;
			Constants.ServiceFactory.NetService.ProcessNode(nextNodeId, delegate (ushort nodeId, ref NetNode node) {
				nextIsJunction = (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
				nextIsTransition = (node.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
				nextHasTrafficLights = (node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
				nextIsEndOrOneWayOut = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
				return true;
			});

			bool nextIsSimpleJunction = false;
			bool nextIsSplitJunction = false;
			if (Options.highwayRules && !nextHasTrafficLights) {
				// determine if junction is a simple junction (highway rules only apply to simple junctions)
				NodeGeometry nodeGeo = NodeGeometry.Get(nextNodeId);
				nextIsSimpleJunction = nodeGeo.IsSimpleJunction;
				nextIsSplitJunction = nodeGeo.OutgoingSegments > 1;
			}
			bool isNextRealJunction = prevSegGeo.CountOtherSegments(startNode) > 1;
			bool nextAreOnlyOneWayHighways = prevEndGeo.OnlyHighways;

			// determine if highway rules should be applied
			bool onHighway = Options.highwayRules && nextAreOnlyOneWayHighways && prevEndGeo.OutgoingOneWay && prevSegGeo.IsHighway();
			bool applyHighwayRules = onHighway && nextIsSimpleJunction;
			bool applyHighwayRulesAtJunction = applyHighwayRules && isNextRealJunction;
			bool iterateViaGeometry = applyHighwayRulesAtJunction && prevLaneInfo.CheckType(ROUTED_LANE_TYPES, ARROW_VEHICLE_TYPES);
			ushort nextSegmentId = iterateViaGeometry ? segmentId : (ushort)0; // start with u-turns at highway junctions

#if DEBUGROUTING
			if (debug) {
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevSegment={segmentId}. Starting exploration with nextSegment={nextSegmentId} @ nextNodeId={nextNodeId} -- onHighway={onHighway} applyHighwayRules={applyHighwayRules} applyHighwayRulesAtJunction={applyHighwayRulesAtJunction} Options.highwayRules={Options.highwayRules} nextIsSimpleJunction={nextIsSimpleJunction} nextAreOnlyOneWayHighways={nextAreOnlyOneWayHighways} prevEndGeo.OutgoingOneWay={prevEndGeo.OutgoingOneWay} prevSegGeo.IsHighway()={prevSegGeo.IsHighway()} iterateViaGeometry={iterateViaGeometry}");
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevSegIsInverted={prevSegIsInverted} leftHandDrive={leftHandDrive}");
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevSimilarLaneCount={prevSimilarLaneCount} prevInnerSimilarLaneIndex={prevInnerSimilarLaneIndex} prevOuterSimilarLaneIndex={prevOuterSimilarLaneIndex} prevHasBusLane={prevHasBusLane}");
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): nextIsJunction={nextIsJunction} nextIsEndOrOneWayOut={nextIsEndOrOneWayOut} nextHasTrafficLights={nextHasTrafficLights} nextIsSimpleJunction={nextIsSimpleJunction} nextIsSplitJunction={nextIsSplitJunction} isNextRealJunction={isNextRealJunction}");
			}
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
				Constants.ServiceFactory.NetService.ProcessSegment(nextSegmentId, delegate (ushort nextSegId, ref NetSegment segment) {
					isNextStartNodeOfNextSegment = segment.m_startNode == nextNodeId;
					/*segment.UpdateLanes(nextSegmentId, true);
					if (isNextStartNodeOfNextSegment) {
						segment.UpdateStartSegments(nextSegmentId);
					} else {
						segment.UpdateEndSegments(nextSegmentId);
					}*/
					nextSegmentInfo = segment.Info;
					nextSegIsInverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
					nextFirstLaneId = segment.m_lanes;
					return true;
				});
				bool nextIsHighway = SegmentGeometry.calculateIsHighway(nextSegmentInfo);
				bool nextHasBusLane = SegmentGeometry.calculateHasBusLane(nextSegmentInfo);

#if DEBUGROUTING
				if (debug) {
					Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): Exploring nextSegmentId={nextSegmentId}");
					Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): isNextStartNodeOfNextSegment={isNextStartNodeOfNextSegment} nextSegIsInverted={nextSegIsInverted} nextFirstLaneId={nextFirstLaneId} nextIsHighway={nextIsHighway} nextHasBusLane={nextHasBusLane} totalOutgoingLanes={totalOutgoingLanes} totalIncomingLanes={totalIncomingLanes}");
				}
#endif

				// determine next segment direction by evaluating the geometry information
				ArrowDirection nextIncomingDir = ArrowDirection.None;
				bool isNextSegmentValid = true;

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
								isNextSegmentValid = false;
							}
						}
					}
				} else {
					nextIncomingDir = ArrowDirection.Turn;
				}

#if DEBUGROUTING
				if (debug)
					Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevSegment={segmentId}. Exploring nextSegment={nextSegmentId} -- nextFirstLaneId={nextFirstLaneId} -- nextIncomingDir={nextIncomingDir} valid={isNextSegmentValid}");
#endif


				NetInfo.Direction nextDir = isNextStartNodeOfNextSegment ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				NetInfo.Direction nextDir2 = !nextSegIsInverted ? nextDir : NetInfo.InvertDirection(nextDir);

				LaneTransitionData[] nextRelaxedTransitionDatas = null;
				byte numNextRelaxedTransitionDatas = 0;
				LaneTransitionData[] nextCompatibleTransitionDatas = null;
				int[] nextCompatibleOuterSimilarIndices = null;
				byte numNextCompatibleTransitionDatas = 0;
				LaneTransitionData[] nextLaneConnectionTransitionDatas = null;
				byte numNextLaneConnectionTransitionDatas = 0;
				LaneTransitionData[] nextForcedTransitionDatas = null;
				byte numNextForcedTransitionDatas = 0;
				int[] nextCompatibleTransitionDataIndices = null;
				byte numNextCompatibleTransitionDataIndices = 0;
				int[] compatibleLaneIndexToLaneConnectionIndex = null;

				if (isNextSegmentValid) {
					nextRelaxedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
					nextCompatibleTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
					nextLaneConnectionTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
					nextForcedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
					nextCompatibleOuterSimilarIndices = new int[MAX_NUM_TRANSITIONS];
					nextCompatibleTransitionDataIndices = new int[MAX_NUM_TRANSITIONS];
					compatibleLaneIndexToLaneConnectionIndex = new int[MAX_NUM_TRANSITIONS];
				}


				uint nextLaneId = nextFirstLaneId;
				byte nextLaneIndex = 0;
				//ushort compatibleLaneIndicesMask = 0;

				while (nextLaneIndex < nextSegmentInfo.m_lanes.Length && nextLaneId != 0u) {
					// determine valid lanes based on lane arrows
					NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];

#if DEBUGROUTING
					if (debug)
						Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevSegment={segmentId}. Exploring nextSegment={nextSegmentId}, lane {nextLaneId}, idx {nextLaneIndex}");
#endif

					if (nextLaneInfo.CheckType(ROUTED_LANE_TYPES, ROUTED_VEHICLE_TYPES) &&
						(prevLaneInfo.m_vehicleType & nextLaneInfo.m_vehicleType) != VehicleInfo.VehicleType.None
						/*(nextLaneInfo.m_vehicleType & prevLaneInfo.m_vehicleType) != VehicleInfo.VehicleType.None &&
						(nextLaneInfo.m_laneType & prevLaneInfo.m_laneType) != NetInfo.LaneType.None*/) { // next is compatible lane
#if DEBUGROUTING
						if (debug)
							Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): vehicle type check passed for nextLaneId={nextLaneId}, idx={nextLaneIndex}");
#endif
						if ((nextLaneInfo.m_finalDirection & nextDir2) != NetInfo.Direction.None) { // next is incoming lane
#if DEBUGROUTING
							if (debug)
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): lane direction check passed for nextLaneId={nextLaneId}, idx={nextLaneIndex}");
#endif
							++incomingVehicleLanes;

#if DEBUGROUTING
							if (debug)
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): increasing number of incoming lanes at nextLaneId={nextLaneId}, idx={nextLaneIndex}: isNextValid={isNextSegmentValid}, nextLaneInfo.m_finalDirection={nextLaneInfo.m_finalDirection}, nextDir2={nextDir2}: incomingVehicleLanes={incomingVehicleLanes}, outgoingVehicleLanes={outgoingVehicleLanes} ");
#endif

							if (isNextSegmentValid) {
								// calculate current similar lane index starting from outer lane
								int nextOuterSimilarLaneIndex = CalcOuterSimilarLaneIndex(nextSegmentId, nextLaneIndex);
								//int nextInnerSimilarLaneIndex = CalcInnerSimilarLaneIndex(nextSegmentId, nextLaneIndex);
								bool isCompatibleLane = false;
								LaneEndTransitionType transitionType = LaneEndTransitionType.Invalid;

								// check for lane connections
								bool nextHasOutgoingConnections = LaneConnectionManager.Instance.HasConnections(nextLaneId, isNextStartNodeOfNextSegment);
								bool nextIsConnectedWithPrev = true;
								if (nextHasOutgoingConnections) {
									nextIsConnectedWithPrev = LaneConnectionManager.Instance.AreLanesConnected(prevLaneId, nextLaneId, startNode);
								}

#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): connection information for nextLaneId={nextLaneId}, idx={nextLaneIndex}: nextOuterSimilarLaneIndex={nextOuterSimilarLaneIndex}, nextHasOutgoingConnections={nextHasOutgoingConnections}, nextIsConnectedWithPrev={nextIsConnectedWithPrev}");
#endif

								int currentLaneConnectionTransIndex = -1;
								if (nextHasOutgoingConnections) {
									// check for lane connections
									if (nextIsConnectedWithPrev) {
										// lane is connected with previous lane
										if (numNextLaneConnectionTransitionDatas < MAX_NUM_TRANSITIONS) {
											currentLaneConnectionTransIndex = numNextLaneConnectionTransitionDatas;
											nextLaneConnectionTransitionDatas[numNextLaneConnectionTransitionDatas++].Set(nextLaneId, nextLaneIndex, LaneEndTransitionType.LaneConnection, nextSegmentId, isNextStartNodeOfNextSegment);
										} else {
											Log.Warning($"nextTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
										}
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): nextLaneId={nextLaneId}, idx={nextLaneIndex} has outgoing connections and is connected with previous lane. adding as lane connection lane.");
#endif
									} else {
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): nextLaneId={nextLaneId}, idx={nextLaneIndex} has outgoing connections but is NOT connected with previous lane");
#endif
									}
								}

								if (!nextIsJunction) {
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): nextLaneId={nextLaneId}, idx={nextLaneIndex} is not a junction. adding as Default.");
#endif
									isCompatibleLane = true;
									transitionType = LaneEndTransitionType.Default;
								} else if (nextLaneInfo.CheckType(ROUTED_LANE_TYPES, ARROW_VEHICLE_TYPES)) {
									// check for lane arrows
									LaneArrows nextLaneArrows = LaneArrowManager.Instance.GetFinalLaneArrows(nextLaneId);
									bool hasLeftArrow = (nextLaneArrows & LaneArrows.Left) != LaneArrows.None;
									bool hasRightArrow = (nextLaneArrows & LaneArrows.Right) != LaneArrows.None;
									bool hasForwardArrow = (nextLaneArrows & LaneArrows.Forward) != LaneArrows.None || (nextLaneArrows & LaneArrows.LeftForwardRight) == LaneArrows.None;

#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): start lane arrow check for nextLaneId={nextLaneId}, idx={nextLaneIndex}: hasLeftArrow={hasLeftArrow}, hasForwardArrow={hasForwardArrow}, hasRightArrow={hasRightArrow}");
#endif

									if (applyHighwayRules || // highway rules enabled
											(nextIncomingDir == ArrowDirection.Right && hasLeftArrow) || // valid incoming right
											(nextIncomingDir == ArrowDirection.Left && hasRightArrow) || // valid incoming left
											(nextIncomingDir == ArrowDirection.Forward && hasForwardArrow) || // valid incoming straight
											(nextIncomingDir == ArrowDirection.Turn && (nextIsEndOrOneWayOut || ((leftHandDrive && hasRightArrow) || (!leftHandDrive && hasLeftArrow))))) { // valid turning lane
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): lane arrow check passed for nextLaneId={nextLaneId}, idx={nextLaneIndex}. adding as default lane.");
#endif
										isCompatibleLane = true;
										transitionType = LaneEndTransitionType.Default;
									} else if (nextIsConnectedWithPrev) {
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): lane arrow check FAILED for nextLaneId={nextLaneId}, idx={nextLaneIndex}. adding as relaxed lane.");
#endif

										// lane can be used by all vehicles that may disregard lane arrows
										transitionType = LaneEndTransitionType.Relaxed;
										if (numNextRelaxedTransitionDatas < MAX_NUM_TRANSITIONS) {
											nextRelaxedTransitionDatas[numNextRelaxedTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType, nextSegmentId, isNextStartNodeOfNextSegment, GlobalConfig.Instance.PathFinding.IncompatibleLaneDistance);
										} else {
											Log.Warning($"nextTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
										}
									}
								} else if (!nextHasOutgoingConnections) {
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): nextLaneId={nextLaneId}, idx={nextLaneIndex} is used by vehicles that do not follow lane arrows. adding as default.");
#endif

									// routed vehicle that does not follow lane arrows (trains, trams, metros, monorails)
									transitionType = LaneEndTransitionType.Default;

									if (numNextForcedTransitionDatas < MAX_NUM_TRANSITIONS) {
										nextForcedTransitionDatas[numNextForcedTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType, nextSegmentId, isNextStartNodeOfNextSegment);
									} else {
										Log.Warning($"nextForcedTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
									}
								}

								if (isCompatibleLane) {
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): adding nextLaneId={nextLaneId}, idx={nextLaneIndex} as compatible lane now.");
#endif

									if (numNextCompatibleTransitionDatas < MAX_NUM_TRANSITIONS) {
										nextCompatibleOuterSimilarIndices[numNextCompatibleTransitionDatas] = nextOuterSimilarLaneIndex;
										compatibleLaneIndexToLaneConnectionIndex[numNextCompatibleTransitionDatas] = currentLaneConnectionTransIndex;
										//compatibleLaneIndicesMask |= POW2MASKS[numNextCompatibleTransitionDatas];
										nextCompatibleTransitionDatas[numNextCompatibleTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType, nextSegmentId, isNextStartNodeOfNextSegment);
									} else {
										Log.Warning($"nextCompatibleTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
									}
								} else {
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): nextLaneId={nextLaneId}, idx={nextLaneIndex} is NOT compatible.");
#endif
								}
							}
						} else {
#if DEBUGROUTING
							if (debug)
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): lane direction check NOT passed for nextLaneId={nextLaneId}, idx={nextLaneIndex}: isNextValid={isNextSegmentValid}, nextLaneInfo.m_finalDirection={nextLaneInfo.m_finalDirection}, nextDir2={nextDir2}");
#endif
							if ((nextLaneInfo.m_finalDirection & NetInfo.InvertDirection(nextDir2)) != NetInfo.Direction.None) {
								++outgoingVehicleLanes;
#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): increasing number of outgoing lanes at nextLaneId={nextLaneId}, idx={nextLaneIndex}: isNextValid={isNextSegmentValid}, nextLaneInfo.m_finalDirection={nextLaneInfo.m_finalDirection}, nextDir2={nextDir2}: incomingVehicleLanes={incomingVehicleLanes}, outgoingVehicleLanes={outgoingVehicleLanes}");
#endif
							}
						}
					} else {
#if DEBUGROUTING
						if (debug)
							Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): vehicle type check NOT passed for nextLaneId={nextLaneId}, idx={nextLaneIndex}: prevLaneInfo.m_vehicleType={prevLaneInfo.m_vehicleType}, nextLaneInfo.m_vehicleType={nextLaneInfo.m_vehicleType}");
#endif
					}

					Constants.ServiceFactory.NetService.ProcessLane(nextLaneId, delegate (uint lId, ref NetLane lane) {
						nextLaneId = lane.m_nextLane;
						return true;
					});
					++nextLaneIndex;
				} // foreach lane


#if DEBUGROUTING
				if (debug)
					Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): isNextValid={isNextSegmentValid} Compatible lanes: " + nextCompatibleTransitionDatas?.ArrayToString());
#endif
				if (isNextSegmentValid) {
					bool laneChangesAllowed = JunctionRestrictionsManager.Instance.IsLaneChangingAllowedWhenGoingStraight(nextSegmentId, isNextStartNodeOfNextSegment);
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

#if DEBUGROUTING
						if (debug)
							Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): found compatible lanes! compatibleLaneIndicesSortedByOuterSimilarIndex={compatibleLaneIndicesSortedByOuterSimilarIndex.ArrayToString()}, laneDiff={laneDiff}, applyHighwayRulesAtSegment={applyHighwayRulesAtSegment}");
#endif

						if (applyHighwayRulesAtJunction) {
							// we reached a highway junction where more than two segments are connected to each other
#if DEBUGROUTING
							if (debug)
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): applying highway rules at junction");
#endif

							// number of lanes that were processed in earlier segment iterations (either all incoming or all outgoing)
							int numLanesSeen = Math.Max(totalIncomingLanes, totalOutgoingLanes);

							int minNextInnerSimilarIndex = -1;
							int maxNextInnerSimilarIndex = -1;
							int refNextInnerSimilarIndex = -1; // this lane will be referred as the "stay" lane with zero distance

#if DEBUGHWJUNCTIONROUTING
							if (debug) {
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): applying highway rules at junction");
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): totalIncomingLanes={totalIncomingLanes}, totalOutgoingLanes={totalOutgoingLanes}, numLanesSeen={numLanesSeen} laneChangesAllowed={laneChangesAllowed}");
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevInnerSimilarLaneIndex={prevInnerSimilarLaneIndex}, prevSimilarLaneCount={prevSimilarLaneCount}, nextCompatibleLaneCount={nextCompatibleLaneCount}");
							}
#endif

							if (nextIsSplitJunction) {
								// lane splitting at junction
								minNextInnerSimilarIndex = prevInnerSimilarLaneIndex + numLanesSeen;

								if (minNextInnerSimilarIndex >= nextCompatibleLaneCount) {
									// there have already been explored more outgoing lanes than incoming lanes on the previous segment. Also allow vehicles to go to the current segment.
									minNextInnerSimilarIndex = maxNextInnerSimilarIndex = refNextInnerSimilarIndex = nextCompatibleLaneCount - 1;
								} else {
									maxNextInnerSimilarIndex = refNextInnerSimilarIndex = minNextInnerSimilarIndex;
									if (laneChangesAllowed) {
										// allow lane changes at highway junctions
										if (minNextInnerSimilarIndex > 0 && prevInnerSimilarLaneIndex > 0) {
											--minNextInnerSimilarIndex;
										}
									}
								}

#if DEBUGHWJUNCTIONROUTING
								if (debug) {
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): highway rules at junction: lane splitting junction. minNextInnerSimilarIndex={minNextInnerSimilarIndex}, maxNextInnerSimilarIndex={maxNextInnerSimilarIndex}");
								}
#endif
							} else {
								// lane merging at junction
								minNextInnerSimilarIndex = prevInnerSimilarLaneIndex - numLanesSeen;

								if (minNextInnerSimilarIndex < 0) {
									if (prevInnerSimilarLaneIndex == prevSimilarLaneCount - 1) {
										// there have already been explored more incoming lanes than outgoing lanes on the previous segment. Allow the current segment to also join the big merging party. What a fun!
										minNextInnerSimilarIndex = 0;
										maxNextInnerSimilarIndex = nextCompatibleLaneCount - 1;
									} else {
										// lanes do not connect (min/max = -1)
									}
								} else {
									// allow lane changes at highway junctions
									refNextInnerSimilarIndex = minNextInnerSimilarIndex;
									if (laneChangesAllowed) {
										maxNextInnerSimilarIndex = Math.Min(nextCompatibleLaneCount - 1, minNextInnerSimilarIndex + 1);
										if (minNextInnerSimilarIndex > 0) {
											--minNextInnerSimilarIndex;
										}
									} else {
										maxNextInnerSimilarIndex = minNextInnerSimilarIndex;
									}

									if (totalIncomingLanes > 0 && prevInnerSimilarLaneIndex == prevSimilarLaneCount - 1 && maxNextInnerSimilarIndex < nextCompatibleLaneCount - 1) {
										// we reached the outermost lane on the previous segment but there are still lanes to go on the next segment: allow merging
										maxNextInnerSimilarIndex = nextCompatibleLaneCount - 1;
									}
								}

#if DEBUGHWJUNCTIONROUTING
								if (debug) {
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): highway rules at junction: lane merging/unknown junction. minNextInnerSimilarIndex={minNextInnerSimilarIndex}, maxNextInnerSimilarIndex={maxNextInnerSimilarIndex}");
								}
#endif
							}

							if (minNextInnerSimilarIndex >= 0) {
#if DEBUGHWJUNCTIONROUTING
								if (debug) {
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): minNextInnerSimilarIndex >= 0. nextCompatibleTransitionDatas={nextCompatibleTransitionDatas.ArrayToString()}");
								}
#endif

								// explore lanes
								for (int nextInnerSimilarIndex = minNextInnerSimilarIndex; nextInnerSimilarIndex <= maxNextInnerSimilarIndex; ++nextInnerSimilarIndex) {
									int nextTransitionIndex = FindLaneByInnerIndex(nextCompatibleTransitionDatas, numNextCompatibleTransitionDatas, nextSegmentId, nextInnerSimilarIndex);

#if DEBUGHWJUNCTIONROUTING
									if (debug) {
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): highway junction iteration: nextInnerSimilarIndex={nextInnerSimilarIndex}, nextTransitionIndex={nextTransitionIndex}");
									}
#endif

									if (nextTransitionIndex < 0) {
										continue;
									}

									// calculate lane distance
									byte compatibleLaneDist = 0;
									if (refNextInnerSimilarIndex >= 0) {
										compatibleLaneDist = (byte)Math.Abs(refNextInnerSimilarIndex - nextInnerSimilarIndex);
									}

									// skip lanes having lane connections
									if (LaneConnectionManager.Instance.HasConnections(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, isNextStartNodeOfNextSegment)) {
										int laneConnectionTransIndex = compatibleLaneIndexToLaneConnectionIndex[nextTransitionIndex];
										if (laneConnectionTransIndex >= 0) {
											nextLaneConnectionTransitionDatas[laneConnectionTransIndex].distance = compatibleLaneDist;
										}
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): Next lane ({nextCompatibleTransitionDatas[nextTransitionIndex].laneId}) has outgoing lane connections. Skip for now but set compatibleLaneDist={compatibleLaneDist} if laneConnectionTransIndex={laneConnectionTransIndex} >= 0.");
#endif
										continue; // disregard lane since it has outgoing connections
									}

									nextCompatibleTransitionDatas[nextTransitionIndex].distance = compatibleLaneDist;
#if DEBUGHWJUNCTIONROUTING
									if (debug) {
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): highway junction iteration: compatibleLaneDist={compatibleLaneDist}");
									}
#endif

									UpdateHighwayLaneArrows(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, isNextStartNodeOfNextSegment, nextIncomingDir);

									if (numNextCompatibleTransitionDataIndices < MAX_NUM_TRANSITIONS) {
										nextCompatibleTransitionDataIndices[numNextCompatibleTransitionDataIndices++] = nextTransitionIndex;
									} else {
										Log.Warning($"nextCompatibleTransitionDataIndices overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
									}
								}

#if DEBUGHWJUNCTIONROUTING
								if (debug) {
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): highway junction iterations finished: nextCompatibleTransitionDataIndices={nextCompatibleTransitionDataIndices.ArrayToString()}");
								}
#endif
							}
						} else {
							/*
							 * This is
							 *    1. a highway lane splitting/merging point,
							 *    2. a city or highway lane continuation point (simple transition with equal number of lanes or flagged city transition), or
							 *    3. a city junction
							 *  with multiple or a single target lane: Perform lane matching
							 */

#if DEBUGROUTING
							if (debug)
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): regular node");
#endif

							// min/max compatible outer similar lane indices
							int minNextCompatibleOuterSimilarIndex = -1;
							int maxNextCompatibleOuterSimilarIndex = -1;
							if (nextIncomingDir == ArrowDirection.Turn) {
								minNextCompatibleOuterSimilarIndex = 0;
								maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): u-turn: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
							} else if (isNextRealJunction) {
#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): next is real junction");
#endif

								// at junctions: try to match distinct lanes
								if (nextCompatibleLaneCount > prevSimilarLaneCount && prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
									// merge inner lanes
									minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
									maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): merge inner lanes: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
								} else if (nextCompatibleLaneCount < prevSimilarLaneCount && prevSimilarLaneCount % nextCompatibleLaneCount == 0) {
									// symmetric split
									int splitFactor = prevSimilarLaneCount / nextCompatibleLaneCount;
									minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex / splitFactor;
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): symmetric split: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
								} else {
									// 1-to-n (split inner lane) or 1-to-1 (direct lane matching)
									minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
									maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): 1-to-n (split inner lane) or 1-to-1 (direct lane matching): minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
								}

								bool straightLaneChangesAllowed = nextIncomingDir == ArrowDirection.Forward && laneChangesAllowed;

#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): laneChangesAllowed={laneChangesAllowed} straightLaneChangesAllowed={straightLaneChangesAllowed}");
#endif

								if (!straightLaneChangesAllowed) {
									if (nextHasBusLane && !prevHasBusLane) {
										// allow vehicles on the bus lane AND on the next lane to merge on this lane
										maxNextCompatibleOuterSimilarIndex = Math.Min(nextCompatibleLaneCount - 1, maxNextCompatibleOuterSimilarIndex + 1);
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): allow vehicles on the bus lane AND on the next lane to merge on this lane: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
									} else if (!nextHasBusLane && prevHasBusLane) {
										// allow vehicles to enter the bus lane
										minNextCompatibleOuterSimilarIndex = Math.Max(0, minNextCompatibleOuterSimilarIndex - 1);
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): allow vehicles to enter the bus lane: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
									}
								} else {
									// vehicles may change lanes when going straight
									minNextCompatibleOuterSimilarIndex = minNextCompatibleOuterSimilarIndex - 1;
									maxNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex + 1;
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): vehicles may change lanes when going straight: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
								}
							} else if (prevSimilarLaneCount == nextCompatibleLaneCount) {
								// equal lane count: consider all available lanes
								minNextCompatibleOuterSimilarIndex = 0;
								maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): equal lane count: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
							} else {
								// lane continuation point: lane merging/splitting

#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): lane continuation point: lane merging/splitting");
#endif

								bool sym1 = (prevSimilarLaneCount & 1) == 0; // mod 2 == 0
								bool sym2 = (nextCompatibleLaneCount & 1) == 0; // mod 2 == 0
#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): sym1={sym1}, sym2={sym2}");
#endif
								if (prevSimilarLaneCount < nextCompatibleLaneCount) {
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): lane merging (prevSimilarLaneCount={prevSimilarLaneCount} < nextCompatibleLaneCount={nextCompatibleLaneCount})");
#endif

									// lane merging
									if (sym1 == sym2) {
										// merge outer lanes
										int a = (nextCompatibleLaneCount - prevSimilarLaneCount) >> 1; // nextCompatibleLaneCount - prevSimilarLaneCount is always > 0
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): merge outer lanes. a={a}");
#endif
										if (prevSimilarLaneCount == 1) {
											minNextCompatibleOuterSimilarIndex = 0;
											maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1; // always >=0
#if DEBUGROUTING
											if (debug)
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevSimilarLaneCount == 1: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
										} else if (prevOuterSimilarLaneIndex == 0) {
											minNextCompatibleOuterSimilarIndex = 0;
											maxNextCompatibleOuterSimilarIndex = a;
#if DEBUGROUTING
											if (debug)
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevOuterSimilarLaneIndex == 0: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
										} else if (prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
											minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
											maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1; // always >=0
#if DEBUGROUTING
											if (debug)
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
										} else {
											minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
#if DEBUGROUTING
											if (debug)
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): default case: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
										}
									} else {
										// criss-cross merge
										int a = (nextCompatibleLaneCount - prevSimilarLaneCount - 1) >> 1; // nextCompatibleLaneCount - prevSimilarLaneCount - 1 is always >= 0
										int b = (nextCompatibleLaneCount - prevSimilarLaneCount + 1) >> 1; // nextCompatibleLaneCount - prevSimilarLaneCount + 1 is always >= 2
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): criss-cross merge: a={a}, b={b}");
#endif
										if (prevSimilarLaneCount == 1) {
											minNextCompatibleOuterSimilarIndex = 0;
											maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1; // always >=0
#if DEBUGROUTING
											if (debug)
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevSimilarLaneCount == 1: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
										} else if (prevOuterSimilarLaneIndex == 0) {
											minNextCompatibleOuterSimilarIndex = 0;
											maxNextCompatibleOuterSimilarIndex = b;
#if DEBUGROUTING
											if (debug)
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevOuterSimilarLaneIndex == 0: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
										} else if (prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
											minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
											maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1; // always >=0
#if DEBUGROUTING
											if (debug)
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
										} else {
											minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
											maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + b;
#if DEBUGROUTING
											if (debug)
												Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): default criss-cross case: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
										}
									}
								} else {
									// at lane splits: distribute traffic evenly (1-to-n, n-to-n)										
									// prevOuterSimilarIndex is always > nextCompatibleLaneCount
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): at lane splits: distribute traffic evenly (1-to-n, n-to-n)");
#endif
									if (sym1 == sym2) {
										// split outer lanes
										int a = (prevSimilarLaneCount - nextCompatibleLaneCount) >> 1; // prevSimilarLaneCount - nextCompatibleLaneCount is always > 0
										minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex - a; // a is always <= prevSimilarLaneCount
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): split outer lanes: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
									} else {
										// split outer lanes, criss-cross inner lanes 
										int a = (prevSimilarLaneCount - nextCompatibleLaneCount - 1) >> 1; // prevSimilarLaneCount - nextCompatibleLaneCount - 1 is always >= 0

										minNextCompatibleOuterSimilarIndex = (a - 1 >= prevOuterSimilarLaneIndex) ? 0 : prevOuterSimilarLaneIndex - a - 1;
										maxNextCompatibleOuterSimilarIndex = (a >= prevOuterSimilarLaneIndex) ? 0 : prevOuterSimilarLaneIndex - a;
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): split outer lanes, criss-cross inner lanes: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif
									}
								}
							}

#if DEBUGROUTING
							if (debug)
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): pre-final bounds: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif

							minNextCompatibleOuterSimilarIndex = Math.Max(0, Math.Min(minNextCompatibleOuterSimilarIndex, nextCompatibleLaneCount - 1));
							maxNextCompatibleOuterSimilarIndex = Math.Max(0, Math.Min(maxNextCompatibleOuterSimilarIndex, nextCompatibleLaneCount - 1));

							if (minNextCompatibleOuterSimilarIndex > maxNextCompatibleOuterSimilarIndex) {
								minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex;
							}

#if DEBUGROUTING
							if (debug)
								Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): final bounds: minNextCompatibleOuterSimilarIndex={minNextCompatibleOuterSimilarIndex}, maxNextCompatibleOuterSimilarIndex={maxNextCompatibleOuterSimilarIndex}");
#endif

							// find best matching lane(s)
							for (int nextCompatibleOuterSimilarIndex = minNextCompatibleOuterSimilarIndex; nextCompatibleOuterSimilarIndex <= maxNextCompatibleOuterSimilarIndex; ++nextCompatibleOuterSimilarIndex) {
								int nextTransitionIndex = FindLaneWithMaxOuterIndex(compatibleLaneIndicesSortedByOuterSimilarIndex, nextCompatibleOuterSimilarIndex);

#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): best matching lane iteration -- nextCompatibleOuterSimilarIndex={nextCompatibleOuterSimilarIndex} => nextTransitionIndex={nextTransitionIndex}");
#endif

								if (nextTransitionIndex < 0) {
									continue;
								}

								// calculate lane distance
								byte compatibleLaneDist = 0;
								if (nextIncomingDir == ArrowDirection.Turn) {
									compatibleLaneDist = (byte)GlobalConfig.Instance.PathFinding.UturnLaneDistance;
								} else if (!isNextRealJunction && ((!nextIsJunction && !nextIsTransition) || nextCompatibleLaneCount == prevSimilarLaneCount)) {
									int relLaneDist = nextCompatibleOuterSimilarIndices[nextTransitionIndex] - prevOuterSimilarLaneIndex; // relative lane distance (positive: change to more outer lane, negative: change to more inner lane)
									compatibleLaneDist = (byte)Math.Abs(relLaneDist);
								}

								// skip lanes having lane connections
								if (LaneConnectionManager.Instance.HasConnections(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, isNextStartNodeOfNextSegment)) {
									int laneConnectionTransIndex = compatibleLaneIndexToLaneConnectionIndex[nextTransitionIndex];
									if (laneConnectionTransIndex >= 0) {
										nextLaneConnectionTransitionDatas[laneConnectionTransIndex].distance = compatibleLaneDist;
									}
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): Next lane ({nextCompatibleTransitionDatas[nextTransitionIndex].laneId}) has outgoing lane connections. Skip for now but set compatibleLaneDist={compatibleLaneDist} if laneConnectionTransIndex={laneConnectionTransIndex} >= 0.");
#endif
									continue; // disregard lane since it has outgoing connections
								}

								if (nextIncomingDir == ArrowDirection.Turn) {
									// force u-turns to happen on the innermost lane
									if (nextCompatibleOuterSimilarIndex != maxNextCompatibleOuterSimilarIndex) {
										++compatibleLaneDist;
										nextCompatibleTransitionDatas[nextTransitionIndex].type = LaneEndTransitionType.Relaxed;
#if DEBUGROUTING
										if (debug)
											Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): Next lane ({nextCompatibleTransitionDatas[nextTransitionIndex].laneId}) is avoided u-turn. Incrementing compatible lane distance to {compatibleLaneDist}");
#endif
									}
								}

#if DEBUGROUTING
								if (debug)
									Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): -> compatibleLaneDist={compatibleLaneDist}");
#endif

								nextCompatibleTransitionDatas[nextTransitionIndex].distance = compatibleLaneDist;
								if (onHighway && !isNextRealJunction && compatibleLaneDist > 1) {
									// under normal circumstances vehicles should not change more than one lane on highways at one time
									nextCompatibleTransitionDatas[nextTransitionIndex].type = LaneEndTransitionType.Relaxed;
#if DEBUGROUTING
									if (debug)
										Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): -> under normal circumstances vehicles should not change more than one lane on highways at one time: setting type to Relaxed");
#endif
								} else if (applyHighwayRulesAtSegment) {
									UpdateHighwayLaneArrows(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, isNextStartNodeOfNextSegment, nextIncomingDir);
								}

								if (numNextCompatibleTransitionDataIndices < MAX_NUM_TRANSITIONS) {
									nextCompatibleTransitionDataIndices[numNextCompatibleTransitionDataIndices++] = nextTransitionIndex;
								} else {
									Log.Warning($"nextCompatibleTransitionDataIndices overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
								}
							} // foreach lane
						} // highway/city rules if/else
					} // compatible lanes found

					// build final array
					LaneTransitionData[] nextTransitionDatas = new LaneTransitionData[numNextRelaxedTransitionDatas + numNextCompatibleTransitionDataIndices + numNextLaneConnectionTransitionDatas + numNextForcedTransitionDatas];
					int j = 0;
					for (int i = 0; i < numNextCompatibleTransitionDataIndices; ++i) {
						nextTransitionDatas[j++] = nextCompatibleTransitionDatas[nextCompatibleTransitionDataIndices[i]];
					}

					for (int i = 0; i < numNextLaneConnectionTransitionDatas; ++i) {
						nextTransitionDatas[j++] = nextLaneConnectionTransitionDatas[i];
					}

					for (int i = 0; i < numNextRelaxedTransitionDatas; ++i) {
						nextTransitionDatas[j++] = nextRelaxedTransitionDatas[i];
					}

					for (int i = 0; i < numNextForcedTransitionDatas; ++i) {
						nextTransitionDatas[j++] = nextForcedTransitionDatas[i];
					}

#if DEBUGROUTING
					if (debug)
						Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): build final array: nextTransitionDatas={nextTransitionDatas.ArrayToString()}");
#endif

					backwardRouting.AddTransitions(nextTransitionDatas);

#if DEBUGROUTING
					if (debug)
						Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): updated incoming/outgoing lanes for next segment iteration: totalIncomingLanes={totalIncomingLanes}, totalOutgoingLanes={totalOutgoingLanes}");
#endif
				} // valid segment

				if (nextSegmentId != prevSegmentId) {
					totalIncomingLanes += incomingVehicleLanes;
					totalOutgoingLanes += outgoingVehicleLanes;
				}

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

			// update backward routing
			laneEndBackwardRoutings[GetLaneEndRoutingIndex(laneId, startNode)] = backwardRouting;

			// update forward routing
			LaneTransitionData[] newTransitions = backwardRouting.transitions;
			if (newTransitions != null) {
				for (int i = 0; i < newTransitions.Length; ++i) {
					uint sourceIndex = GetLaneEndRoutingIndex(newTransitions[i].laneId, newTransitions[i].startNode);

					LaneTransitionData forwardTransition = new LaneTransitionData();
					forwardTransition.laneId = laneId;
					forwardTransition.laneIndex = (byte)laneIndex;
					forwardTransition.type = newTransitions[i].type;
					forwardTransition.distance = newTransitions[i].distance;
					forwardTransition.segmentId = segmentId;
					forwardTransition.startNode = startNode;

					laneEndForwardRoutings[sourceIndex].AddTransition(forwardTransition);

#if DEBUGROUTING
					if (debug)
						Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): adding transition to forward routing of laneId={laneId}, idx={laneIndex} @ {newTransitions[i].startNode} (sourceIndex={sourceIndex}): {forwardTransition.ToString()}\n\nNew forward routing:\n{laneEndForwardRoutings[sourceIndex].ToString()}");
#endif
				}
			}

#if DEBUGROUTING
			if (debug)
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, {laneId}, {startNode}): FINISHED calculating routing data for array index {GetLaneEndRoutingIndex(laneId, startNode)}: {backwardRouting}");
#endif
		}

		/// <summary>
		/// remove all forward routings pointing to this lane
		/// </summary>
		/// <param name="laneId"></param>
		/// <param name="startNode"></param>
		protected void ResetLaneRoutings(uint laneId, bool startNode) {
			uint index = GetLaneEndRoutingIndex(laneId, startNode);

			LaneTransitionData[] oldBackwardTransitions = laneEndBackwardRoutings[index].transitions;
			if (oldBackwardTransitions != null) {
				for (int i = 0; i < oldBackwardTransitions.Length; ++i) {
					uint sourceIndex = GetLaneEndRoutingIndex(oldBackwardTransitions[i].laneId, oldBackwardTransitions[i].startNode);
					laneEndForwardRoutings[sourceIndex].RemoveTransition(laneId);
				}
			}

			laneEndBackwardRoutings[index].Reset();
		}

		private void UpdateHighwayLaneArrows(uint laneId, bool startNode, ArrowDirection dir) {

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
			//Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: highway rules -- next lane {laneId} obeys highway rules. Setting highway lane arrows to {newHighwayArrows}. prevHighwayArrows={prevHighwayArrows}");
#endif

			if (newHighwayArrows != prevHighwayArrows && newHighwayArrows != Flags.LaneArrows.None) {
				Flags.setHighwayLaneArrowFlags(laneId, newHighwayArrows, false);
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

		public int CalcInnerSimilarLaneIndex(ushort segmentId, int laneIndex) {
			int ret = -1;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				ret = CalcInnerSimilarLaneIndex(segment.Info.m_lanes[laneIndex]);
				return true;
			});

			return ret;
		}

		public int CalcInnerSimilarLaneIndex(NetInfo.Lane laneInfo) {
			// note: m_direction is correct here
			return (byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0 ? laneInfo.m_similarLaneIndex : laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1;
		}

		public int CalcOuterSimilarLaneIndex(ushort segmentId, int laneIndex) {
			int ret = -1;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				ret = CalcOuterSimilarLaneIndex(segment.Info.m_lanes[laneIndex]);
				return true;
			});

			return ret;
		}

		public int CalcOuterSimilarLaneIndex(NetInfo.Lane laneInfo) {
			// note: m_direction is correct here
			return (byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0 ? laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1 : laneInfo.m_similarLaneIndex;
		}

		protected int FindLaneWithMaxOuterIndex(int[] indicesSortedByOuterIndex, int targetOuterLaneIndex) {
			return indicesSortedByOuterIndex[Math.Max(0, Math.Min(targetOuterLaneIndex, indicesSortedByOuterIndex.Length - 1))];
		}

		protected int FindLaneByOuterIndex(LaneTransitionData[] laneTransitions, int num, ushort segmentId, int targetOuterLaneIndex) {
			for (int i = 0; i < num; ++i) {
				int outerIndex = CalcOuterSimilarLaneIndex(segmentId, laneTransitions[i].laneIndex);
				if (outerIndex == targetOuterLaneIndex) {
					return i;
				}
			}
			return -1;
		}

		protected int FindLaneByInnerIndex(LaneTransitionData[] laneTransitions, int num, ushort segmentId, int targetInnerLaneIndex) {
			for (int i = 0; i < num; ++i) {
				int innerIndex = CalcInnerSimilarLaneIndex(segmentId, laneTransitions[i].laneIndex);
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
			for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
				SubscribeToSegmentGeometry((ushort)segmentId);
			}
		}
	}
}
