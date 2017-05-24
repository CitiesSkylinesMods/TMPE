using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.AI;
using UnityEngine;
using TrafficManager.State;
using System.Threading;
using TrafficManager.Util;
using TrafficManager.Traffic;
using TrafficManager.Geometry;
using static TrafficManager.Traffic.PrioritySegment;
using CSUtil.Commons;

namespace TrafficManager.Manager {
	public class TrafficPriorityManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<int[]>>, ICustomDataManager<List<Configuration.PrioritySegment>> {
		public enum UnableReason {
			None,
			NoJunction,
			HasTimedLight,
			InvalidSegment,
			NotIncoming
		}

		public static readonly TrafficPriorityManager Instance = new TrafficPriorityManager();

		public const float MAX_SQR_STOP_VELOCITY = 0.01f;
		public const float MAX_SQR_YIELD_VELOCITY = 0.09f;
		public const float MAX_YIELD_VELOCITY = 0.3f;

		/// <summary>
		/// List of segments that are connected to roads with timed traffic lights or priority signs. Index: segment id
		/// </summary>
		private PrioritySegment[] PrioritySegments = null;

		private TrafficPriorityManager() {
			PrioritySegments = new PrioritySegment[NetManager.MAX_SEGMENT_COUNT];
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Priority signs:");
			for (int i = 0; i < PrioritySegments.Length; ++i) {
				if (PrioritySegments[i].IsDefault()) {
					continue;
				}
				Log._Debug($"Segment {i}: {PrioritySegments[i]}");
			}
		}

		public bool MayNodeHavePrioritySigns(ushort nodeId) {
			UnableReason reason;
			return MayNodeHavePrioritySigns(nodeId, out reason);
		}

		public bool MayNodeHavePrioritySigns(ushort nodeId, out UnableReason reason) {
			if (!Services.NetService.CheckNodeFlags(nodeId, NetNode.Flags.Created | NetNode.Flags.Deleted | NetNode.Flags.Junction, NetNode.Flags.Created | NetNode.Flags.Junction)) {
				reason = UnableReason.NoJunction;
				//Log._Debug($"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, result=false, reason={reason}");
				return false;
			}

			if (TrafficLightSimulationManager.Instance.HasTimedSimulation(nodeId)) {
				reason = UnableReason.HasTimedLight;
				//Log._Debug($"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, result=false, reason={reason}");
				return false;
			}

			//Log._Debug($"TrafficPriorityManager.MayNodeHavePrioritySigns: nodeId={nodeId}, result=true");
			reason = UnableReason.None;
			return true;
		}

		public bool MaySegmentHavePrioritySign(ushort segmentId, bool startNode) {
			UnableReason reason;
			return MaySegmentHavePrioritySign(segmentId, startNode, out reason);
		}

		public bool MaySegmentHavePrioritySign(ushort segmentId, bool startNode, out UnableReason reason) {
			if (! Services.NetService.IsSegmentValid(segmentId)) {
				reason = UnableReason.InvalidSegment;
				Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, startNode={startNode}, result=false, reason={reason}");
				return false;
			}

			if (! MayNodeHavePrioritySigns(Services.NetService.GetSegmentNodeId(segmentId, startNode), out reason)) {
				Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, startNode={startNode}, result=false, reason={reason}");
				return false;
			}

			SegmentEndGeometry endGeo = SegmentGeometry.Get(segmentId)?.GetEnd(startNode);

			if (endGeo.OutgoingOneWay) {
				reason = UnableReason.NotIncoming;
				Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, startNode={startNode}, result=false, reason={reason}");
				return false;
			}

			Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, startNode={startNode}, result=true");
			reason = UnableReason.None;
			return true;
		}

		public bool MaySegmentHavePrioritySign(ushort segmentId) {
			UnableReason reason;
			return MaySegmentHavePrioritySign(segmentId, out reason);
		}

		public bool MaySegmentHavePrioritySign(ushort segmentId, out UnableReason reason) {
			if (!Services.NetService.IsSegmentValid(segmentId)) {
				reason = UnableReason.InvalidSegment;
				Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, result=false, reason={reason}");
				return false;
			}

			bool ret =
				(MaySegmentHavePrioritySign(segmentId, true, out reason) ||
				MaySegmentHavePrioritySign(segmentId, false, out reason));
			Log._Debug($"TrafficPriorityManager.MaySegmentHavePrioritySign: segmentId={segmentId}, result={ret}, reason={reason}");
			return ret;
		}

		public bool HasSegmentPrioritySign(ushort segmentId) {
			return !PrioritySegments[segmentId].IsDefault();
		}

		public bool HasSegmentPrioritySign(ushort segmentId, bool startNode) {
			return PrioritySegments[segmentId].HasPrioritySignAtNode(startNode);
		}

		public bool HasNodePrioritySign(ushort nodeId) {
			bool ret = false;
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
				if (HasSegmentPrioritySign(segmentId, nodeId == segment.m_startNode)) {
					ret = true;
					return false;
				}
				return true;
			});
			//Log._Debug($"TrafficPriorityManager.HasNodePrioritySign: nodeId={nodeId}, result={ret}");
			return ret;
		}

		public bool SetPrioritySign(ushort segmentId, bool startNode, PriorityType type) {
			UnableReason reason;
			return SetPrioritySign(segmentId, startNode, type, out reason);
		}

		public bool SetPrioritySign(ushort segmentId, bool startNode, PriorityType type, out UnableReason reason) {
			bool ret = true;
			reason = UnableReason.None;

			if (type != PriorityType.None &&
				! MaySegmentHavePrioritySign(segmentId, startNode, out reason)) {
				Log._Debug($"TrafficPriorityManager.SetPrioritySign: Segment {segmentId} @ {startNode} may not have a priority sign: {reason}");
				ret = false;
				type = PriorityType.None;
			}

			if (type != PriorityType.None) {
				SubscribeToSegmentGeometry(segmentId);
				SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
				if (segGeo == null) {
					Log.Error($"TrafficPriorityManager.SetPrioritySign: No geometry information available for segment {segmentId}");
					reason = UnableReason.InvalidSegment;
					return false;
				}

				ushort nodeId = segGeo.GetNodeId(startNode);
				Services.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
					TrafficLightManager.Instance.RemoveTrafficLight(nodeId, ref node);
					return true;
				});
			}

			if (startNode) {
				PrioritySegments[segmentId].startType = type;
			} else {
				PrioritySegments[segmentId].endType = type;
			}

			UnsubscribeFromSegmentGeometryIfRequired(segmentId);
			SegmentEndManager.Instance.UpdateSegmentEnd(segmentId, startNode);
			Log._Debug($"TrafficPriorityManager.SetPrioritySign: segmentId={segmentId}, startNode={startNode}, type={type}, result={ret}, reason={reason}");
			return ret;
		}

		public void RemovePrioritySignsFromNode(ushort nodeId) {
			Log._Debug($"TrafficPriorityManager.RemovePrioritySignsFromNode: nodeId={nodeId}");
			
			Services.NetService.IterateNodeSegments(nodeId, delegate(ushort segmentId, ref NetSegment segment) {
				RemovePrioritySignFromSegment(segmentId, nodeId == segment.m_startNode);
				return true;
			});
		}

		internal void RemovePrioritySignFromSegment(ushort segmentId, bool startNode) {
			Log._Debug($"TrafficPriorityManager.RemovePrioritySignFromSegment: segmentId={segmentId}, startNode={startNode}");

			if (startNode) {
				PrioritySegments[segmentId].startType = PriorityType.None;
			} else {
				PrioritySegments[segmentId].endType = PriorityType.None;
			}

			SegmentEndManager.Instance.UpdateSegmentEnd(segmentId, startNode);
			UnsubscribeFromSegmentGeometryIfRequired(segmentId);
		}

		internal void RemovePrioritySignsFromSegment(ushort segmentId) {
			Log._Debug($"TrafficPriorityManager.RemovePrioritySignsFromSegment: segmentId={segmentId}");

			PrioritySegments[segmentId].Reset();
			SegmentEndManager.Instance.UpdateSegmentEnd(segmentId, true);
			SegmentEndManager.Instance.UpdateSegmentEnd(segmentId, false);
			UnsubscribeFromSegmentGeometry(segmentId);
		}

		public PriorityType GetPrioritySign(ushort segmentId, bool startNode) {
			return startNode ? PrioritySegments[segmentId].startType : PrioritySegments[segmentId].endType;
		}

		public byte CountPrioritySignsAtNode(ushort nodeId, PriorityType sign) {
			byte ret = 0;
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
				if (GetPrioritySign(segmentId, segment.m_startNode == nodeId) == sign) {
					++ret;
				}
				return true;
			});
			Log._Debug($"TrafficPriorityManager.CountPrioritySignsAtNode: nodeId={nodeId}, sign={sign}, result={ret}");
			return ret;
		}

		/// <summary>
		/// Checks if a vehicle (the target vehicle) has to wait for other incoming vehicles at a junction with priority signs.
		/// </summary>
		/// <param name="vehicleId">target vehicle</param>
		/// <param name="vehicleData">target vehicle data</param>
		/// <param name="curPos">Current path unit the target vehicle is located at</param>
		/// <param name="nextPos">Next path unit the target vehicle will be located at</param>
		/// <returns>true if the target vehicle must wait for other vehicles, false otherwise</returns>
		public bool HasIncomingVehiclesWithHigherPriority(ushort vehicleId, ref Vehicle vehicleData, ref NetSegment curSeg, ushort transitNodeId, ref NetNode transitNode, ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
			VehicleManager vehManager = Singleton<VehicleManager>.instance; // TODO do not depend on VehicleManager

#if DEBUG
			bool debug = false; // transitNodeId == 27423;
			if (debug) {
				Log._Debug($"HasIncomingVehicles: ##### Checking vehicle {vehicleId} at node {transitNodeId}. Coming from seg. {curPos.m_segment}, lane {curPos.m_lane}, going to seg. {nextPos.m_segment}, lane {nextPos.m_lane}");
			}
#else
			bool debug = false;
#endif

			ushort curSegmentId = curPos.m_segment;
			bool curStartNode = curSeg.m_startNode == transitNodeId;
			
			PriorityType curSign = GetPrioritySign(curSegmentId, curStartNode);
			if (curSign == PriorityType.None) {
				return false;
			}
			bool curOnMain = curSign == PriorityType.Main;

			SegmentEnd curEnd = SegmentEndManager.Instance.GetSegmentEnd(curSegmentId, curStartNode);
			if (curEnd == null) {
#if DEBUG
				Log.Warning($"HasIncomingVehicles: No segment end found for segment {curSegmentId} @ {curStartNode}");
				return false;
#endif
			}

			if (! Services.VehicleService.IsVehicleValid(vehicleId)) {
				curEnd.RequestCleanup();
				return false;
			}

			Vector3 transitNodePos = transitNode.m_position;
			float targetTimeToTransitNode = Single.NaN;
			if (Options.simAccuracy <= 1) {
				Vector3 targetToNode = transitNodePos - vehicleData.GetLastFramePosition();
				Vector3 targetVel = vehicleData.GetLastFrameVelocity();
				float targetSpeed = targetVel.magnitude;
				float targetDistanceToTransitNode = targetToNode.magnitude;

				if (targetSpeed > 0)
					targetTimeToTransitNode = targetDistanceToTransitNode / targetSpeed;
				else
					targetTimeToTransitNode = 0;
			}

			// iterate over all cars approaching the transit node and check if the target vehicle should be prioritized
			
			NodeGeometry transitNodeGeo = NodeGeometry.Get(transitNodeId);
			foreach (SegmentEndGeometry otherEndGeo in transitNodeGeo.SegmentEndGeometries) {
				if (otherEndGeo == null) {
					continue;
				}

				ushort otherSegmentId = otherEndGeo.SegmentId;
				if (otherSegmentId == curSegmentId) {
					continue;
				}

				bool otherStartNode = otherEndGeo.StartNode;
				if (otherEndGeo.OutgoingOneWay) {
					// not an incoming segment
					continue;
				}

				PriorityType otherSign = GetPrioritySign(otherSegmentId, otherStartNode);
				if (otherSign == PriorityType.None) {
					continue;
				}
				bool otherOnMain = otherSign == PriorityType.Main;

				SegmentEnd otherEnd = SegmentEndManager.Instance.GetSegmentEnd(otherSegmentId, otherStartNode);
				if (otherEnd == null) {
#if DEBUG
					Log.Error($"HasIncomingVehicles: No segment end found for other segment {otherSegmentId} @ {otherStartNode}");
#endif
					continue;
				}

				ushort otherVehicleId = otherEnd.FirstRegisteredVehicleId;
				while (otherVehicleId != 0) {
#if DEBUG
					if (debug) {
						Log._Debug("");
						Log._Debug($"HasIncomingVehicles: checking other vehicle {otherVehicleId} @ seg. {otherSegmentId}");
					}
#endif

					VehicleState otherState = VehicleStateManager.Instance._GetVehicleState(otherVehicleId);
					if (!otherState.Valid) {
#if DEBUG
						if (debug)
							Log._Debug($"HasIncomingVehicles: other vehicle {otherVehicleId}: state is invalid. *IGNORING*");
#endif
						otherVehicleId = otherState.NextVehicleIdOnSegment;
						continue;
					}

#if DEBUG
					if (debug)
						Log._Debug($"HasIncomingVehicles: Checking against other vehicle {otherVehicleId}.");
#endif

					if (otherState.JunctionTransitState != VehicleJunctionTransitState.None) {
						bool conflicting = false;
						otherState.ProcessCurrentAndNextPathPositionAndOtherVehicleCurrentAndNextPathPosition(ref vehManager.m_vehicles.m_buffer[otherVehicleId], ref curPos, ref nextPos, ref vehicleData,
								delegate (ref Vehicle otherVehicleData, ref PathUnit.Position incomingCurPos, ref PathUnit.Position incomingNextPos, ref Vehicle targetVehData,
								ref PathUnit.Position targetCurPos, ref PathUnit.Position targetNextPos) {
							bool incomingStateChangedRecently = otherState.IsJunctionTransitStateNew();
							if (
								otherState.JunctionTransitState == VehicleJunctionTransitState.Enter ||
								(otherState.JunctionTransitState == VehicleJunctionTransitState.Leave && incomingStateChangedRecently)) {
								// incoming vehicle is (1) entering the junction or (2) leaving but last state update ocurred very recently.
								Vector3 incomingPos = otherVehicleData.GetLastFramePosition();
								Vector3 incomingVel = otherVehicleData.GetLastFrameVelocity();
								Vector3 incomingToNode = transitNodePos - incomingPos;

								// check if incoming vehicle moves towards node
								float dot = Vector3.Dot(incomingToNode, incomingVel);
								if (dot <= 0) {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} is moving away from the transit node ({dot}). *IGNORING*");
#endif
									return;
								}
#if DEBUG
								if (debug)
									Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} is moving towards the transit node ({dot}). Distance: {incomingToNode.magnitude}");
#endif

								// check if estimated approach time of the incoming vehicle is within bounds (only if incoming vehicle is far enough away from the junction and target vehicle is moving)
								if (Options.simAccuracy <= 1 &&
										!Single.IsInfinity(targetTimeToTransitNode) &&
										!Single.IsNaN(targetTimeToTransitNode) &&
										incomingToNode.sqrMagnitude > GlobalConfig.Instance.MaxPriorityCheckSqrDist) {
									// check speeds
									float incomingSpeed = incomingVel.magnitude;
									float incomingDistanceToTransitNode = incomingToNode.magnitude;
									float incomingTimeToTransitNode = Single.NaN;

									if (incomingSpeed > 0)
										incomingTimeToTransitNode = incomingDistanceToTransitNode / incomingSpeed;
									else
										incomingTimeToTransitNode = Single.PositiveInfinity;

									float timeDiff = Mathf.Abs(incomingTimeToTransitNode - targetTimeToTransitNode);
									if (timeDiff > GlobalConfig.Instance.MaxPriorityApproachTime) {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} needs {incomingTimeToTransitNode} time units to get to the node where target needs {targetTimeToTransitNode} time units (diff = {timeDiff}). Difference to large. *IGNORING*");
#endif
										return;
									} else {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} needs {incomingTimeToTransitNode} time units to get to the node where target needs {targetTimeToTransitNode} time units (diff = {timeDiff}). Difference within bounds.");
#endif
									}
								} else {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Target is stopped.");
#endif
								}
							} else if (otherState.JunctionTransitState == VehicleJunctionTransitState.Leave) {
#if DEBUG
								if (debug)
									Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} is LEAVING but state update did not occur recently.");
#endif

								float incomingSqrSpeed = otherVehicleData.GetLastFrameVelocity().sqrMagnitude;
								if (incomingSqrSpeed <= MAX_SQR_STOP_VELOCITY) {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} is LEAVING but not moving. -> BLOCKED");
#endif
									otherState.JunctionTransitState = VehicleJunctionTransitState.Blocked;
									incomingStateChangedRecently = true;
								}
							}

							if (!incomingStateChangedRecently &&
								(otherState.JunctionTransitState == VehicleJunctionTransitState.Blocked ||
								(otherState.JunctionTransitState == VehicleJunctionTransitState.Stop && vehicleId < otherVehicleId))
							) {
#if DEBUG
								if (debug)
									Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} is BLOCKED and has waited a bit or is STOP and targetVehicleId {vehicleId} < incomingVehicleId {otherVehicleId}. *IGNORING*");
#endif

								// incoming vehicle waits because the junction is blocked and we waited a little. Allow target vehicle to enter the junciton.
								return;
							}

							// check priority rules
							if (HasVehiclePriority(debug, transitNodeId, vehicleId, ref targetVehData, ref targetCurPos, ref targetNextPos, curOnMain, otherVehicleId, ref otherVehicleData, ref incomingCurPos, ref incomingNextPos, otherOnMain, curEnd, otherEnd)) {
#if DEBUG
								if (debug)
									Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} is not conflicting.");
#endif
								return;
							} else {
#if DEBUG
								if (debug)
									Log._Debug($"==========> HasIncomingVehicles: Incoming {otherVehicleId} IS conflicting.");
#endif
								conflicting = true;
								return;
							}
						});

						if (conflicting) {
							// the target vehicle must wait
							return true;
						}
					} else {
#if DEBUG
						if (debug)
							Log._Debug($"HasIncomingVehicles: Incoming {otherVehicleId} (main) is not conflicting ({otherState.JunctionTransitState}).");
#endif
					}

					// check next incoming vehicle
					otherVehicleId = otherState.NextVehicleIdOnSegment;
				}
			}
			return false;
		}

		/// <summary>
		/// Implements priority checking for two vehicles approaching or waiting at a junction.
		/// </summary>
		/// <param name="debug"></param>
		/// <param name="transitNodeId">id of the junction</param>
		/// <param name="targetVehicleId">target vehicle for which priority is being checked</param>
		/// <param name="targetVehicleData">target vehicle data</param>
		/// <param name="targetCurPos">target vehicle current path position</param>
		/// <param name="targetNextPos">target vehicle next path position</param>
		/// <param name="targetIsOnMainRoad">true if the target vehicle is coming from a main road</param>
		/// <param name="incomingVehicleId">possibly conflicting incoming vehicle</param>
		/// <param name="incomingCurPos">incoming vehicle current path position</param>
		/// <param name="incomingNextPos">incoming vehicle next path position</param>
		/// <param name="incomingIsOnMainRoad">true if the incoming vehicle is coming from a main road</param>
		/// <param name="targetEnd">segment end the target vehicle is coming from</param>
		/// <param name="incomingEnd">segment end the incoming vehicle is coming from</param>
		/// <returns>true if the target vehicle has priority, false otherwise</returns>
		private bool HasVehiclePriority(bool debug, ushort transitNodeId, ushort targetVehicleId, ref Vehicle targetVehicleData, ref PathUnit.Position targetCurPos, ref PathUnit.Position targetNextPos, bool targetIsOnMainRoad, ushort incomingVehicleId, ref Vehicle incomingVehicleData, ref PathUnit.Position incomingCurPos, ref PathUnit.Position incomingNextPos, bool incomingIsOnMainRoad, SegmentEnd targetEnd, SegmentEnd incomingEnd) {
			try {
#if DEBUG
				//debug = targetEnd.NodeId == 16015;
				//debug = nodeId == 13531;
				if (debug) {
					Log._Debug("");
					Log._Debug($"  HasVehiclePriority: *** Checking if {targetVehicleId} (main road = {targetIsOnMainRoad}) @ (seg. {targetCurPos.m_segment}, lane {targetCurPos.m_lane}) -> (seg. {targetNextPos.m_segment}, lane {targetNextPos.m_lane}) has priority over {incomingVehicleId} (main road = {incomingIsOnMainRoad}) @ (seg. {incomingCurPos.m_segment}, lane {incomingCurPos.m_lane}) -> (seg. {incomingNextPos.m_segment}, lane {incomingNextPos.m_lane}).");
                }
#endif

				if (targetEnd.NodeId != incomingEnd.NodeId) {
					Log._Debug($"HasVehiclePriority: Incompatible SegmentEnds: targetEnd.NodeId={targetEnd.NodeId}, incomingEnd.NodeId={incomingEnd.NodeId}");
					return true;
				}

				ushort nodeId = targetEnd.NodeId;

				// delete invalid incoming car
				if ((incomingVehicleData.m_flags & Vehicle.Flags.Created) == 0) {
					incomingEnd.RequestCleanup();
					return true;
				}

				//var targetVehState = VehicleStateManager.GetVehicleState(targetCarId);
				//var incomingVehState = VehicleStateManager.GetVehicleState(incomingCarId);

				/*if (targetVehState == null) {
					targetEnd.RequestCleanup();
					return true;
				}*/

				if ((targetVehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
					// target vehicle is on emergency
					return true;
				}

				if ((targetVehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail && incomingVehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Monorail) || (incomingVehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail && targetVehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Monorail)) {
					// monorails and cars do not collide
					return true;
				}

				/*if (incomingVehState == null) {
					incomingEnd.RequestCleanup();
					return true;
				}*/

				//var targetVehPos = targetVehState.GetCurrentPosition();

				// check if target is on main road and incoming is on low-priority road
				if (targetIsOnMainRoad && !incomingIsOnMainRoad) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Target is on main road and incoming is not. Target HAS PRIORITY.");
                    }
#endif
					return true;
				}

				//         TOP
				//          |
				//          |
				// LEFT --- + --- RIGHT
				//          |
				//          |
				//        BOTTOM

				// We assume the target car is coming from BOTTOM.

				SegmentGeometry targetGeometry = SegmentGeometry.Get(targetCurPos.m_segment);
				if (targetGeometry == null) {
					Log.Error($"TrafficPriorityManager.SetPrioritySign: No geometry information available for segment {targetCurPos.m_segment}");
					return true;
				}
				SegmentGeometry incomingGeometry = SegmentGeometry.Get(incomingCurPos.m_segment);
				if (incomingGeometry == null) {
					Log.Error($"TrafficPriorityManager.SetPrioritySign: No geometry information available for segment {incomingCurPos.m_segment}");
					return true;
				}
				bool isTargetStartNode = targetGeometry.StartNodeId() == nodeId;
				ArrowDirection targetToDir = targetGeometry.GetDirection(targetNextPos.m_segment, isTargetStartNode); // target direction of target vehicle (relative to incoming direction of target vehicle)
				ArrowDirection incomingFromRelDir = targetGeometry.GetDirection(incomingCurPos.m_segment, isTargetStartNode); // incoming direction of incoming vehicle (relative to incoming direction of target vehicle)
				ArrowDirection incomingToDir = incomingGeometry.GetDirection(incomingNextPos.m_segment, incomingGeometry.StartNodeId() == nodeId); // target direction of incoming vehicle (relative to incoming direction of incoming vehicle)
#if DEBUG
				if (debug) {
					Log._Debug($"  HasVehiclePriority: targetToDir: {targetToDir.ToString()}, incomingRelDir: {incomingFromRelDir.ToString()}, incomingToDir: {incomingToDir.ToString()}");
                }
#endif

				if (targetToDir == ArrowDirection.None || incomingFromRelDir == ArrowDirection.None || incomingToDir == ArrowDirection.None) {
					return true;
				}

				if (Services.SimulationService.LeftHandDrive) {
					// mirror situation for left-hand traffic systems
					targetToDir = InvertLeftRight(targetToDir);
					incomingFromRelDir = InvertLeftRight(incomingFromRelDir);
					incomingToDir = InvertLeftRight(incomingToDir);
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: LHD! targetToDir: {targetToDir.ToString()}, incomingRelDir: {incomingFromRelDir.ToString()}, incomingToDir: {incomingToDir.ToString()}");
					}
#endif
				}

				bool sameTargets = false;
				bool laneOrderCorrect = false;
				if (targetNextPos.m_segment == incomingNextPos.m_segment) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Target and incoming are going to the same segment.");
					}
#endif

					// target and incoming are both going to same segment
					sameTargets = true;
					if (targetNextPos.m_lane == incomingNextPos.m_lane && targetCurPos.m_segment != incomingCurPos.m_segment) {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target and incoming are going to the same segment AND lane. lane order is incorrect!");
						}
#endif
						// both are going to the same lane. lane order is always incorrect
						laneOrderCorrect = false;
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target and incoming are going to the same segment BUT NOT to the same lane. Determining if lane order is correct.");
						}
#endif
						switch (targetToDir) {
							case ArrowDirection.Left:
								laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, targetNextPos.m_lane, incomingNextPos.m_lane); // stay left
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT. Checking if lane {targetNextPos.m_lane} is LEFT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
								}
#endif
								break;
							case ArrowDirection.Forward:
							default:
								switch (incomingFromRelDir) {
									case ArrowDirection.Left:
									case ArrowDirection.Forward:
										laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, incomingNextPos.m_lane, targetNextPos.m_lane); // stay right
#if DEBUG
										if (debug) {
											Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from LEFT or FORWARD ({incomingFromRelDir}). Checking if lane {targetNextPos.m_lane} is RIGHT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
										}
#endif
										break;
									case ArrowDirection.Right:
										laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, targetNextPos.m_lane, incomingNextPos.m_lane); // stay left
#if DEBUG
										if (debug) {
											Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from RIGHT. Checking if lane {targetNextPos.m_lane} is LEFT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
										}
#endif
										break;
									case ArrowDirection.Turn:
									default:
										laneOrderCorrect = true;
#if DEBUG
										if (debug) {
											Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from TURN (should not happen). Result: {laneOrderCorrect}");
										}
#endif
										break;
								}
								break;
							case ArrowDirection.Right:
								laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, incomingNextPos.m_lane, targetNextPos.m_lane); // stay right
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going RIGHT. Checking if lane {targetNextPos.m_lane} is RIGHT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
								}
#endif
								break;
						}
						//laneOrderCorrect = IsLaneOrderConflictFree(targetNextPos.m_segment, targetNextPos.m_lane, incomingNextPos.m_lane); // FIXME
#if DEBUG
						if (debug) {
							Log._Debug($"    HasVehiclePriority: >>> Lane order correct? {laneOrderCorrect}");
						}
#endif
					}
				}

				if (sameTargets && laneOrderCorrect) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Lane order between car {targetVehicleId} and {incomingVehicleId} is correct. Target HAS PRIORITY.");
					}
#endif
					return true;
				}

				if (!targetIsOnMainRoad && !incomingIsOnMainRoad) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Both target {targetVehicleId} and incoming {incomingVehicleId} are coming from a low-priority road.");
					}
#endif

					// the right-most vehicle has priority
					if (incomingFromRelDir == ArrowDirection.Left) {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Incoming comes from left. Target HAS PRIORITY!");
						}
#endif
						return true;
					} else if (incomingFromRelDir == ArrowDirection.Right) {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Incoming comes from right. Target MUST WAIT!");
						}
#endif
						return false;
					} else {
						// turn/forward
					}
				}
				bool incomingCrossingStreet = incomingToDir == ArrowDirection.Forward || incomingToDir == ArrowDirection.Left;

#if DEBUG
				if (debug) {
					Log._Debug($"  HasVehiclePriority: !!!!!! Lane order is INCORRECT or both are NOT GOING TO SAME target. incomingCrossingStreet={incomingCrossingStreet}");
				}
#endif

				bool ret;
				switch (targetToDir) {
					case ArrowDirection.Right:
						// target: BOTTOM->RIGHT
						if ((!targetIsOnMainRoad && incomingIsOnMainRoad) && sameTargets && !laneOrderCorrect) {
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going RIGHT and is on low-priority road turning right. the other vehicle is on a priority road. target MUST WAIT.");
							}
#endif
							return false; // vehicle must wait for incoming vehicle on priority road
						}

#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target is going RIGHT without conflict (targetIsOnMainRoad={targetIsOnMainRoad}, incomingIsOnMainRoad={incomingIsOnMainRoad}, sameTargets={sameTargets}, laneOrderCorrect={laneOrderCorrect}). target HAS PRIORITY.");
                        }
#endif

						return true;
					case ArrowDirection.Forward:
					default:
						// target: BOTTOM->TOP
						switch (incomingFromRelDir) {
							case ArrowDirection.Right:
								ret = !incomingIsOnMainRoad && !incomingCrossingStreet;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from RIGHT. incomingIsOnMainRoad={incomingIsOnMainRoad}, incomingCrossingStreet={incomingCrossingStreet}, result={ret}");
								}
#endif
								return ret;
							case ArrowDirection.Left:
								ret = targetIsOnMainRoad || !incomingCrossingStreet; // TODO check
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from LEFT. targetIsOnMainRoad={targetIsOnMainRoad}, incomingCrossingStreet={incomingCrossingStreet}, result={ret}");
								}
#endif
								return ret;
							case ArrowDirection.Forward:
							default:
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from FORWARD. result=True");
								}
#endif
								return true;
						}
					case ArrowDirection.Left:
						// target: BOTTOM->LEFT
						switch (incomingFromRelDir) {
							case ArrowDirection.Right:
								ret = !incomingCrossingStreet;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from RIGHT. incomingCrossingStreet={incomingCrossingStreet}. result={ret}");
                                }
#endif
								return ret;
							case ArrowDirection.Left:
								if (targetIsOnMainRoad && incomingIsOnMainRoad) { // bent priority road
									ret = true;
								} else {
									ret = !incomingCrossingStreet;
								}
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from LEFT. targetIsOnMainRoad={targetIsOnMainRoad}, incomingIsOnMainRoad={incomingIsOnMainRoad}, incomingCrossingStreet={incomingCrossingStreet}. result={ret}");
								}
#endif
								return ret;
							case ArrowDirection.Forward:
							default:
								ret = incomingToDir == ArrowDirection.Left || incomingToDir == ArrowDirection.Turn;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from FORWARD. incomingToDir={incomingToDir}. result={ret}");
								}
#endif
								return ret;
						}
				}
			} catch (Exception e) {
				Log.Error("Error occured: " + e.ToString());
			}

#if DEBUG
			if (debug) {
				Log._Debug($"  HasVehiclePriority: ALL CHECKS FAILED. returning FALSE.");
			}
#endif
			return false;
		}

		private static ArrowDirection InvertLeftRight(ArrowDirection dir) { // TODO move to Util
			if (dir == ArrowDirection.Left)
				dir = ArrowDirection.Right;
			else if (dir == ArrowDirection.Right)
				dir = ArrowDirection.Left;
			return dir;
		}

		/// <summary>
		/// Checks if lane <paramref name="leftLaneIndex"/> lies to the left of lane <paramref name="rightLaneIndex"/>.
		/// </summary>
		/// <param name="debug"></param>
		/// <param name="segmentId"></param>
		/// <param name="nodeId"></param>
		/// <param name="leftLaneIndex"></param>
		/// <param name="rightLaneIndex"></param>
		/// <returns></returns>
		public bool IsLaneOrderConflictFree(bool debug, ushort segmentId, ushort nodeId, byte leftLaneIndex, byte rightLaneIndex) { // TODO I think this is incorrect. See TrafficLightTool._guiLaneChangeWindow
			try {
				if (leftLaneIndex == rightLaneIndex) {
					return false;
				}

				NetManager netManager = Singleton<NetManager>.instance;

				NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

				NetInfo.Direction dir = nodeId == netManager.m_segments.m_buffer[segmentId].m_startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				NetInfo.Direction dir2 = ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
				NetInfo.Direction dir3 = Services.SimulationService.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

				NetInfo.Lane leftLane = segmentInfo.m_lanes[leftLaneIndex];
				NetInfo.Lane rightLane = segmentInfo.m_lanes[rightLaneIndex];

#if DEBUG
				if (debug) {
					Log._Debug($"    IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): dir={dir}, dir2={dir2}, dir3={dir3} laneDir={leftLane.m_direction}, leftLanePos={leftLane.m_position}, rightLanePos={rightLane.m_position}");
				}
#endif

				bool ret = (dir3 == NetInfo.Direction.Forward) ^ (leftLane.m_position < rightLane.m_position);
				return ret;
			} catch (Exception e) {
				Log.Error($"IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): Error: {e.ToString()}");
            }
			return true;
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			RemovePrioritySignsFromSegment(geometry.SegmentId);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			if (! MaySegmentHavePrioritySign(geometry.SegmentId, true)) {
				RemovePrioritySignFromSegment(geometry.SegmentId, true);
			} else {
				UpdateNode(geometry.StartNodeId());
			}

			if (!MaySegmentHavePrioritySign(geometry.SegmentId, false)) {
				RemovePrioritySignFromSegment(geometry.SegmentId, false);
			} else {
				UpdateNode(geometry.EndNodeId());
			}
		}

		protected void UpdateNode(ushort nodeId) {
			UnableReason reason;
			if (! MayNodeHavePrioritySigns(nodeId, out reason)) {
				RemovePrioritySignsFromNode(nodeId);
				return;
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < PrioritySegments.Length; ++i) {
				RemovePrioritySignsFromSegment((ushort)i);
			}
		}

		protected void UnsubscribeFromSegmentGeometryIfRequired(ushort segmentId) {
			if (! HasSegmentPrioritySign(segmentId)) {
				UnsubscribeFromSegmentGeometry(segmentId);
			}
		}

		[Obsolete]
		public bool LoadData(List<int[]> data) {
			bool success = true;
			Log.Info($"Loading {data.Count} priority segments (old method)");
			foreach (var segment in data) {
				try {
					if (segment.Length < 3)
						continue;

					if ((PriorityType)segment[2] == PriorityType.None) {
						continue;
					}

					ushort nodeId = (ushort)segment[0];
					ushort segmentId = (ushort)segment[1];
					PriorityType sign = (PriorityType)segment[2];

					if (!Services.NetService.IsNodeValid(nodeId)) {
						continue;
					}
					if (!Services.NetService.IsSegmentValid(segmentId)) {
						continue;
					}

					SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
					if (segGeo == null) {
						Log.Error($"TrafficPriorityManager.LoadData: No geometry information available for segment {segmentId}");
						continue;
					}
					bool startNode = segGeo.StartNodeId() == nodeId;

					SetPrioritySign(segmentId, startNode, sign);
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading data from Priority segments: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		[Obsolete]
		public List<int[]> SaveData(ref bool success) {
			return null;
		}

		public bool LoadData(List<Configuration.PrioritySegment> data) {
			bool success = true;
			Log.Info($"Loading {data.Count} priority segments (new method)");
			foreach (var prioSegData in data) {
				try {
					if ((PriorityType)prioSegData.priorityType == PriorityType.None) {
						continue;
					}
					if (!Services.NetService.IsNodeValid(prioSegData.nodeId)) {
						continue;
					}
					if (!Services.NetService.IsSegmentValid(prioSegData.segmentId)) {
						continue;
					}

					SegmentGeometry segGeo = SegmentGeometry.Get(prioSegData.segmentId);
					if (segGeo == null) {
						Log.Error($"TrafficPriorityManager.SaveData: No geometry information available for segment {prioSegData.segmentId}");
						continue;
					}
					bool startNode = segGeo.StartNodeId() == prioSegData.nodeId;

					SetPrioritySign(prioSegData.segmentId, startNode, (PriorityType)prioSegData.priorityType);
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading data from Priority segments: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		List<Configuration.PrioritySegment> ICustomDataManager<List<Configuration.PrioritySegment>>.SaveData(ref bool success) {
			List<Configuration.PrioritySegment> ret = new List<Configuration.PrioritySegment>();
			for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
				try {
					if (! Services.NetService.IsSegmentValid(segmentId) || ! HasSegmentPrioritySign(segmentId)) {
						continue;
					}
					SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
					if (segGeo == null) {
						Log.Error($"TrafficPriorityManager.SaveData: No geometry information available for segment {segmentId}");
						continue;
					}

					PriorityType startSign = GetPrioritySign(segmentId, true);
					if (startSign != PriorityType.None) {
						ushort startNodeId = segGeo.StartNodeId();
						if (Services.NetService.IsNodeValid(startNodeId)) {
							Log._Debug($"Saving priority sign of type {startSign} @ start node {startNodeId} of segment {segmentId}");
							ret.Add(new Configuration.PrioritySegment(segmentId, startNodeId, (int)startSign));
						}
					}

					PriorityType endSign = GetPrioritySign(segmentId, false);
					if (endSign != PriorityType.None) {
						ushort endNodeId = segGeo.EndNodeId();
						if (Services.NetService.IsNodeValid(endNodeId)) {
							Log._Debug($"Saving priority sign of type {endSign} @ end node {endNodeId} of segment {segmentId}");
							ret.Add(new Configuration.PrioritySegment(segmentId, endNodeId, (int)endSign));
						}
					}
				} catch (Exception e) {
					Log.Error($"Exception occurred while saving priority segment @ seg. {segmentId}: {e.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}
