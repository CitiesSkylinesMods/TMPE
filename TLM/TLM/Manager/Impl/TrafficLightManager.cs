namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using ColossalFramework;
    using TrafficManager.TrafficLight.Impl;
    using TrafficManager.API.Traffic.Data;

    /// <summary>
    /// Manages traffic light toggling
    /// </summary>
    public class TrafficLightManager
        : AbstractCustomManager,
          ICustomDataManager<List<Configuration.NodeTrafficLight>>,
          ICustomDataManager<string>,
          ITrafficLightManager
    {
        public static readonly TrafficLightManager Instance = new TrafficLightManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for TrafficLightManager");
        }

        // TODO: Consider replacing out error code with Result<> or VoidResult<>
        public bool SetTrafficLight(ushort nodeId, bool flag, ref NetNode node) {
            return SetTrafficLight(nodeId, flag, ref node, out ToggleTrafficLightError _);
        }

        public bool SetTrafficLight(ushort nodeId,
                                    bool flag,
                                    ref NetNode node,
                                    out ToggleTrafficLightError reason) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                      && DebugSettings.NodeId == nodeId;
#else
            const bool logTrafficLights = false;
#endif
            if (logTrafficLights) {
                Log._Debug($"TrafficLightManager.SetTrafficLight: called for node {nodeId}, flag={flag}");
            }

            if (!CanToggleTrafficLight(nodeId, flag, ref node, out reason)) {
                if (logTrafficLights) {
                    Log._Debug($"TrafficLightManager.SetTrafficLight: Traffic light @ {nodeId} is not toggleable");
                }

                if (reason != ToggleTrafficLightError.HasTimedLight || !flag) {
                    if (logTrafficLights) {
                        Log._Debug("TrafficLightManager.SetTrafficLight: ... but has timed light " +
                                   "and we want to enable it");
                    }

                    return false;
                }
            }

            NetNode.Flags flags = node.m_flags | NetNode.Flags.CustomTrafficLights;
            if (flag) {
                if (logTrafficLights) {
                    Log._Debug($"Adding traffic light @ node {nodeId}");
                }

                flags |= NetNode.Flags.TrafficLights;
                TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);
            } else {
                if (logTrafficLights) {
                    Log._Debug($"Removing traffic light @ node {nodeId}");
                }

                flags &= ~NetNode.Flags.TrafficLights;
            }

            if (logTrafficLights) {
                Log._Debug("TrafficLightManager.SetTrafficLight: Setting traffic light at " +
                           $"node {nodeId} -- flags={flags}");
            }

            node.m_flags = flags;
            Constants.ManagerFactory.GeometryManager.MarkAsUpdated(nodeId, true);
            Notifier.Instance.OnNodeModified(nodeId, this);
            return true;
        }

        public bool AddTrafficLight(ushort nodeId, ref NetNode node) {
            ToggleTrafficLightError reason;
            return AddTrafficLight(nodeId, ref node, out reason);
        }

        public bool AddTrafficLight(ushort nodeId,
                                    ref NetNode node,
                                    out ToggleTrafficLightError reason) {
            TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);
            return SetTrafficLight(nodeId, true, ref node, out reason);
        }

        public bool RemoveTrafficLight(ushort nodeId, ref NetNode node) {
            ToggleTrafficLightError reason;
            return RemoveTrafficLight(nodeId, ref node, out reason);
        }

        public bool RemoveTrafficLight(ushort nodeId,
                                       ref NetNode node,
                                       out ToggleTrafficLightError reason) {
            return SetTrafficLight(nodeId, false, ref node, out reason);
        }

        public void RemoveAllExistingTrafficLights() {
            Array16<NetNode> nodes = NetManager.instance.m_nodes;
            for (ushort i = 0; i < nodes.m_size; i++) {
                RemoveTrafficLight(i, ref nodes.m_buffer[i]);
            }
        }

        public bool ToggleTrafficLight(ushort nodeId) => ToggleTrafficLight(nodeId, ref nodeId.ToNode());

        public bool ToggleTrafficLight(ushort nodeId, ref NetNode node) {
            return SetTrafficLight(nodeId, !HasTrafficLight(nodeId, ref node), ref node);
        }

        public bool ToggleTrafficLight(ushort nodeId, ref NetNode node, out ToggleTrafficLightError reason) {
            return SetTrafficLight(nodeId, !HasTrafficLight(nodeId, ref node), ref node, out reason);
        }

        public void ResetTrafficLightAndPrioritySignsFromNode(ushort nodeId) {
            if (!Shortcuts.InSimulationThread()) {
                SimulationManager.instance.AddAction(() => ResetTrafficLightAndPrioritySignsFromNode(nodeId));
                return;
            }

            ref NetNode netNode = ref nodeId.ToNode();
            if (!netNode.IsValid())
                return;
            if (netNode.m_flags.IsFlagSet(NetNode.Flags.CustomTrafficLights) &&
                (this as ITrafficLightManager).CanToggleTrafficLight(nodeId)) {
                TrafficPriorityManager.Instance.RemovePrioritySignsFromNode(nodeId);

                // if CustomTrafficLights is not set, UpdateNodeFlags() resets traffic lights.
                netNode.m_flags &= ~NetNode.Flags.CustomTrafficLights;
                NetManager.instance.UpdateNodeFlags(nodeId);

                Constants.ManagerFactory.GeometryManager.MarkAsUpdated(nodeId, true);
                Notifier.Instance.OnNodeModified(nodeId, this);
            }
        }

        public bool? GetHasTrafficLight(ushort nodeId) {
            NetNode.Flags flags = nodeId.ToNode().m_flags;
            return flags.IsFlagSet(NetNode.Flags.CustomTrafficLights)
                ? HasTrafficLight(nodeId)
                : null;
        }

        public void SetHasTrafficLight(ushort nodeId, bool? value) {
            if (value == null) {
                ResetTrafficLightAndPrioritySignsFromNode(nodeId);
            } else {
                SetTrafficLight(nodeId, value.Value, ref nodeId.ToNode());
            }
        }

        bool ITrafficLightManager.CanToggleTrafficLight(ushort nodeId) {
            ref NetNode netNode = ref nodeId.ToNode();
            return netNode.IsValid() &&
                CanToggleTrafficLight(
                nodeId,
                HasTrafficLight(nodeId, ref netNode),
                ref netNode,
                out _);
        }

        public bool CanToggleTrafficLight(ushort nodeId,
                                          bool flag, // override?
                                          ref NetNode node,
                                          out ToggleTrafficLightError reason)
        {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == nodeId;
#else
            const bool logTrafficLights = false;
#endif
            ref NetNode netNode = ref nodeId.ToNode();

            if (!flag && TrafficLightSimulationManager.Instance.HasTimedSimulation(nodeId)) {
                reason = ToggleTrafficLightError.HasTimedLight;
                if (logTrafficLights) {
                    Log._Debug($"Cannot toggle traffic lights at node {nodeId}: Node has a timed traffic light");
                }

                return false;
            }

            if (flag &&
                (!netNode.IsValid()
                || !netNode.m_flags.IsFlagSet(NetNode.Flags.Junction)
                || (netNode.m_flags.IsFlagSet(NetNode.Flags.Untouchable)
                    && (!node.Info.m_class || node.Info.m_class.m_service != ItemClass.Service.Road)))) {

                reason = ToggleTrafficLightError.NoJunction;

                if (logTrafficLights) {
                    Log._Debug($"Cannot toggle traffic lights at node {nodeId}: Node is not a junction");
                }

                return false;
            }

            if (!flag && netNode.m_flags.IsFlagSet(NetNode.Flags.LevelCrossing)) {
                reason = ToggleTrafficLightError.IsLevelCrossing;

                if (logTrafficLights) {
                    Log._Debug($"Cannot toggle traffic lights at node {nodeId}: Node is a level crossing");
                }

                return false;
            }

            int numRoads = 0;
            int numTrainTracks = 0;
            int numMonorailTracks = 0;
            int numPedSegments = 0;

            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    NetInfo info = segmentId.ToSegment().Info;
                    if (info.m_class.m_service == ItemClass.Service.Road) {
                        ++numRoads;
                    } else if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Train) !=
                               VehicleInfo.VehicleType.None) {
                        ++numTrainTracks;
                    } else if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Monorail) !=
                               VehicleInfo.VehicleType.None) {
                        ++numMonorailTracks;
                    }

                    if (info.m_hasPedestrianLanes) {
                        ++numPedSegments;
                    }
                }
            }

            if (numRoads >= 2 || numTrainTracks >= 2 || numMonorailTracks >= 2 || numPedSegments != 0) {
                if (logTrafficLights) {
                    Log._DebugFormat(
                        "Can toggle traffic lights at node {0}: numRoads={1} numTrainTracks={2} " +
                        "numMonorailTracks={3} numPedSegments={4}",
                        nodeId,
                        numRoads,
                        numTrainTracks,
                        numMonorailTracks,
                        numPedSegments);
                }

                reason = ToggleTrafficLightError.None;
                return true;
            }

            if (logTrafficLights) {
                Log._DebugFormat(
                    "Cannot toggle traffic lights at node {0}: Insufficient segments. numRoads={1} " +
                    "numTrainTracks={2} numMonorailTracks={3} numPedSegments={4}",
                    nodeId,
                    numRoads,
                    numTrainTracks,
                    numMonorailTracks,
                    numPedSegments);
            }

            reason = ToggleTrafficLightError.InsufficientSegments;
            return false;
        }

        public bool CanEnableTrafficLight(ushort nodeId,
                                          ref NetNode node,
                                          out ToggleTrafficLightError reason) {
            var ret = CanToggleTrafficLight(nodeId, true, ref node, out reason);

            if (!ret && reason == ToggleTrafficLightError.HasTimedLight) {
                reason = ToggleTrafficLightError.None;
                return true;
            }

            return ret;
        }

        public bool HasTrafficLight(ushort nodeId) => HasTrafficLight(nodeId, ref nodeId.ToNode());

        public bool HasTrafficLight(ushort nodeId, ref NetNode node) {
            return node.IsValid()
                && node.m_flags.IsFlagSet(NetNode.Flags.TrafficLights);
        }

        [Obsolete]
        public bool LoadData(string data) {
            bool success = true;
            var trafficLightDefs = data.Split(',');
            Log.Info($"Loading junction traffic light data (old method)");

            IEnumerable<string[]> src = trafficLightDefs
                                        .Select(def => def.Split(':'))
                                        .Where(split => split.Length > 1);
            foreach (string[] split in src) {
                try {
#if DEBUGLOAD
                    Log._Debug($"Traffic light split data: {split[0]} , {split[1]}");
#endif
                    ushort nodeId = Convert.ToUInt16(split[0]);
                    uint flag = Convert.ToUInt16(split[1]);

                    if (!nodeId.ToNode().IsValid()) {
                        continue;
                    }

                    Flags.SetNodeTrafficLight(nodeId, flag > 0);
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
                    ref NetNode netNode = ref nodeLight.nodeId.ToNode();

                    if (!netNode.IsValid())
                        continue;

#if DEBUGLOAD
                    Log._Debug($"Setting traffic light @ {nodeLight.nodeId} to {nodeLight.trafficLight}");
#endif
                    SetTrafficLight(
                        nodeLight.nodeId,
                        nodeLight.trafficLight,
                        ref netNode);
                } catch (Exception e) {
                    // ignore as it's probably bad save data.
                    Log.Error($"Error setting the NodeTrafficLights @ {nodeLight.nodeId}: {e}");
                    success = false;
                }
            }

            return success;
        }

        List<Configuration.NodeTrafficLight>
            ICustomDataManager<List<Configuration.NodeTrafficLight>>.SaveData(ref bool success) {
            return null;
        }
    }
}