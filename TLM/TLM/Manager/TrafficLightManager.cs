using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	public class TrafficLightManager : AbstractNodeGeometryObservingManager, ICustomDataManager<List<Configuration.NodeTrafficLight>>, ICustomDataManager<string> {
		public static TrafficLightManager Instance { get; private set; } = null;

		static TrafficLightManager() {
			Instance = new TrafficLightManager();
		}

		internal void NodeSimulationStep(ushort nodeId, ref NetNode data) {
			Flags.applyNodeTrafficLightFlag(nodeId);
		}

		public void SetTrafficLight(ushort nodeId, bool flag) {
			if (Flags.setNodeTrafficLight(nodeId, flag)) {
				SubscribeToNodeGeometry(nodeId);
			}
		}

		public void AddTrafficLight(ushort nodeId) {
			SetTrafficLight(nodeId, true);
		}

		public void RemoveTrafficLight(ushort nodeId) {
			SetTrafficLight(nodeId, false);
		}

		public void ToggleTrafficLight(ushort nodeId) {
			SetTrafficLight(nodeId, !HasTrafficLight(nodeId));
		}

		public bool HasTrafficLight(ushort nodeId) {
			return Flags.isNodeTrafficLight(nodeId);
		}

		protected override void HandleInvalidNode(NodeGeometry geometry) {
			Flags.resetTrafficLight(geometry.NodeId);
		}

		protected override void HandleValidNode(NodeGeometry geometry) {
			Flags.applyNodeTrafficLightFlag(geometry.NodeId);
		}

		public void ApplyAllFlags() {
			for (ushort nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				Flags.applyNodeTrafficLightFlag(nodeId);
			}
		}

		public override void OnBeforeSaveData() {
			base.OnBeforeSaveData();
			ApplyAllFlags();
		}

		public override void OnAfterLoadData() {
			base.OnAfterLoadData();
			ApplyAllFlags();
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

					if (!NetUtil.IsNodeValid(nodeId))
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
					if (!NetUtil.IsNodeValid(nodeLight.nodeId))
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
			List<Configuration.NodeTrafficLight> ret = new List<Configuration.NodeTrafficLight>();
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
			return ret;
		}
	}
}
