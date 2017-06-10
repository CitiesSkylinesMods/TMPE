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
		/// <param name="vehicle">target vehicle data</param>
		/// <returns>true if the target vehicle must wait for other vehicles, false otherwise</returns>
		public bool HasPriority(ushort vehicleId, ref Vehicle vehicle, ref VehicleState state, ref NetNode transitNode) {
			SegmentEndGeometry endGeo = SegmentGeometry.Get(state.currentSegmentId)?.GetEnd(state.currentStartNode);
			if (endGeo == null) {
#if DEBUG
				Log.Warning($"TrafficPriorityManager.HasPriority({vehicleId}): No segment end geometry found for segment {state.currentSegmentId} @ {state.currentStartNode}");
				return true;
#endif
			}

			SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(state.currentSegmentId, state.currentStartNode);
			if (end == null) {
#if DEBUG
				Log.Warning($"TrafficPriorityManager.HasPriority({vehicleId}): No segment end found for segment {state.currentSegmentId} @ {state.currentStartNode}");
				return true;
#endif
			}
			ushort transitNodeId = end.NodeId;

#if DEBUG
			bool debug = GlobalConfig.Instance.DebugSwitches[13] && (GlobalConfig.Instance.TTLDebugNodeId <= 0 || transitNodeId == GlobalConfig.Instance.TTLDebugNodeId);
			if (debug) {
				Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): Checking vehicle {vehicleId} at node {transitNodeId}. Coming from seg. {state.currentSegmentId}, start {state.currentStartNode}, lane {state.currentLaneIndex}, going to seg. {state.nextSegmentId}, lane {state.nextLaneIndex}\nstate: {state}");
			}
#else
			bool debug = false;
#endif

			if (! state.spawned) {
#if DEBUG
				if (debug)
					Log.Warning($"TrafficPriorityManager.HasPriority({vehicleId}): Vehicle is not spawned.");
#endif
				return true;
			}

			if ((vehicle.m_flags & Vehicle.Flags.Emergency2) != 0) {
				// target vehicle is on emergency
#if DEBUG
				if (debug)
					Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): Vehicle is on emergency.");
#endif
				return true;
			}

			if (vehicle.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail) {
				// monorails do not obey priority signs
#if DEBUG
				if (debug)
					Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): Vehicle is a monorail.");
#endif
				return true;
			}
			
			PriorityType curSign = GetPrioritySign(state.currentSegmentId, state.currentStartNode);
			if (curSign == PriorityType.None) {
#if DEBUG
				if (debug)
					Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): Sign is None @ seg. {state.currentSegmentId}, start {state.currentStartNode}");
#endif
				return true;
			}
			bool onMain = curSign == PriorityType.Main;

			/*if (! Services.VehicleService.IsVehicleValid(vehicleId)) {
				curEnd.RequestCleanup();
				return true;
			}*/

			// calculate approx. time after which the transit node will be reached
			float targetTimeToTransitNode = Single.NaN;
			if (Options.simAccuracy <= 1) {
				Vector3 targetToNode = transitNode.m_position - vehicle.GetLastFramePosition();
				Vector3 targetVel = vehicle.GetLastFrameVelocity();
				float targetSpeed = targetVel.magnitude;
				float targetDistanceToTransitNode = targetToNode.magnitude;

				if (targetSpeed > 0)
					targetTimeToTransitNode = targetDistanceToTransitNode / targetSpeed;
				else
					targetTimeToTransitNode = 0;
			}

#if DEBUG
			if (debug)
				Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): estimated target time to transit node {transitNodeId} is {targetTimeToTransitNode} for vehicle {vehicleId}");
#endif

			ArrowDirection targetToDir = endGeo.GetDirection(state.nextSegmentId); // target direction of target vehicle (relative to incoming direction of target vehicle)

			// iterate over all cars approaching the transit node and check if the target vehicle should be prioritized
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

			NodeGeometry transitNodeGeo = NodeGeometry.Get(transitNodeId);
			foreach (SegmentEndGeometry otherEndGeo in transitNodeGeo.SegmentEndGeometries) {
				if (otherEndGeo == null) {
					continue;
				}

				ushort otherSegmentId = otherEndGeo.SegmentId;
				if (otherSegmentId == state.currentSegmentId) {
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
				bool incomingOnMain = otherSign == PriorityType.Main;

				SegmentEnd incomingEnd = SegmentEndManager.Instance.GetSegmentEnd(otherSegmentId, otherStartNode);
				if (incomingEnd == null) {
#if DEBUG
					Log.Error($"TrafficPriorityManager.HasPriority({vehicleId}): No segment end found for other segment {otherSegmentId} @ {otherStartNode}");
#endif
					continue;
				}

				ArrowDirection incomingFromRelDir = endGeo.GetDirection(otherSegmentId); // incoming direction of incoming vehicle (relative to incoming direction of target vehicle)

#if DEBUG
				if (debug)
					Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): checking other segment {otherSegmentId} @ {transitNodeId}");
#endif

				ushort incomingVehicleId = incomingEnd.FirstRegisteredVehicleId;
				while (incomingVehicleId != 0) {
#if DEBUG
					if (debug) {
						Log._Debug("");
						Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): checking other vehicle {incomingVehicleId} @ seg. {otherSegmentId}");
					}
#endif
					if (IsConflictingVehicle(debug, transitNodeId, transitNode.m_position, targetTimeToTransitNode, vehicleId, ref vehicle, ref state, onMain, endGeo, targetToDir, incomingVehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingVehicleId], ref vehStateManager.VehicleStates[incomingVehicleId], incomingOnMain, otherEndGeo, incomingFromRelDir)) {
#if DEBUG
						if (debug) {
							Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): incoming vehicle {incomingVehicleId} is conflicting.");
						}
#endif
						return false;
					}

					// check next incoming vehicle
					incomingVehicleId = vehStateManager.VehicleStates[incomingVehicleId].nextVehicleIdOnSegment;
				}
			}
#if DEBUG
			if (debug) {
				Log._Debug($"TrafficPriorityManager.HasPriority({vehicleId}): No conflicting incoming vehicles found.");
			}
#endif
			return true;
		}

		private bool IsConflictingVehicle(bool debug, ushort transitNodeId, Vector3 transitNodePos, float targetTimeToTransitNode, ushort vehicleId, ref Vehicle vehicle, ref VehicleState state, bool onMain, SegmentEndGeometry endGeo, ArrowDirection targetToDir, ushort incomingVehicleId, ref Vehicle incomingVehicle, ref VehicleState incomingState, bool incomingOnMain, SegmentEndGeometry incomingEndGeo, ArrowDirection incomingFromRelDir) {
#if DEBUG
			if (debug) {
				Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Checking against other vehicle {incomingVehicleId}.");
				Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): TARGET is coming from seg. {state.currentSegmentId}, start {state.currentStartNode}, lane {state.currentLaneIndex}, going to seg. {state.nextSegmentId}, lane {state.nextLaneIndex}\nstate: {state}");
				Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): INCOMING is coming from seg. {incomingState.currentSegmentId}, start {incomingState.currentStartNode}, lane {incomingState.currentLaneIndex}, going to seg. {incomingState.nextSegmentId}, lane {incomingState.nextLaneIndex}\nincoming state: {incomingState}");
			}
#endif

			if (!incomingState.spawned) {
#if DEBUG
				if (debug)
					Log.Warning($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming vehicle is not spawned.");
#endif
				return false;
			}

			if (incomingVehicle.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail) {
				// monorails and cars do not collide
#if DEBUG
				if (debug) {
					Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming vehicle is a monorail.");
				}
#endif
				return false;
			}

			if (incomingState.JunctionTransitState != VehicleJunctionTransitState.None) {
				Vector3 incomingVel = incomingVehicle.GetLastFrameVelocity();
				bool incomingStateChangedRecently = incomingState.IsJunctionTransitStateNew();
				if (incomingState.JunctionTransitState == VehicleJunctionTransitState.Approach ||
					incomingState.JunctionTransitState == VehicleJunctionTransitState.Leave
				) {
					float incomingSqrSpeed = incomingVel.sqrMagnitude;
					if (!incomingStateChangedRecently && incomingSqrSpeed <= MAX_SQR_STOP_VELOCITY) {
#if DEBUG
						if (debug)
							Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is LEAVING or APPROACHING but not moving. -> BLOCKED");
#endif
						incomingState.JunctionTransitState = VehicleJunctionTransitState.Blocked;
						incomingStateChangedRecently = true;
						return false;
					}

					// incoming vehicle is (1) entering the junction or (2) leaving
					Vector3 incomingPos = incomingVehicle.GetLastFramePosition();
					Vector3 incomingToNode = transitNodePos - incomingPos;

					// check if incoming vehicle moves towards node
					float dot = Vector3.Dot(incomingToNode, incomingVel);
					if (dot <= 0) {
#if DEBUG
						if (debug)
							Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is moving away from the transit node ({dot}). *IGNORING*");
#endif
						return false;
					}
#if DEBUG
					if (debug)
						Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is moving towards the transit node ({dot}). Distance: {incomingToNode.magnitude}");
#endif

					// check if estimated approach time of the incoming vehicle is within bounds (only if incoming vehicle is far enough away from the junction and target vehicle is moving)
					if (Options.simAccuracy <= 1 &&
						!Single.IsInfinity(targetTimeToTransitNode) &&
						!Single.IsNaN(targetTimeToTransitNode) &&
						incomingToNode.sqrMagnitude > GlobalConfig.Instance.MaxPriorityCheckSqrDist
					) {

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
								Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} needs {incomingTimeToTransitNode} time units to get to the node where target needs {targetTimeToTransitNode} time units (diff = {timeDiff}). Difference to large. *IGNORING*");
#endif
							return false;
						} else {
#if DEBUG
							if (debug)
								Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} needs {incomingTimeToTransitNode} time units to get to the node where target needs {targetTimeToTransitNode} time units (diff = {timeDiff}). Difference within bounds. Priority check required.");
#endif
						}
					} else {
#if DEBUG
						if (debug)
							Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming is stopped.");
#endif
					}
				}

				if (!incomingStateChangedRecently &&
					(incomingState.JunctionTransitState == VehicleJunctionTransitState.Blocked ||
					(incomingState.JunctionTransitState == VehicleJunctionTransitState.Stop && vehicleId < incomingVehicleId))
				) {
#if DEBUG
					if (debug)
						Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is BLOCKED and has waited a bit or is STOP and targetVehicleId {vehicleId} < incomingVehicleId {incomingVehicleId}. *IGNORING*");
#endif

					// incoming vehicle waits because the junction is blocked and we waited a little. Allow target vehicle to enter the junciton.
					return false;
				}

				// check priority rules
				ArrowDirection incomingToDir = incomingEndGeo.GetDirection(incomingState.nextSegmentId); // target direction of incoming vehicle (relative to incoming direction of incoming vehicle)

				if (HasVehiclePriority(debug, transitNodeId, vehicleId, ref vehicle, ref state, onMain, targetToDir, incomingVehicleId, ref incomingVehicle, ref incomingState, incomingOnMain, incomingFromRelDir, incomingToDir)) {
#if DEBUG
					if (debug)
						Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} is not conflicting.");
#endif
					return false;
				} else {
#if DEBUG
					if (debug)
						Log._Debug($"==========> TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} IS conflicting.");
#endif
					return true;
				}
			} else {
#if DEBUG
				if (debug)
					Log._Debug($"TrafficPriorityManager.IsConflictingVehicle({vehicleId}, {incomingVehicleId}): Incoming {incomingVehicleId} (main) is not conflicting ({incomingState.JunctionTransitState}).");
#endif
				return false;
			}
		}

		/// <summary>
		/// Implements priority checking for two vehicles approaching or waiting at a junction.
		/// </summary>
		/// <param name="debug"></param>
		/// <param name="transitNodeId">id of the junction</param>
		/// <param name="vehicleId">target vehicle for which priority is being checked</param>
		/// <param name="vehicle">target vehicle data</param>
		/// <param name="targetCurPos">target vehicle current path position</param>
		/// <param name="targetNextPos">target vehicle next path position</param>
		/// <param name="onMain">true if the target vehicle is coming from a main road</param>
		/// <param name="incomingVehicleId">possibly conflicting incoming vehicle</param>
		/// <param name="incomingCurPos">incoming vehicle current path position</param>
		/// <param name="incomingNextPos">incoming vehicle next path position</param>
		/// <param name="incomingOnMain">true if the incoming vehicle is coming from a main road</param>
		/// <returns>true if the target vehicle has priority, false otherwise</returns>
		private bool HasVehiclePriority(bool debug, ushort transitNodeId, ushort vehicleId, ref Vehicle vehicle, ref VehicleState state, bool onMain, ArrowDirection targetToDir, ushort incomingVehicleId, ref Vehicle incomingVehicle, ref VehicleState incomingState, bool incomingOnMain, ArrowDirection incomingFromRelDir, ArrowDirection incomingToDir) {
#if DEBUG
			if (debug) {
				Log._Debug("");
				Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): *** Checking if vehicle {vehicleId} (main road = {onMain}) @ (seg. {state.currentSegmentId}, start {state.currentStartNode}, lane {state.currentLaneIndex}) -> (seg. {state.nextSegmentId}, lane {state.nextLaneIndex}) has priority over {incomingVehicleId} (main road = {incomingOnMain}) @ (seg. {incomingState.currentSegmentId}, start {incomingState.currentStartNode}, lane {incomingState.currentLaneIndex}) -> (seg. {incomingState.nextSegmentId}, lane {incomingState.nextLaneIndex}).");
            }
#endif

			// check if target is on main road and incoming is on low-priority road
			if (onMain && !incomingOnMain) {
#if DEBUG
				if (debug) {
					Log._Debug($"  TrafficPriorityManager.HasVehiclePriority({vehicleId}, {incomingVehicleId}): Target is on main road and incoming is not. Target HAS PRIORITY.");
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
			bool incomingIsLeaving = incomingState.JunctionTransitState == VehicleJunctionTransitState.Leave;
			if (state.nextSegmentId == incomingState.nextSegmentId) {
#if DEBUG
				if (debug) {
					Log._Debug($"  HasVehiclePriority: Target and incoming are going to the same segment.");
				}
#endif

				// target and incoming are both going to same segment
				sameTargets = true;
				if (state.nextLaneIndex == incomingState.nextLaneIndex && state.currentSegmentId != incomingState.currentSegmentId) {
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
							laneOrderCorrect = IsLaneOrderConflictFree(debug, state.nextSegmentId, transitNodeId, state.nextLaneIndex, incomingState.nextLaneIndex); // stay left
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going LEFT. Checking if lane {state.nextLaneIndex} is LEFT to {incomingState.nextLaneIndex}. Result: {laneOrderCorrect}");
							}
#endif
							break;
						case ArrowDirection.Forward:
						default:
							switch (incomingFromRelDir) {
								case ArrowDirection.Left:
								case ArrowDirection.Forward:
									laneOrderCorrect = IsLaneOrderConflictFree(debug, state.nextSegmentId, transitNodeId, incomingState.nextLaneIndex, state.nextLaneIndex); // stay right
#if DEBUG
									if (debug) {
										Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from LEFT or FORWARD ({incomingFromRelDir}). Checking if lane {state.nextLaneIndex} is RIGHT to {incomingState.nextLaneIndex}. Result: {laneOrderCorrect}");
									}
#endif
									break;
								case ArrowDirection.Right:
									laneOrderCorrect = IsLaneOrderConflictFree(debug, state.nextSegmentId, transitNodeId, state.nextLaneIndex, incomingState.nextLaneIndex); // stay left
#if DEBUG
									if (debug) {
										Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from RIGHT. Checking if lane {state.nextLaneIndex} is LEFT to {incomingState.nextLaneIndex}. Result: {laneOrderCorrect}");
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
							laneOrderCorrect = IsLaneOrderConflictFree(debug, state.nextSegmentId, transitNodeId, incomingState.nextLaneIndex, state.nextLaneIndex); // stay right
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going RIGHT. Checking if lane {state.nextLaneIndex} is RIGHT to {incomingState.nextLaneIndex}. Result: {laneOrderCorrect}");
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
					Log._Debug($"  HasVehiclePriority: Lane order between car {vehicleId} and {incomingVehicleId} is correct. Target HAS PRIORITY.");
				}
#endif
				return true;
			}

			if (!onMain && !incomingOnMain) {
#if DEBUG
				if (debug) {
					Log._Debug($"  HasVehiclePriority: Both target {vehicleId} and incoming {incomingVehicleId} are coming from a low-priority road.");
				}
#endif

				// the right-most vehicle has priority
				if (incomingFromRelDir == ArrowDirection.Left) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Incoming comes from left. Target HAS PRIORITY!");
					}
#endif
					return !incomingIsLeaving;
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
					if ((!onMain && incomingOnMain) && sameTargets && !laneOrderCorrect) {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target is going RIGHT and is on low-priority road turning right. the other vehicle is on a priority road. target MUST WAIT.");
						}
#endif
						ret = false; // vehicle must wait for incoming vehicle on priority road
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target is going RIGHT without conflict (targetIsOnMainRoad={onMain}, incomingIsOnMainRoad={incomingOnMain}, sameTargets={sameTargets}, laneOrderCorrect={laneOrderCorrect}). target HAS PRIORITY.");
						}
#endif

						ret = true;
					}
					break;
				case ArrowDirection.Forward:
				default:
					// target: BOTTOM->TOP
					switch (incomingFromRelDir) {
						case ArrowDirection.Right:
							ret = !incomingOnMain && !incomingCrossingStreet;
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from RIGHT. incomingIsOnMainRoad={incomingOnMain}, incomingCrossingStreet={incomingCrossingStreet}, result={ret}");
							}
#endif
							break;
						case ArrowDirection.Left:
							ret = onMain || !incomingCrossingStreet; // TODO check
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from LEFT. targetIsOnMainRoad={onMain}, incomingCrossingStreet={incomingCrossingStreet}, result={ret}");
							}
#endif
							break;
						case ArrowDirection.Forward:
						default:
							ret = true;
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from FORWARD. result=True");
							}
#endif
							break;
					}
					break;
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
							break;
						case ArrowDirection.Left:
							if (onMain && incomingOnMain) { // bent priority road
								ret = true;
							} else {
								ret = !incomingCrossingStreet;
							}
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from LEFT. targetIsOnMainRoad={onMain}, incomingIsOnMainRoad={incomingOnMain}, incomingCrossingStreet={incomingCrossingStreet}. result={ret}");
							}
#endif
							break;
						case ArrowDirection.Forward:
						default:
							ret = incomingToDir == ArrowDirection.Left || incomingToDir == ArrowDirection.Turn;
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from FORWARD. incomingToDir={incomingToDir}. result={ret}");
							}
#endif
							break;
					}
					break;
			}

			if (ret) {
				return !incomingIsLeaving;
			} else {
#if DEBUG
				if (debug) {
					Log._Debug($"  HasVehiclePriority: ALL CHECKS FAILED. returning FALSE.");
				}
#endif

				return false;
			}
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
