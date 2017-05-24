using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.TrafficLight;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	/// <summary>
	/// Manages traffic light toggling
	/// </summary>
	public class TrafficLightManager : AbstractCustomManager, ICustomDataManager<List<Configuration.NodeTrafficLight>>, ICustomDataManager<string> {
		public enum UnableReason {
			None,
			NoJunction,
			HasTimedLight,
			InsufficientSegments
		}

		public static readonly TrafficLightManager Instance = new TrafficLightManager();

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"- Not implemented -");
			// TODO implement
		}

		public bool SetTrafficLight(ushort nodeId, bool flag, ref NetNode node) {
			UnableReason reason;
			return SetTrafficLight(nodeId, flag, ref node, out reason);
		}

		public bool SetTrafficLight(ushort nodeId, bool flag, ref NetNode node, out UnableReason reason) {
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
				Log._Debug($"TrafficLightManager.SetTrafficLight: called for node {nodeId}, flag={flag}");
#endif
			if (! IsTrafficLightToggleable(nodeId, ref node, out reason)) {
#if DEBUGTTL
				if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
					Log._Debug($"TrafficLightManager.SetTrafficLight: Traffic light @ {nodeId} is not toggleable");
#endif
				if (reason != UnableReason.HasTimedLight || !flag) {
#if DEBUGTTL
					if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
						Log._Debug($"TrafficLightManager.SetTrafficLight: ... but has timed light and we want to enable it");
#endif
					return false;
				}
			}

			NetNode.Flags flags = node.m_flags | NetNode.Flags.CustomTrafficLights;
			if ((bool)flag) {
#if DEBUGTTL
				if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
					Log._Debug($"Adding traffic light @ node {nodeId}");
#endif
				flags |= NetNode.Flags.TrafficLights;
			} else {
#if DEBUGTTL
				if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
					Log._Debug($"Removing traffic light @ node {nodeId}");
#endif
				flags &= ~NetNode.Flags.TrafficLights;
			}
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
				Log._Debug($"TrafficLightManager.SetTrafficLight: Setting traffic light at node {nodeId} -- flags={flags}");
#endif
			node.m_flags = flags;
			return true;
		}

		public bool AddTrafficLight(ushort nodeId, ref NetNode node) {
			UnableReason reason;
			return AddTrafficLight(nodeId, ref node, out reason);
		}

		public bool AddTrafficLight(ushort nodeId, ref NetNode node, out UnableReason reason) {
			TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);
			return SetTrafficLight(nodeId, true, ref node, out reason);
		}

		public bool RemoveTrafficLight(ushort nodeId, ref NetNode node) {
			UnableReason reason;
			return RemoveTrafficLight(nodeId, ref node, out reason);
		}

		public bool RemoveTrafficLight(ushort nodeId, ref NetNode node, out UnableReason reason) {
			return SetTrafficLight(nodeId, false, ref node, out reason);
		}

		public bool ToggleTrafficLight(ushort nodeId, ref NetNode node) {
			return SetTrafficLight(nodeId, !HasTrafficLight(nodeId, ref node), ref node);
		}

		public bool ToggleTrafficLight(ushort nodeId, ref NetNode node, out UnableReason reason) {
			return SetTrafficLight(nodeId, !HasTrafficLight(nodeId, ref node), ref node, out reason);
		}

		public bool IsTrafficLightToggleable(ushort nodeId, ref NetNode node, out UnableReason reason) {
			if (TrafficLightSimulationManager.Instance.HasTimedSimulation(nodeId)) {
				reason = UnableReason.HasTimedLight;
#if DEBUGTTL
				if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
					Log._Debug($"Cannot toggle traffic lights at node {nodeId}: Node has a timed traffic light");
#endif
				return false;
			}

			if (!LogicUtil.CheckFlags((uint)node.m_flags, (uint)(NetNode.Flags.Created | NetNode.Flags.Deleted | NetNode.Flags.Junction), (uint)(NetNode.Flags.Created | NetNode.Flags.Junction))) {
				reason = UnableReason.NoJunction;
#if DEBUGTTL
				if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
					Log._Debug($"Cannot toggle traffic lights at node {nodeId}: Node is not a junction");
#endif
				return false;
			}

			int numRoads = 0;
			int numTrainTracks = 0;
			int numMonorailTracks = 0;
			int numPedSegments = 0;
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
				NetInfo info = segment.Info;
				if (info.m_class.m_service == ItemClass.Service.Road) {
					++numRoads;
				} else if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Train) != VehicleInfo.VehicleType.None) {
					++numTrainTracks;
				} else if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Monorail) != VehicleInfo.VehicleType.None) {
					++numMonorailTracks;
				}
				if (info.m_hasPedestrianLanes) {
					++numPedSegments;
				}

				return true;
			});

			if (numRoads >= 2 || numTrainTracks >= 2 || numMonorailTracks >= 2 || numPedSegments != 0) {
#if DEBUGTTL
				if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
					Log._Debug($"Can toggle traffic lights at node {nodeId}: numRoads={numRoads} numTrainTracks={numTrainTracks} numMonorailTracks={numMonorailTracks} numPedSegments={numPedSegments}");
#endif
				reason = UnableReason.None;
				return true;
			}

#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == nodeId)
				Log._Debug($"Cannot toggle traffic lights at node {nodeId}: Insufficient segments. numRoads={numRoads} numTrainTracks={numTrainTracks} numMonorailTracks={numMonorailTracks} numPedSegments={numPedSegments}");
#endif
			reason = UnableReason.InsufficientSegments;
			return false;
		}

		public bool IsTrafficLightEnablable(ushort nodeId, ref NetNode node, out UnableReason reason) {
			bool ret = IsTrafficLightToggleable(nodeId, ref node, out reason);
			if (reason == UnableReason.HasTimedLight) {
				reason = UnableReason.None;
				return true;
			}
			return ret;
		}

		public bool HasTrafficLight(ushort nodeId, ref NetNode node) {
			return LogicUtil.CheckFlags((uint)node.m_flags, (uint)(NetNode.Flags.Created | NetNode.Flags.Deleted | NetNode.Flags.TrafficLights), (uint)(NetNode.Flags.Created | NetNode.Flags.TrafficLights));
		}

		[Obsolete]
		public bool LoadData(string data) {
			bool success = true;
			var trafficLightDefs = data.Split(',');

			Log.Info($"Loading junction traffic light data (old method)");

			foreach (var split in trafficLightDefs.Select(def => def.Split(':')).Where(split => split.Length > 1)) {
				try {
					Log._Debug($"Traffic light split data: {split[0]} , {split[1]}");
					var nodeId = Convert.ToUInt16(split[0]);
					uint flag = Convert.ToUInt16(split[1]);

					if (!Services.NetService.IsNodeValid(nodeId))
						continue;

					Flags.setNodeTrafficLight(nodeId, flag > 0);
				} catch (Exception e) {
					// ignore as it's probably bad save data.
					Log.Error($"Error setting the NodeTrafficLights: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		[Obsolete]
		public string SaveData(ref bool success) {
			return null;
		}

		public bool LoadData(List<Configuration.NodeTrafficLight> data) {
			bool success = true;
			Log.Info($"Loading toggled traffic lights (new method)");

			foreach (Configuration.NodeTrafficLight nodeLight in data) {
				try {
					if (!Services.NetService.IsNodeValid(nodeLight.nodeId))
						continue;

					Log._Debug($"Setting traffic light @ {nodeLight.nodeId} to {nodeLight.trafficLight}");
					Services.NetService.ProcessNode(nodeLight.nodeId, delegate (ushort nodeId, ref NetNode node) {
						SetTrafficLight(nodeLight.nodeId, nodeLight.trafficLight, ref node);
						return true;
					});
					//Flags.setNodeTrafficLight(nodeLight.nodeId, nodeLight.trafficLight);
				} catch (Exception e) {
					// ignore as it's probably bad save data.
					Log.Error($"Error setting the NodeTrafficLights @ {nodeLight.nodeId}: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		List<Configuration.NodeTrafficLight> ICustomDataManager<List<Configuration.NodeTrafficLight>>.SaveData(ref bool success) {
			return null;

			/*List<Configuration.NodeTrafficLight> ret = new List<Configuration.NodeTrafficLight>();
			for (ushort nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				try {
					if (!Flags.mayHaveTrafficLight(nodeId))
						continue;

					bool? hasTrafficLight = Flags.isNodeTrafficLight(nodeId);
					if (hasTrafficLight == null)
						continue;

					if ((bool)hasTrafficLight) {
						Log._Debug($"Saving that node {nodeId} has a traffic light");
					} else {
						Log._Debug($"Saving that node {nodeId} does not have a traffic light");
					}

					ret.Add(new Configuration.NodeTrafficLight(nodeId, (bool)hasTrafficLight));
				} catch (Exception e) {
					Log.Error($"Exception occurred while saving node traffic light @ {nodeId}: {e.ToString()}");
					success = false;
				}
			}
			return ret;*/
		}
	}
}
