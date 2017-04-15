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
			HasTimedLight
		}

		public static readonly TrafficLightManager Instance = new TrafficLightManager();

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"- Not implemented -");
			// TODO implement
		}

		public bool SetTrafficLight(ushort nodeId, bool flag) {
			UnableReason reason;
			return SetTrafficLight(nodeId, flag, out reason);
		}

		public bool SetTrafficLight(ushort nodeId, bool flag, out UnableReason reason) {
			if (! IsTrafficLightToggleable(nodeId, out reason)) {
				return false;
			}

			Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
				NetNode.Flags flags = node.m_flags | NetNode.Flags.CustomTrafficLights;
				if ((bool)flag) {
					//Log._Debug($"Adding traffic light @ node {nId}");
					flags |= NetNode.Flags.TrafficLights;
				} else {
					//Log._Debug($"Removing traffic light @ node {nId}");
					flags &= ~NetNode.Flags.TrafficLights;
				}
				node.m_flags = flags;
				return true;
			});
			return true;
		}

		public bool AddTrafficLight(ushort nodeId) {
			UnableReason reason;
			return AddTrafficLight(nodeId, out reason);
		}

		public bool AddTrafficLight(ushort nodeId, out UnableReason reason) {
			TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);
			return SetTrafficLight(nodeId, true, out reason);
		}

		public bool RemoveTrafficLight(ushort nodeId) {
			UnableReason reason;
			return RemoveTrafficLight(nodeId, out reason);
		}

		public bool RemoveTrafficLight(ushort nodeId, out UnableReason reason) {
			return SetTrafficLight(nodeId, false, out reason);
		}

		public bool ToggleTrafficLight(ushort nodeId) {
			return SetTrafficLight(nodeId, !HasTrafficLight(nodeId));
		}

		public bool ToggleTrafficLight(ushort nodeId, out UnableReason reason) {
			return SetTrafficLight(nodeId, !HasTrafficLight(nodeId), out reason);
		}

		public bool IsTrafficLightToggleable(ushort nodeId, out UnableReason reason) {
			if (!Services.NetService.CheckNodeFlags(nodeId, NetNode.Flags.Created | NetNode.Flags.Deleted | NetNode.Flags.Junction, NetNode.Flags.Created | NetNode.Flags.Junction)) {
				reason = UnableReason.NoJunction;
				return false;
			}

			if (TrafficLightSimulationManager.Instance.HasActiveTimedSimulation(nodeId)) {
				reason = UnableReason.HasTimedLight;
				return false;
			}

			reason = UnableReason.None;
			return true;
		}

		public bool HasTrafficLight(ushort nodeId) {
			return Services.NetService.CheckNodeFlags(nodeId, NetNode.Flags.Created | NetNode.Flags.Deleted | NetNode.Flags.TrafficLights, NetNode.Flags.Created | NetNode.Flags.TrafficLights);
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
					SetTrafficLight(nodeLight.nodeId, nodeLight.trafficLight);
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
