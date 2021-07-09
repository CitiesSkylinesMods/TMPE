namespace TrafficManager.UI {
    using System.Text;
    using ColossalFramework;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util;
    using UnityEngine;
#if DEBUG
    using TrafficManager.State.ConfigData;
    using TrafficManager.API.Traffic.Enums;
#endif

    /// <summary>
    /// Renders extra debug time overlays.
    /// </summary>
    public static class DebugToolGUI {
        private const float DEBUG_CLOSE_LOD = 300f;

        /// <summary>Displays segment ids over segments.</summary>
        internal static void DisplaySegments() {
            TrafficMeasurementManager trafficMeasurementManager = TrafficMeasurementManager.Instance;
            NetManager netManager = Singleton<NetManager>.instance;
            GUIStyle counterStyle = new GUIStyle();
            IExtSegmentEndManager endMan = Constants.ManagerFactory.ExtSegmentEndManager;
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;

            for (int i = 1; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
                if ((segmentsBuffer[i].m_flags & NetSegment.Flags.Created) ==
                    NetSegment.Flags.None) {
                    // segment is unused
                    continue;
                }

#if DEBUG
                ItemClass.Service service = segmentsBuffer[i].Info.GetService();
                ItemClass.SubService subService = segmentsBuffer[i].Info.GetSubService();
#else
                if ((netManager.m_segments.m_buffer[i].m_flags & NetSegment.Flags.Untouchable) !=
                    NetSegment.Flags.None) {
                    continue;
                }
#endif
                NetInfo segmentInfo = segmentsBuffer[i].Info;

                Vector3 centerPos = segmentsBuffer[i].m_bounds.center;
                bool visible = GeometryUtil.WorldToScreenPoint(centerPos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = centerPos - camPos;

                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;
                counterStyle.fontSize = (int)(12f * zoom);
                counterStyle.normal.textColor = new Color(1f, 0f, 0f);

                var labelSb = new StringBuilder();
                labelSb.AppendFormat("Segment {0}", i);
#if DEBUG
                labelSb.AppendFormat(", flags: {0}", segmentsBuffer[i].m_flags);
                labelSb.AppendFormat("\nsvc: {0}, sub: {1}", service, subService);

                uint startVehicles = endMan.GetRegisteredVehicleCount(
                    ref endMan.ExtSegmentEnds[endMan.GetIndex((ushort)i, true)]);

                uint endVehicles = endMan.GetRegisteredVehicleCount(
                    ref endMan.ExtSegmentEnds[endMan.GetIndex((ushort)i, false)]);

                labelSb.AppendFormat( "\nstart veh.: {0}, end veh.: {1}", startVehicles, endVehicles);
#endif
                labelSb.AppendFormat("\nTraffic: {0} %", segmentsBuffer[i].m_trafficDensity);

#if DEBUG
                int fwdSegIndex = trafficMeasurementManager.GetDirIndex(
                    (ushort)i,
                    NetInfo.Direction.Forward);
                int backSegIndex = trafficMeasurementManager.GetDirIndex(
                    (ushort)i,
                    NetInfo.Direction.Backward);

                labelSb.Append("\n");

#if MEASURECONGESTION
                float fwdCongestionRatio =
                    trafficMeasurementManager
                        .segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements > 0
                        ? ((uint)trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongested * 100u) /
                          (uint)trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements
                        : 0; // now in %
                float backCongestionRatio =
                    trafficMeasurementManager
                        .segmentDirTrafficData[backSegIndex].numCongestionMeasurements > 0
                        ? ((uint)trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongested * 100u) /
                          (uint)trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongestionMeasurements
                        : 0; // now in %


                labelSb.Append("min speeds: ");
                labelSb.AppendFormat(
                        " {0}%/{1}%",
                        trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].minSpeed / 100,
                        trafficMeasurementManager.segmentDirTrafficData[backSegIndex].minSpeed /
                        100);
                labelSb.Append(", ");
#endif
                labelSb.Append("mean speeds: ");
                labelSb.AppendFormat(
                        " {0}%/{1}%",
                        trafficMeasurementManager.SegmentDirTrafficData[fwdSegIndex].meanSpeed /
                        100,
                        trafficMeasurementManager.SegmentDirTrafficData[backSegIndex].meanSpeed /
                        100);
#if PFTRAFFICSTATS || MEASURECONGESTION
                labelSb.Append("\n");
#endif
#if PFTRAFFICSTATS
                labelSb.Append("pf bufs: ");
                labelSb.AppendFormat(
                    " {0}/{1}",
                    trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].totalPathFindTrafficBuffer,
                    trafficMeasurementManager.segmentDirTrafficData[backSegIndex].totalPathFindTrafficBuffer);
#endif
#if PFTRAFFICSTATS && MEASURECONGESTION
                labelSb.Append(", ");
#endif
#if MEASURECONGESTION
                labelSb.Append("cong: ");
                labelSb.AppendFormat(
                    " {0}% ({1}/{2})/{3}% ({4}/{5})",
                    fwdCongestionRatio,
                    trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongested,
                    trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements,
                    backCongestionRatio,
                    trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongested,
                    trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongestionMeasurements);
#endif
                labelSb.AppendFormat(
                    "\nstart: {0}, end: {1}",
                    segmentsBuffer[i].m_startNode,
                    segmentsBuffer[i].m_endNode);
#endif

                var labelStr = labelSb.ToString();
                Vector2 dim = counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    x: screenPos.x - (dim.x / 2f),
                    y: screenPos.y,
                    width: dim.x,
                    height: dim.y);

                GUI.Label(labelRect, labelStr, counterStyle);

                if (Options.showLanes) {
                    DebugToolGUI.DisplayLanes(
                        segmentId: (ushort)i,
                        segment: ref segmentsBuffer[i],
                        segmentInfo: ref segmentInfo);
                }
            }
        } // end DisplaySegments

        /// <summary>Displays node ids over nodes.</summary>
        internal static void DisplayNodes() {
            var counterStyle = new GUIStyle();
            NetManager netManager = Singleton<NetManager>.instance;

            for (int i = 1; i < NetManager.MAX_NODE_COUNT; ++i) {
                if ((netManager.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Created) ==
                    NetNode.Flags.None) {
                    // node is unused
                    continue;
                }

                Vector3 pos = netManager.m_nodes.m_buffer[i].m_position;
                bool visible = GeometryUtil.WorldToScreenPoint(pos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = pos - camPos;
                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;

                counterStyle.fontSize = (int)(15f * zoom);
                counterStyle.normal.textColor = new Color(0f, 0f, 1f);

                string labelStr = "Node " + i;
#if DEBUG
                labelStr += string.Format(
                    "\nflags: {0}\nlane: {1}",
                    netManager.m_nodes.m_buffer[i].m_flags,
                    netManager.m_nodes.m_buffer[i].m_lane);
#endif
                Vector2 dim = counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    x: screenPos.x - (dim.x / 2f),
                    y: screenPos.y,
                    width: dim.x,
                    height: dim.y);

                GUI.Label(labelRect, labelStr, counterStyle);
            }
        } // end DisplayNodes

        /// <summary>Displays vehicle ids over vehicles.</summary>
        internal static void DisplayVehicles() {
            GUIStyle _counterStyle = new GUIStyle();
            SimulationManager simManager = Singleton<SimulationManager>.instance;
            ExtVehicleManager vehStateManager = ExtVehicleManager.Instance;
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

            int startVehicleId = 1;
            int endVehicleId = Constants.ServiceFactory.VehicleService.MaxVehicleCount - 1;
#if DEBUG
            if (DebugSettings.VehicleId != 0) {
                startVehicleId = endVehicleId = DebugSettings.VehicleId;
            }
#endif
            Vehicle[] vehiclesBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;

            for (int i = startVehicleId; i <= endVehicleId; ++i) {
                if (vehicleManager.m_vehicles.m_buffer[i].m_flags == 0) {
                    // node is unused
                    continue;
                }

                Vector3 vehPos = vehicleManager.m_vehicles.m_buffer[i].GetSmoothPosition((ushort)i);
                bool visible = GeometryUtil.WorldToScreenPoint(vehPos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = simManager.m_simulationView.m_position;
                Vector3 diff = vehPos - camPos;
                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;

                _counterStyle.fontSize = (int)(10f * zoom);
                _counterStyle.normal.textColor = new Color(1f, 1f, 1f);
                // _counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

                ExtVehicle vState = vehStateManager.ExtVehicles[(ushort)i];
                ExtCitizenInstance driverInst =
                    ExtCitizenInstanceManager.Instance.ExtInstances[
                        Constants.ManagerFactory.ExtVehicleManager
                                 .GetDriverInstanceId(
                                     (ushort)i,
                                     ref vehiclesBuffer[i])];
                // bool startNode = vState.currentStartNode;
                // ushort segmentId = vState.currentSegmentId;

                // Converting magnitudes into game speed float, and then into km/h
                SpeedValue vehSpeed = SpeedValue.FromVelocity(vehicleManager.m_vehicles.m_buffer[i].GetLastFrameVelocity().magnitude);
#if DEBUG
                if (GlobalConfig.Instance.Debug.ExtPathMode != ExtPathMode.None &&
                    driverInst.pathMode != GlobalConfig.Instance.Debug.ExtPathMode) {
                    continue;
                }
#endif
                string labelStr = string.Format(
                    "V #{0} is a {1}{2} {3} @ ~{4} (len: {5:0.0}, {6} @ {7} ({8}), l. {9} " +
                    "-> {10}, l. {11}), w: {12}\n" +
                    "di: {13} dc: {14} m: {15} f: {16} l: {17} lid: {18} ltsu: {19} lpu: {20} " +
                    "als: {21} srnd: {22} trnd: {23}",
                    i,
                    vState.recklessDriver ? "reckless " : string.Empty,
                    vState.flags,
                    vState.vehicleType,
                    vehSpeed.ToKmphPrecise().ToString(),
                    vState.totalLength,
                    vState.junctionTransitState,
                    vState.currentSegmentId,
                    vState.currentStartNode,
                    vState.currentLaneIndex,
                    vState.nextSegmentId,
                    vState.nextLaneIndex,
                    vState.waitTime,
                    driverInst.instanceId,
                    ExtCitizenInstanceManager.Instance.GetCitizenId(driverInst.instanceId),
                    driverInst.pathMode,
                    driverInst.failedParkingAttempts,
                    driverInst.parkingSpaceLocation,
                    driverInst.parkingSpaceLocationId,
                    vState.lastTransitStateUpdate,
                    vState.lastPositionUpdate,
                    vState.lastAltLaneSelSegmentId,
                    Constants.ManagerFactory.ExtVehicleManager.GetStaticVehicleRand((ushort)i),
                    Constants.ManagerFactory.ExtVehicleManager.GetTimedVehicleRand((ushort)i));

                Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    x: screenPos.x - (dim.x / 2f),
                    y: screenPos.y - dim.y - 50f,
                    width: dim.x,
                    height: dim.y);

                GUI.Box(labelRect, labelStr, _counterStyle);
            }
        } // end DisplayVehicles

        /// <summary>Displays debug data over citizens. </summary>
        internal static void DisplayCitizens() {
            GUIStyle counterStyle = new GUIStyle();
            CitizenManager citManager = Singleton<CitizenManager>.instance;
            Citizen[] citizensBuffer = Singleton<CitizenManager>.instance.m_citizens.m_buffer;
            VehicleParked[] parkedVehiclesBuffer = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer;
            Vehicle[] vehiclesBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;

            for (int i = 1; i < CitizenManager.MAX_INSTANCE_COUNT; ++i) {
                if ((citManager.m_instances.m_buffer[i].m_flags &
                     CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
                    continue;
                }
#if DEBUG
                if (GlobalConfig.Instance.Debug.NoValidPathCitizensOverlay) {
#endif
                    if (citManager.m_instances.m_buffer[i].m_path != 0) {
                        continue;
                    }
#if DEBUG
                }
#endif

                Vector3 pos = citManager.m_instances.m_buffer[i].GetSmoothPosition((ushort)i);
                bool visible = GeometryUtil.WorldToScreenPoint(pos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = pos - camPos;

                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;

                counterStyle.fontSize = (int)(10f * zoom);
                counterStyle.normal.textColor = new Color(1f, 0f, 1f);
                // _counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

#if DEBUG
                if (GlobalConfig.Instance.Debug.ExtPathMode != ExtPathMode.None &&
                    ExtCitizenInstanceManager.Instance.ExtInstances[i].pathMode !=
                    GlobalConfig.Instance.Debug.ExtPathMode) {
                    continue;
                }
#endif

                var labelSb = new StringBuilder();
                ExtCitizen[] extCitizensBuf = ExtCitizenManager.Instance.ExtCitizens;
                labelSb.AppendFormat(
                    "Inst. {0}, Cit. {1},\nm: {2}, tm: {3}, ltm: {4}, ll: {5}",
                    i,
                    citManager.m_instances.m_buffer[i].m_citizen,
                    ExtCitizenInstanceManager.Instance.ExtInstances[i].pathMode,
                    extCitizensBuf[citManager.m_instances.m_buffer[i].m_citizen].transportMode,
                    extCitizensBuf[citManager.m_instances.m_buffer[i].m_citizen].lastTransportMode,
                    extCitizensBuf[citManager.m_instances.m_buffer[i].m_citizen].lastLocation);

                if (citManager.m_instances.m_buffer[i].m_citizen != 0) {
                    Citizen citizen = citizensBuffer[citManager.m_instances.m_buffer[i].m_citizen];
                    if (citizen.m_parkedVehicle != 0) {
                        labelSb.AppendFormat(
                            "\nparked: {0} dist: {1}",
                            citizen.m_parkedVehicle,
                            (parkedVehiclesBuffer[citizen.m_parkedVehicle].m_position - pos).magnitude);
                    }

                    if (citizen.m_vehicle != 0) {
                        labelSb.AppendFormat(
                            "\nveh: {0} dist: {1}",
                            citizen.m_vehicle,
                            (vehiclesBuffer[citizen.m_vehicle].GetLastFramePosition() - pos).magnitude);
                    }
                }

                string labelStr = labelSb.ToString();
                Vector2 dim = counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    x: screenPos.x - (dim.x / 2f),
                    y: screenPos.y - dim.y - 50f,
                    width: dim.x,
                    height: dim.y);

                GUI.Box(labelRect, labelStr, counterStyle);
            }
        } // end DisplayCitizens

        internal static void DisplayBuildings() {
            GUIStyle _counterStyle = new GUIStyle();
            BuildingManager buildingManager = Singleton<BuildingManager>.instance;

            for (int i = 1; i < BuildingManager.MAX_BUILDING_COUNT; ++i) {
                if ((buildingManager.m_buildings.m_buffer[i].m_flags & Building.Flags.Created)
                    == Building.Flags.None) {
                    continue;
                }

                Vector3 pos = buildingManager.m_buildings.m_buffer[i].m_position;
                bool visible = GeometryUtil.WorldToScreenPoint(pos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = pos - camPos;
                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 150f / diff.magnitude;

                _counterStyle.fontSize = (int)(10f * zoom);
                _counterStyle.normal.textColor = new Color(0f, 1f, 0f);
                // _counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

                string labelStr = string.Format(
                    "Building {0}, PDemand: {1}, IncTDem: {2}, OutTDem: {3}",
                    i,
                    ExtBuildingManager.Instance.ExtBuildings[i].parkingSpaceDemand,
                    ExtBuildingManager.Instance.ExtBuildings[i].incomingPublicTransportDemand,
                    ExtBuildingManager.Instance.ExtBuildings[i].outgoingPublicTransportDemand);

                Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    x: screenPos.x - (dim.x / 2f),
                    y: screenPos.y - dim.y - 50f,
                    width: dim.x,
                    height: dim.y);

                GUI.Box(labelRect, labelStr, _counterStyle);
            }
        } // end DisplayBuildings

        /// <summary>Displays lane ids over lanes.</summary>
        private static void DisplayLanes(ushort segmentId,
                                         ref NetSegment segment,
                                         ref NetInfo segmentInfo)
        {
            var _counterStyle = new GUIStyle();
            Vector3 centerPos = segment.m_bounds.center;
            bool visible = GeometryUtil.WorldToScreenPoint(centerPos, out Vector3 screenPos);

            if (!visible) {
                return;
            }

            screenPos.y -= 200;

            if (screenPos.z < 0) {
                return;
            }

            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            Vector3 diff = centerPos - camPos;

            if (diff.magnitude > DEBUG_CLOSE_LOD) {
                return; // do not draw if too distant
            }

            float zoom = 1.0f / diff.magnitude * 150f;

            _counterStyle.fontSize = (int)(11f * zoom);
            _counterStyle.normal.textColor = new Color(1f, 1f, 0f);

            // uint totalDensity = 0u;
            // for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
            //        if (CustomRoadAI.currentLaneDensities[segmentId] != null &&
            //         i < CustomRoadAI.currentLaneDensities[segmentId].Length)
            //                totalDensity += CustomRoadAI.currentLaneDensities[segmentId][i];
            // }

            uint curLaneId = segment.m_lanes;
            var labelSb = new StringBuilder();
            NetLane[] lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
                if (curLaneId == 0) {
                    break;
                }

                bool laneTrafficDataLoaded =
                    TrafficMeasurementManager.Instance.GetLaneTrafficData(
                        segmentId,
                        (byte)i,
                        out LaneTrafficData laneTrafficData);

                NetInfo.Lane laneInfo = segmentInfo.m_lanes[i];

#if PFTRAFFICSTATS
                uint pfTrafficBuf =
                    TrafficMeasurementManager
                        .Instance.segmentDirTrafficData[
                            TrafficMeasurementManager.Instance.GetDirIndex(
                                segmentId,
                                laneInfo.m_finalDirection)]
                        .totalPathFindTrafficBuffer;
#endif
                // TrafficMeasurementManager.Instance.GetTrafficData(segmentId,
                // laneInfo.m_finalDirection, out dirTrafficData);
                // int dirIndex = laneInfo.m_finalDirection == NetInfo.Direction.Backward ? 1 : 0;

                labelSb.AppendFormat("L idx {0}, id {1}", i, curLaneId);
#if DEBUG
                labelSb.AppendFormat(
                    ", in: {0}, out: {1}, f: {2}, l: {3} km/h, rst: {4}, dir: {5}, fnl: {6}, " +
                    "pos: {7:0.##}, sim: {8} for {9}/{10}",
                    RoutingManager.Instance.CalcInnerSimilarLaneIndex(segmentId, i),
                    RoutingManager.Instance.CalcOuterSimilarLaneIndex(segmentId, i),
                    (NetLane.Flags)lanesBuffer[curLaneId].m_flags,
                    SpeedLimitManager.Instance.GetCustomSpeedLimit(curLaneId),
                    VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(
                        segmentId,
                        segmentInfo,
                        (uint)i,
                        laneInfo,
                        VehicleRestrictionsMode.Configured),
                    laneInfo.m_direction,
                    laneInfo.m_finalDirection,
                    laneInfo.m_position,
                    laneInfo.m_similarLaneIndex,
                    laneInfo.m_vehicleType,
                    laneInfo.m_laneType);
#endif
                if (laneTrafficDataLoaded) {
                    labelSb.AppendFormat(
                        ", sp: {0}%",
                        TrafficMeasurementManager.Instance.CalcLaneRelativeMeanSpeed(
                            segmentId,
                            (byte)i,
                            curLaneId,
                            laneInfo) / 100);
#if DEBUG
                    labelSb.AppendFormat(
                        ", buf: {0}, max: {1}, acc: {2}",
                        laneTrafficData.trafficBuffer,
                        laneTrafficData.maxTrafficBuffer,
                        laneTrafficData.accumulatedSpeeds);

#if PFTRAFFICSTATS
                    labelSb.AppendFormat(
                        ", pfBuf: {0}/{1}, ({2} %)",
                        laneTrafficData.pathFindTrafficBuffer,
                        laneTrafficData.lastPathFindTrafficBuffer,
                        pfTrafficBuf > 0
                            ? "" + ((laneTrafficData.lastPathFindTrafficBuffer * 100u) /
                                    pfTrafficBuf)
                            : "n/a");
#endif
#endif
#if MEASUREDENSITY
                    if (dirTrafficDataLoaded) {
                        labelSb.AppendFormat(
                            ", rel. dens.: {0}%",
                            dirTrafficData.accumulatedDensities > 0
                                ? "" + Math.Min(
                                      laneTrafficData[i].accumulatedDensities * 100 /
                                      dirTrafficData.accumulatedDensities,
                                      100)
                                : "?");
                    }

                    labelSb.AppendFormat(
                        ", acc: {0}",
                        laneTrafficData[i].accumulatedDensities);
#endif
                }

                labelSb.AppendFormat(", nd: {0}", lanesBuffer[curLaneId].m_nodes);
#if DEBUG
                //    labelSb.AppendFormat(
                //        " ({0}/{1}/{2})",
                //        CustomRoadAI.currentLaneDensities[segmentId] != null &&
                //        i < CustomRoadAI.currentLaneDensities[segmentId].Length
                //            ? string.Empty + CustomRoadAI.currentLaneDensities[segmentId][i]
                //            : "?",
                //        CustomRoadAI.maxLaneDensities[segmentId] != null &&
                //        i < CustomRoadAI.maxLaneDensities[segmentId].Length
                //            ? string.Empty + CustomRoadAI.maxLaneDensities[segmentId][i]
                //            : "?",
                //        totalDensity);
                //    labelSb.AppendFormat(
                //        " ({0}/{1})",
                //        CustomRoadAI.currentLaneDensities[segmentId] != null &&
                //        i < CustomRoadAI.currentLaneDensities[segmentId].Length
                //            ? string.Empty + CustomRoadAI.currentLaneDensities[segmentId][i]
                //            : "?",
                //        totalDensity);
#endif
                //    labelSb.AppendFormat(
                //        ", abs. dens.: {0} %",
                //        CustomRoadAI.laneMeanAbsDensities[segmentId] != null &&
                //        i < CustomRoadAI.laneMeanAbsDensities[segmentId].Length
                //            ? "" + CustomRoadAI.laneMeanAbsDensities[segmentId][i]
                //            : "?");
                labelSb.Append("\n");

                curLaneId = lanesBuffer[curLaneId].m_nextLane;
            }

            var labelStr = labelSb.ToString();
            Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
            Rect labelRect = new Rect(
                x: screenPos.x - (dim.x / 2f),
                y: screenPos.y,
                width: dim.x,
                height: dim.y);

            GUI.Label(labelRect, labelStr, _counterStyle);
        } // end DisplayLanes
    } // end class
}