using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Util;
using Util;
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
			Invalid,
			LaneArrow,
			LaneConnection,
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
#if DEBUGGEO
			public ushort segmentId;
#endif

			public override string ToString() {
				return $"[LaneTransitionData\n" +
					"\t" + $"laneId = {laneId}\n" +
					"\t" + $"laneIndex = {laneIndex}\n" +
#if DEBUGGEO
					"\t" + $"segmentId = {segmentId}\n" +
#endif
					"\t" + $"type = {type}\n" +
					"\t" + $"distance = {distance}\n" +
					"LaneTransitionData]";
			}

			public void Set(uint laneId, byte laneIndex, LaneEndTransitionType type, byte distance
#if DEBUGGEO
				, ushort segmentId
#endif
				) {
				this.laneId = laneId;
				this.laneIndex = laneIndex;
				this.type = type;
				this.distance = distance;
#if DEBUGGEO
				this.segmentId = segmentId;
#endif
			}

			public void Set(uint laneId, byte laneIndex, LaneEndTransitionType type
#if DEBUGGEO
				, ushort segmentId
#endif
				) {
				Set(laneId, laneIndex, type, 0
#if DEBUGGEO
				, segmentId
#endif
				);
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
			if (!segmentsUpdated) {
				return;
			}

			try {
				Monitor.Enter(updateLock);
				segmentsUpdated = false;

				int len = updatedSegmentBuckets.Length;
				for (int i = 0; i < len; i++) {
					ulong segMask = updatedSegmentBuckets[i];
					if (segMask != 0uL) {
						for (int m = 0; m < 64; m++) {
							if ((segMask & 1uL << m) != 0uL) {
								ushort segmentId = (ushort)(i << 6 | m);
								RecalculateSegment(segmentId, false);
							}
						}
						updatedSegmentBuckets[i] = 0;
					}
				}
			} finally {
				Monitor.Exit(updateLock);
			}
		}

		public void RecalculateAll() {
			Log._Debug($"RoutingManager.RecalculateAll: called");
			for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
				try {
					RecalculateSegment(segmentId, false);
				} catch (Exception e) {
					Log.Error($"An error occurred while calculating routes for segment {segmentId}: {e}");
				}
			}
		}

		public void RecalculateSegment(ushort segmentId, bool propagate = true) {
			Log._Debug($"RoutingManager.RecalculateSegment: called for seg. {segmentId}, propagate={propagate}");
			RecalculateSegmentRoutingData(segmentId);

			Services.NetService.IterateSegmentLanes(segmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segId, ref NetSegment segment, byte laneIndex) {
				RecalculateLaneEndRoutingData(segmentId, laneIndex, laneId, true);
				RecalculateLaneEndRoutingData(segmentId, laneIndex, laneId, false);

				return true;
			});

			if (propagate) {
				SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
				if (segGeo == null) {
					return;
				}

				foreach (ushort otherSegmentId in segGeo.GetConnectedSegments(true)) {
					if (otherSegmentId == 0) {
						continue;
					}

					RecalculateSegment(otherSegmentId, false);
				}

				foreach (ushort otherSegmentId in segGeo.GetConnectedSegments(false)) {
					if (otherSegmentId == 0) {
						continue;
					}

					RecalculateSegment(otherSegmentId, false);
				}
			}
		}

		protected void ResetRoutingData(ushort segmentId) {
			Log._Debug($"RoutingManager.ResetRoutingData: called for segment {segmentId}");
			segmentRoutings[segmentId].Reset();

			Services.NetService.IterateSegmentLanes(segmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segId, ref NetSegment segment, byte laneIndex) {
				Log._Debug($"RoutingManager.HandleInvalidSegment: Resetting lane {laneId}, idx {laneIndex} @ seg. {segmentId}");
				uint index = GetLaneEndRoutingIndex(laneId, true);
				laneEndRoutings[index].Reset();

				index = GetLaneEndRoutingIndex(laneId, false);
				laneEndRoutings[index].Reset();

				return true;
			});
		}

		protected void RecalculateSegmentRoutingData(ushort segmentId) {
			Log._Debug($"RoutingManager.RecalculateSegmentRoutingData: called for seg. {segmentId}");

			segmentRoutings[segmentId].Reset();

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				return;
			}

			segmentRoutings[segmentId].highway = segGeo.IsHighway();
			segmentRoutings[segmentId].startNodeOutgoingOneWay = segGeo.IsOutgoingOneWay(true);
			segmentRoutings[segmentId].endNodeOutgoingOneWay = segGeo.IsOutgoingOneWay(false);

#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($"RoutingManager.RecalculateSegmentRoutingData: Calculated routing data for segment {segmentId}: {segmentRoutings[segmentId]}");
#endif
		}

		protected void RecalculateLaneEndRoutingData(ushort segmentId, int laneIndex, uint laneId, bool startNode) {
			Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: called for seg. {segmentId}, lane {laneId}, idx {laneIndex}, start {startNode}");

			uint index = GetLaneEndRoutingIndex(laneId, startNode);
			laneEndRoutings[index].Reset();

			if (! IsOutgoingLane(segmentId, startNode, laneIndex)) {
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
			ushort nextSegmentId = segmentId; // start with u-turns
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
			bool nextIsTransition = false;
			bool nextHasTrafficLights = false;
			Constants.ServiceFactory.NetService.ProcessNode(nextNodeId, delegate (ushort nodeId, ref NetNode node) {
				nextIsJunction = (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
				nextIsTransition = (node.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
				nextHasTrafficLights = (node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
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

			int totalIncomingLanes = 0; // running number of next incoming lanes (number is updated at each segment iteration)
			int totalOutgoingLanes = 0; // running number of next outgoing lanes (number is updated at each segment iteration)

			for (int k = 0; k < 8; ++k) {
				int outgoingVehicleLanes = 0;
				int incomingVehicleLanes = 0;

				laneEndRoutings[index].SetTransitions(k, null);

				if (nextSegmentId == 0) {
					continue;
				}

				bool uturn = nextSegmentId == prevSegmentId;
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

				// determine next segment direction by evaluating the geometry information
				bool isIncomingRight = false;
				bool isIncomingStraight = false;
				bool isIncomingLeft = false;
				bool isIncomingTurn = false;
				bool isNextValid = true;

				if (nextSegmentId != prevSegmentId) {
					for (int j = 0; j < prevEndGeo.IncomingStraightSegments.Length; ++j) {
						if (prevEndGeo.IncomingStraightSegments[j] == 0) {
							break;
						}
						if (prevEndGeo.IncomingStraightSegments[j] == nextSegmentId) {
							isIncomingStraight = true;
							break;
						}
					}

					if (!isIncomingStraight) {
						for (int j = 0; j < prevEndGeo.IncomingRightSegments.Length; ++j) {
							if (prevEndGeo.IncomingRightSegments[j] == 0) {
								break;
							}
							if (prevEndGeo.IncomingRightSegments[j] == nextSegmentId) {
								isIncomingRight = true;
								break;
							}
						}

						if (!isIncomingRight) {
							for (int j = 0; j < prevEndGeo.IncomingLeftSegments.Length; ++j) {
								if (prevEndGeo.IncomingLeftSegments[j] == 0) {
									break;
								}
								if (prevEndGeo.IncomingLeftSegments[j] == nextSegmentId) {
									isIncomingLeft = true;
									break;
								}
							}

							if (!isIncomingLeft) {
								isNextValid = false;
							}
						}
					}
				} else {
					isIncomingTurn = true;
				}

#if DEBUGGEO
				if (GlobalConfig.Instance.DebugSwitches[5])
					Log._Debug($"RoutingManager.RecalculateSegmentRoutingData: prevSegment={segmentId}. Exploring nextSegment={nextSegmentId} -- nextFirstLaneId={nextFirstLaneId} -- left={isIncomingLeft} straight={isIncomingStraight} right={isIncomingRight} turn={isIncomingTurn} valid={isNextValid}");
#endif

				if (isNextValid) {
					NetInfo.Direction nextDir = isNextStartNodeOfNextSegment ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
					NetInfo.Direction nextDir2 = !nextSegIsInverted ? nextDir : NetInfo.InvertDirection(nextDir);

					LaneTransitionData[] nextRelaxedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
					byte numNextRelaxedTransitionDatas = 0;
					LaneTransitionData[] nextCompatibleTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
					int[] nextCompatibleOuterSimilarIndices = new int[MAX_NUM_TRANSITIONS];
					byte numNextCompatibleTransitionDatas = 0;
					int[] nextCompatibleTransitionDataIndices = new int[MAX_NUM_TRANSITIONS];
					byte numNextCompatibleTransitionDataIndices = 0;

					uint nextLaneId = nextFirstLaneId;
					byte nextLaneIndex = 0;
					ushort compatibleOuterSimilarIndexesMask = 0;
					bool hasLaneConnections = false; // true if any lanes are connected by the lane connection tool
					//LaneEndTransition[] nextCompatibleIncomingTransitions = new LaneEndTransition[nextSegmentInfo.m_lanes.Length];
					//int nextCompatibleLaneCount = 0;

					while (nextLaneIndex < nextSegmentInfo.m_lanes.Length && nextLaneId != 0u) {
						// determine valid lanes based on lane arrows
						NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];

						LaneArrows nextLaneArrows = LaneArrowManager.Instance.GetFinalLaneArrows(nextLaneId);

#if DEBUGGEO
						if (GlobalConfig.Instance.DebugSwitches[5])
							Log._Debug($"RoutingManager.RecalculateSegmentRoutingData: prevSegment={segmentId}. Exploring nextSegment={nextSegmentId}, lane {nextLaneId}, idx {nextLaneIndex}, arrows {nextLaneArrows}");
#endif

						if (nextLaneInfo.CheckType(ROUTED_LANE_TYPES, ROUTED_VEHICLE_TYPES)) { // is compatible lane
							if ((byte)(nextLaneInfo.m_finalDirection & nextDir2) != 0) { // is incoming lane
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
										hasLeftArrow = (nextLaneArrows & LaneArrows.Left) != LaneArrows.None;
										hasRightArrow = (nextLaneArrows & LaneArrows.Right) != LaneArrows.None;
										hasForwardArrow = (nextLaneArrows & LaneArrows.Forward) != LaneArrows.None || (nextLaneArrows & LaneArrows.LeftForwardRight) == LaneArrows.None;
									}

									if (applyHighwayRules || // highway rules enabled
											(isIncomingRight && hasLeftArrow) || // valid incoming right
											(isIncomingLeft && hasRightArrow) || // valid incoming left
											(isIncomingStraight && hasForwardArrow) || // valid incoming straight
											(isIncomingTurn && ((leftHandDrive && hasRightArrow) || (!leftHandDrive && hasLeftArrow)))) { // valid turning lane
										isCompatibleLane = true;
										transitionType = LaneEndTransitionType.LaneArrow;
									} else {
										// lane can be used by all vehicles that may disregard lane arrows
										transitionType = LaneEndTransitionType.Relaxed;
										if (numNextRelaxedTransitionDatas < MAX_NUM_TRANSITIONS) {
											nextRelaxedTransitionDatas[numNextRelaxedTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType, GlobalConfig.Instance.IncompatibleLaneDistance
#if DEBUGGEO
										, nextSegmentId
#endif
												);
										} else {
											Log.Warning($"nextTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
										}
									}
								} else {
									// routed vehicle that does not follow lane arrows (e.g. trams, trains)
									transitionType = LaneEndTransitionType.Relaxed;
									if (numNextRelaxedTransitionDatas < MAX_NUM_TRANSITIONS) {
										nextRelaxedTransitionDatas[numNextRelaxedTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType
#if DEBUGGEO
										, nextSegmentId
#endif
											);
									} else {
										Log.Warning($"nextTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
									}
								}

								if (isCompatibleLane) {
									if (numNextCompatibleTransitionDatas < MAX_NUM_TRANSITIONS) {
										nextCompatibleOuterSimilarIndices[numNextCompatibleTransitionDatas] = nextOuterSimilarLaneIndex;
										nextCompatibleTransitionDatas[numNextCompatibleTransitionDatas++].Set(nextLaneId, nextLaneIndex, transitionType
#if DEBUGGEO
										, nextSegmentId
#endif
										);
										compatibleOuterSimilarIndexesMask |= POW2MASKS[nextOuterSimilarLaneIndex];
									} else {
										Log.Warning($"nextCompatibleTransitionDatas overflow @ source lane {prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
									}
								}
							} else {
								++outgoingVehicleLanes;
							}
						}

						Constants.ServiceFactory.NetService.ProcessLane(nextLaneId, delegate (uint lId, ref NetLane lane) {
							nextLaneId = lane.m_nextLane;
							return true;
						});
						++nextLaneIndex;
					} // foreach lane

					int nextCompatibleLaneCount = numNextCompatibleTransitionDatas;
					if (nextCompatibleLaneCount > 0) {
						// we found compatible lanes

						int[] tmp = new int[nextCompatibleLaneCount];
						Array.Copy(nextCompatibleOuterSimilarIndices, tmp, nextCompatibleLaneCount);
						nextCompatibleOuterSimilarIndices = tmp;

						int[] compatibleLaneIndicesSortedByOuterSimilarIndex = nextCompatibleOuterSimilarIndices.Select((x, i) => new KeyValuePair<int, int>(x, i)).OrderBy(x => x.Key).Select(x => x.Value).ToArray();
						
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

								if (nextInnerSimilarIndex >= 0 && nextInnerSimilarIndex < nextCompatibleLaneCount) {
									// enough lanes available
									nextTransitionIndex = FindLaneByInnerIndex(nextCompatibleTransitionDatas, numNextCompatibleTransitionDatas, nextSegmentId, nextInnerSimilarIndex);
								} else {
									// Highway lanes "failed". Too few lanes at prevSegment or nextSegment.
									if (nextInnerSimilarIndex < 0) {
										// lane merging failed (too many incoming lanes)
										if (totalIncomingLanes >= prevSimilarLaneCount) {
											// there have already been explored more incoming lanes than outgoing lanes on the previous segment. Allow the current segment to also join the big merging party. What a fun!
											nextTransitionIndex = FindLaneByOuterIndex(nextCompatibleTransitionDatas, numNextCompatibleTransitionDatas, nextSegmentId, prevOuterSimilarLaneIndex);
										}
									} else if (totalOutgoingLanes >= nextCompatibleLaneCount) {
										// there have already been explored more outgoing lanes than incoming lanes on the previous segment. Also allow vehicles to go to the current segment.
										nextTransitionIndex = FindLaneByOuterIndex(nextCompatibleTransitionDatas, numNextCompatibleTransitionDatas, nextSegmentId, 0);
									}
								}

								// If nextTransitionIndex is still -1 here, then highways rules really cannot handle this situation (that's ok).

								if (nextTransitionIndex >= 0) {
									// go to matched lane

									// update highway mode lane arrows
									if (LaneConnectionManager.Instance.CountConnections(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, isNextStartNodeOfNextSegment) > 0) {
										Flags.removeHighwayLaneArrowFlags(nextCompatibleTransitionDatas[nextTransitionIndex].laneId);
									} else if (applyHighwayRulesAtSegment) {
										Flags.LaneArrows? prevHighwayArrows = Flags.getHighwayLaneArrowFlags(nextCompatibleTransitionDatas[nextTransitionIndex].laneId);
										Flags.LaneArrows newHighwayArrows = Flags.LaneArrows.None;
										if (prevHighwayArrows != null)
											newHighwayArrows = (Flags.LaneArrows)prevHighwayArrows;
										if (isIncomingRight)
											newHighwayArrows |= Flags.LaneArrows.Left;
										else if (isIncomingLeft)
											newHighwayArrows |= Flags.LaneArrows.Right;
										else if (isIncomingStraight)
											newHighwayArrows |= Flags.LaneArrows.Forward;

										if (newHighwayArrows != prevHighwayArrows && newHighwayArrows != Flags.LaneArrows.None) {
											Flags.setHighwayLaneArrowFlags(nextCompatibleTransitionDatas[nextTransitionIndex].laneId, newHighwayArrows, false);
										}
									}

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
							if (uturn) {
								// force u-turns to happen on the innermost lane
								minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
							} else if (isNextRealJunction) {
								// at junctions: try to match distinct lanes
								if (nextCompatibleLaneCount > prevSimilarLaneCount && prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
									// merge inner lanes
									minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
									maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
								} else {
									// 1-to-n (lane splitting is done by FindCompatibleLane), 1-to-1 (direct lane matching)
									minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
									maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
								}

								bool mayChangeLanes = isIncomingStraight && Flags.getStraightLaneChangingAllowed(nextSegmentId, isNextStartNodeOfNextSegment);

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
								if (uturn) {
									compatibleLaneDist = (byte)GlobalConfig.Instance.UturnLaneDistance;
								} else if (!hasLaneConnections) {
									if ((compatibleOuterSimilarIndexesMask & POW2MASKS[nextCompatibleOuterSimilarIndex]) != 0) {
										if (!isNextRealJunction && nextCompatibleLaneCount == prevSimilarLaneCount) {
											int relLaneDist = nextCompatibleOuterSimilarIndices[nextTransitionIndex] - prevOuterSimilarLaneIndex; // relative lane distance (positive: change to more outer lane, negative: change to more inner lane)
											compatibleLaneDist = (byte)Math.Abs(relLaneDist);
										}
									} else {
										compatibleLaneDist = GlobalConfig.Instance.IncompatibleLaneDistance;
									}
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
					LaneTransitionData[] nextTransitionDatas = new LaneTransitionData[numNextRelaxedTransitionDatas + numNextCompatibleTransitionDataIndices];
					int j = 0;
					for (int i = 0; i < numNextCompatibleTransitionDataIndices; ++i) {
						nextTransitionDatas[j++] = nextCompatibleTransitionDatas[nextCompatibleTransitionDataIndices[i]];
					}

					for (int i = 0; i < numNextRelaxedTransitionDatas; ++i) {
						nextTransitionDatas[j++] = nextRelaxedTransitionDatas[i];
					}

					routing.SetTransitions(GetSegmentNodeIndex(nextNodeId, nextSegmentId), nextTransitionDatas);
				} // valid segment

				Constants.ServiceFactory.NetService.ProcessSegment(nextSegmentId, delegate (ushort nextSegId, ref NetSegment segment) {
					if (Constants.ServiceFactory.SimulationService.LeftHandDrive) {
						nextSegmentId = segment.GetLeftSegment(nextNodeId);
					} else {
						nextSegmentId = segment.GetRightSegment(nextNodeId);
					}
					return true;
				});

				if (nextSegmentId != prevSegmentId) {
					totalIncomingLanes += incomingVehicleLanes;
					totalOutgoingLanes += outgoingVehicleLanes;
				} else {
					// we reached the first segment again
					nextSegmentId = 0;
				}
			} // foreach segment

			laneEndRoutings[index] = routing;

#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: Calculated routing data for lane {laneId}, idx {laneIndex}, seg. {segmentId}, start {startNode}, array index {index}: {laneEndRoutings[index]}");
#endif
		}

		private int GetSegmentNodeIndex(ushort nodeId, ushort segmentId) {
			int i = -1;
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segId, ref NetSegment segment) {
				++i;
				if (segId == segmentId) {
					return false;
				}
				return true;
			});
			return i;
		}

		internal uint GetLaneEndRoutingIndex(uint laneId, bool startNode) {
			return (uint)(laneId + (startNode ? 0 : NetManager.MAX_LANE_COUNT));
		}

		public int CalcInnerLaneSimilarIndex(ushort segmentId, int laneIndex) {
			int ret = -1;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				NetInfo.Lane laneInfo = segment.Info.m_lanes[laneIndex];
				ret = (byte)(laneInfo.m_finalDirection & NetInfo.Direction.Forward) != 0 ? laneInfo.m_similarLaneIndex : laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1;
				return true;
			});

			return ret;
		}

		public int CalcOuterLaneSimilarIndex(ushort segmentId, int laneIndex) {
			int ret = -1;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				NetInfo.Lane laneInfo = segment.Info.m_lanes[laneIndex];
				ret = (byte)(laneInfo.m_finalDirection & NetInfo.Direction.Forward) != 0 ? laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1 : laneInfo.m_similarLaneIndex;
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
			ResetRoutingData(geometry.SegmentId);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			ResetRoutingData(geometry.SegmentId);

			try {
				Monitor.Enter(updateLock);

				updatedSegmentBuckets[geometry.SegmentId >> 6] |= 1uL << (int)geometry.SegmentId;
				segmentsUpdated = true;
			} finally {
				Monitor.Exit(updateLock);
			}
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
