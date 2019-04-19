using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.Packaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using UnityEngine;
using static TrafficManager.Configuration;

namespace TrafficManager.Custom.AssetManager
{
    public static class BuildingDecorationPatch
    {
        public static List<IdentificationTool.AssetSegment> m_pendingSegments; // Segments pending to be saved

        public static void ProcessNewPaths(List<ushort> segments, BuildingInfo info)
        {
            try
            {
#if DEBUG
                Debug.Log("Loading TMPE settings...");
#endif
                TmpeAssetData data = AssetDataManager.Instance.AssetsWithData[IdentificationTool.GetNameWithoutPrefix(info.name)];

                Debug.Log("Hoooray! Tmpe settings loaded");

                Configuration config = SerializableDataExtension.DeserializeData(data.TmpeConfiguration, out bool error);
                if (error)
                    throw new Exception("Tmpe: Failed to deserialize data");

                /* We identify the old ids with the new ids */
                IdentificationTool.CreateDictionaries(segments, data.Segments, out Dictionary<ushort, ushort> segmentPairs,
                    out Dictionary<ushort, ushort> nodePairs, out Dictionary<uint, uint> lanePairs);
                IdentificationTool.TranslateGameConfiguration(config, segmentPairs, nodePairs, lanePairs);

                /*SerializableDataExtension.LoadDataState(config, out bool error2);
                if (error2)
                    throw new Exception("Tmpe: error when applying loaded data");*/

                /* Apply settings after some time elapses (hack) */
                ProvisionalDataLoader.StartTimer(config);
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.Log(ex);
#endif
            }
        }

        /* STOCK CODE START */

        /* Called when building (intersection) loads its segments. Detoured method. Added code is marked 'ns'
         * We obrain a list with all the segments and pass them to the ProcessNewPaths method. We create our own list for that purpose instead of
         * using instance.m_tempSegmentBuffer because it almost seems that the game can create multiple segments per one saved path in the file which
         * makes absolutely no sense at all but would lead to our list being shifted compared to the (third) one in which we had saved the ids of the
         * old segments. Whatever, you don't really have to understand this, I studied it a lot when creating my previous mods and it's the part I 
         * understand the most compared to the rest */
        public static void LoadPaths(BuildingInfo info, ushort buildingID, ref Building data, float elevation)
        {
#if DEBUG
            Debug.Log("LoadPaths TMPE detour");
#endif

            // ns
            Dictionary<string, TmpeAssetData> dict = AssetDataManager.Instance.AssetsWithData;
            bool isAssetWithTmpeSettings = dict.ContainsKey(IdentificationTool.GetNameWithoutPrefix(info.name));
            List<ushort> CreatedSegments = null;
            if(isAssetWithTmpeSettings)
            {
                CreatedSegments = new List<ushort>();
            }
            // ns end

            if (info.m_paths != null)
            {
                NetManager instance = Singleton<NetManager>.instance;
                instance.m_tempNodeBuffer.Clear();
                instance.m_tempSegmentBuffer.Clear();
                for (int i = 0; i < info.m_paths.Length; i++)
                {
                    // ns
                    if( isAssetWithTmpeSettings )
                    {
                        CreatedSegments.Add(0);
                    }
                    // ns end

                    BuildingInfo.PathInfo pathInfo = info.m_paths[i];
                    if (pathInfo.m_finalNetInfo != null && pathInfo.m_nodes != null && pathInfo.m_nodes.Length != 0)
                    {
                        Vector3 vector = data.CalculatePosition(pathInfo.m_nodes[0]);
                        bool flag = /*BuildingDecoration.*/RequireFixedHeight(info, pathInfo.m_finalNetInfo, pathInfo.m_nodes[0]);
                        if (!flag)
                        {
                            vector.y = NetSegment.SampleTerrainHeight(pathInfo.m_finalNetInfo, vector, false, pathInfo.m_nodes[0].y + elevation);
                        }
                        Ray ray = new Ray(vector + new Vector3(0f, 8f, 0f), Vector3.down);
                        NetTool.ControlPoint controlPoint;
                        if (!/*BuildingDecoration.*/FindConnectNode(instance.m_tempNodeBuffer, vector, pathInfo.m_finalNetInfo, out controlPoint))
                        {
                            if (NetTool.MakeControlPoint(ray, 16f, pathInfo.m_finalNetInfo, true, NetNode.Flags.Untouchable, NetSegment.Flags.Untouchable, Building.Flags.All, pathInfo.m_nodes[0].y + elevation - pathInfo.m_finalNetInfo.m_buildHeight, true, out controlPoint))
                            {
                                Vector3 vector2 = controlPoint.m_position - vector;
                                if (!flag)
                                {
                                    vector2.y = 0f;
                                }
                                float sqrMagnitude = vector2.sqrMagnitude;
                                if (sqrMagnitude > pathInfo.m_maxSnapDistance * pathInfo.m_maxSnapDistance)
                                {
                                    controlPoint.m_position = vector;
                                    controlPoint.m_elevation = 0f;
                                    controlPoint.m_node = 0;
                                    controlPoint.m_segment = 0;
                                }
                                else
                                {
                                    controlPoint.m_position.y = vector.y;
                                }
                            }
                            else
                            {
                                controlPoint.m_position = vector;
                            }
                        }
                        ushort num;
                        ushort num2;
                        int num3;
                        int num4;
                        if (controlPoint.m_node != 0)
                        {
                            instance.m_tempNodeBuffer.Add(controlPoint.m_node);
                        }
                        else if (NetTool.CreateNode(pathInfo.m_finalNetInfo, controlPoint, controlPoint, controlPoint, NetTool.m_nodePositionsSimulation, 0, false, false, false, false, pathInfo.m_invertSegments, false, 0, out num, out num2, out num3, out num4) == ToolBase.ToolErrors.None)
                        {
                            instance.m_tempNodeBuffer.Add(num);
                            controlPoint.m_node = num;
                            if (pathInfo.m_forbidLaneConnection != null && pathInfo.m_forbidLaneConnection.Length > 0 && pathInfo.m_forbidLaneConnection[0])
                            {
                                NetNode[] buffer = instance.m_nodes.m_buffer;
                                ushort num5 = num;
                                buffer[(int)num5].m_flags = (buffer[(int)num5].m_flags | NetNode.Flags.ForbidLaneConnection);
                            }
                            if (pathInfo.m_trafficLights != null && pathInfo.m_trafficLights.Length > 0)
                            {
                                /*BuildingDecoration.*/
                                TrafficLightsToFlags(pathInfo.m_trafficLights[0], ref instance.m_nodes.m_buffer[(int)num].m_flags);
                            }
                        }
                        for (int j = 1; j < pathInfo.m_nodes.Length; j++)
                        {
                            vector = data.CalculatePosition(pathInfo.m_nodes[j]);
                            bool flag2 = /*BuildingDecoration.*/RequireFixedHeight(info, pathInfo.m_finalNetInfo, pathInfo.m_nodes[j]);
                            if (!flag2)
                            {
                                vector.y = NetSegment.SampleTerrainHeight(pathInfo.m_finalNetInfo, vector, false, pathInfo.m_nodes[j].y + elevation);
                            }
                            ray = new Ray(vector + new Vector3(0f, 8f, 0f), Vector3.down);
                            NetTool.ControlPoint controlPoint2;
                            if (!/*BuildingDecoration.*/FindConnectNode(instance.m_tempNodeBuffer, vector, pathInfo.m_finalNetInfo, out controlPoint2))
                            {
                                if (NetTool.MakeControlPoint(ray, 16f, pathInfo.m_finalNetInfo, true, NetNode.Flags.Untouchable, NetSegment.Flags.Untouchable, Building.Flags.All, pathInfo.m_nodes[j].y + elevation - pathInfo.m_finalNetInfo.m_buildHeight, true, out controlPoint2))
                                {
                                    Vector3 vector3 = controlPoint2.m_position - vector;
                                    if (!flag2)
                                    {
                                        vector3.y = 0f;
                                    }
                                    float sqrMagnitude2 = vector3.sqrMagnitude;
                                    if (sqrMagnitude2 > pathInfo.m_maxSnapDistance * pathInfo.m_maxSnapDistance)
                                    {
                                        controlPoint2.m_position = vector;
                                        controlPoint2.m_elevation = 0f;
                                        controlPoint2.m_node = 0;
                                        controlPoint2.m_segment = 0;
                                    }
                                    else
                                    {
                                        controlPoint2.m_position.y = vector.y;
                                    }
                                }
                                else
                                {
                                    controlPoint2.m_position = vector;
                                }
                            }
                            NetTool.ControlPoint middlePoint = controlPoint2;
                            if (pathInfo.m_curveTargets != null && pathInfo.m_curveTargets.Length >= j)
                            {
                                middlePoint.m_position = data.CalculatePosition(pathInfo.m_curveTargets[j - 1]);
                                if (!flag || !flag2)
                                {
                                    middlePoint.m_position.y = NetSegment.SampleTerrainHeight(pathInfo.m_finalNetInfo, middlePoint.m_position, false, pathInfo.m_curveTargets[j - 1].y + elevation);
                                }
                            }
                            else
                            {
                                middlePoint.m_position = (controlPoint.m_position + controlPoint2.m_position) * 0.5f;
                            }
                            middlePoint.m_direction = VectorUtils.NormalizeXZ(middlePoint.m_position - controlPoint.m_position);
                            controlPoint2.m_direction = VectorUtils.NormalizeXZ(controlPoint2.m_position - middlePoint.m_position);
                            ushort num6;
                            ushort num7;
                            ushort num8;
                            int num9;
                            int num10;
                            if (NetTool.CreateNode(pathInfo.m_finalNetInfo, controlPoint, middlePoint, controlPoint2, NetTool.m_nodePositionsSimulation, 1, false, false, false, false, false, pathInfo.m_invertSegments, false, 0, out num6, out num7, out num8, out num9, out num10) == ToolBase.ToolErrors.None)
                            {
                                instance.m_tempNodeBuffer.Add(num7);
                                instance.m_tempSegmentBuffer.Add(num8);

                                // ns
                                if(isAssetWithTmpeSettings)
                                {
                                    CreatedSegments[CreatedSegments.Count - 1] = num8;
                                }
                                // ns end

                                controlPoint2.m_node = num7;
                                if (pathInfo.m_forbidLaneConnection != null && pathInfo.m_forbidLaneConnection.Length > j && pathInfo.m_forbidLaneConnection[j])
                                {
                                    NetNode[] buffer2 = instance.m_nodes.m_buffer;
                                    ushort num11 = num7;
                                    buffer2[(int)num11].m_flags = (buffer2[(int)num11].m_flags | NetNode.Flags.ForbidLaneConnection);
                                }
                                if (pathInfo.m_trafficLights != null && pathInfo.m_trafficLights.Length > j)
                                {
                                    /*BuildingDecoration.*/
                                    TrafficLightsToFlags(pathInfo.m_trafficLights[j], ref instance.m_nodes.m_buffer[(int)num7].m_flags);
                                }
                                if (pathInfo.m_yieldSigns != null && pathInfo.m_yieldSigns.Length >= j * 2)
                                {
                                    if (pathInfo.m_yieldSigns[j * 2 - 2])
                                    {
                                        NetSegment[] buffer3 = instance.m_segments.m_buffer;
                                        ushort num12 = num8;
                                        buffer3[(int)num12].m_flags = (buffer3[(int)num12].m_flags | NetSegment.Flags.YieldStart);
                                    }
                                    if (pathInfo.m_yieldSigns[j * 2 - 1])
                                    {
                                        NetSegment[] buffer4 = instance.m_segments.m_buffer;
                                        ushort num13 = num8;
                                        buffer4[(int)num13].m_flags = (buffer4[(int)num13].m_flags | NetSegment.Flags.YieldEnd);
                                    }
                                }
                            }
                            controlPoint = controlPoint2;
                            flag = flag2;
                        }
                    }
                }
                for (int k = 0; k < instance.m_tempNodeBuffer.m_size; k++)
                {
                    ushort num14 = instance.m_tempNodeBuffer.m_buffer[k];
                    if ((instance.m_nodes.m_buffer[(int)num14].m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.None)
                    {
                        if (buildingID != 0)
                        {
                            if ((data.m_flags & Building.Flags.Active) == Building.Flags.None && instance.m_nodes.m_buffer[(int)num14].Info.m_canDisable)
                            {
                                NetNode[] buffer5 = instance.m_nodes.m_buffer;
                                ushort num15 = num14;
                                buffer5[(int)num15].m_flags = (buffer5[(int)num15].m_flags | NetNode.Flags.Disabled);
                            }
                            NetNode[] buffer6 = instance.m_nodes.m_buffer;
                            ushort num16 = num14;
                            buffer6[(int)num16].m_flags = (buffer6[(int)num16].m_flags | NetNode.Flags.Untouchable);
                            instance.UpdateNode(num14);
                            instance.m_nodes.m_buffer[(int)num14].m_nextBuildingNode = data.m_netNode;
                            data.m_netNode = num14;
                        }
                        else
                        {
                            instance.UpdateNode(num14);
                        }
                    }
                }
                for (int l = 0; l < instance.m_tempSegmentBuffer.m_size; l++)
                {
                    ushort num17 = instance.m_tempSegmentBuffer.m_buffer[l];
                    if ((instance.m_segments.m_buffer[(int)num17].m_flags & NetSegment.Flags.Untouchable) == NetSegment.Flags.None)
                    {
                        if (buildingID != 0)
                        {
                            NetSegment[] buffer7 = instance.m_segments.m_buffer;
                            ushort num18 = num17;
                            buffer7[(int)num18].m_flags = (buffer7[(int)num18].m_flags | NetSegment.Flags.Untouchable);
                            instance.UpdateSegment(num17);
                        }
                        else
                        {
                            if ((Singleton<ToolManager>.instance.m_properties.m_mode & ItemClass.Availability.AssetEditor) != ItemClass.Availability.None)
                            {
                                NetInfo info2 = instance.m_segments.m_buffer[(int)num17].Info;
                                if ((info2.m_availableIn & ItemClass.Availability.AssetEditor) == ItemClass.Availability.None)
                                {
                                    NetSegment[] buffer8 = instance.m_segments.m_buffer;
                                    ushort num19 = num17;
                                    buffer8[(int)num19].m_flags = (buffer8[(int)num19].m_flags | NetSegment.Flags.Untouchable);
                                }
                            }
                            instance.UpdateSegment(num17);
                        }
                    }
                }

                // ns
                if (isAssetWithTmpeSettings && CreatedSegments.Count > 0)
                {
                    if (CreatedSegments.Last() == 0)
                        CreatedSegments.RemoveAt(CreatedSegments.Count - 1);

                    ProcessNewPaths(CreatedSegments, info);
                }
                // ns end

                instance.m_tempNodeBuffer.Clear();
                instance.m_tempSegmentBuffer.Clear();
            }
        }

        /* Detoured method. We save a list of all the segment ids (and ids of their lanes and nodes as well) to m_pendingSegments
         * We will use it a few moments later when the OnAssetSaved method is called
         * See line 408 */
        public static void SavePaths(BuildingInfo info, ushort buildingID, ref Building data)
        {
            Debug.Log("SavePaths TMPE detour"); //

            List<IdentificationTool.AssetSegment> SavedSegments = new List<IdentificationTool.AssetSegment>();
            // ns end

            FastList<BuildingInfo.PathInfo> fastList = new FastList<BuildingInfo.PathInfo>();
            List<ushort> list = new List<ushort>();
            List<ushort> list2 = new List<ushort>();
            for (ushort num = 1; num < 49152; num += 1)
            {
                if (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)num].m_flags != Building.Flags.None)
                {
                    list.AddRange(BuildingDecoration.GetBuildingSegments(ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)num]));
                    list2.Add(num);
                }
            }
            NetManager instance = Singleton<NetManager>.instance;
            for (int i = 0; i < 36864; i++)
            {
                if ((instance.m_segments.m_buffer[i].m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created)
                {
                    if (!list.Contains((ushort)i))
                    {
                        NetInfo info2 = instance.m_segments.m_buffer[i].Info;
                        ushort startNode = instance.m_segments.m_buffer[i].m_startNode;
                        ushort endNode = instance.m_segments.m_buffer[i].m_endNode;
                        Vector3 position = instance.m_nodes.m_buffer[(int)startNode].m_position;
                        Vector3 position2 = instance.m_nodes.m_buffer[(int)endNode].m_position;
                        Vector3 startDirection = instance.m_segments.m_buffer[i].m_startDirection;
                        Vector3 endDirection = instance.m_segments.m_buffer[i].m_endDirection;
                        Vector3 a;
                        float num2;
                        float num3;
                        if (NetSegment.IsStraight(position, startDirection, position2, endDirection))
                        {
                            a = (position + position2) * 0.5f;
                        }
                        else if (Line2.Intersect(VectorUtils.XZ(position), VectorUtils.XZ(position + startDirection), VectorUtils.XZ(position2), VectorUtils.XZ(position2 + endDirection), out num2, out num3))
                        {
                            float minNodeDistance = info2.GetMinNodeDistance();
                            num2 = Mathf.Max(minNodeDistance, num2);
                            num3 = Mathf.Max(minNodeDistance, num3);
                            a = (position + startDirection * num2 + position2 + endDirection * num3) * 0.5f;
                        }
                        else
                        {
                            a = (position + position2) * 0.5f;
                        }
                        BuildingInfo.PathInfo pathInfo = new BuildingInfo.PathInfo();
                        pathInfo.m_netInfo = info2;
                        pathInfo.m_finalNetInfo = info2;
                        pathInfo.m_nodes = new Vector3[2];
                        pathInfo.m_nodes[0] = position - data.m_position;
                        pathInfo.m_nodes[0].z = -pathInfo.m_nodes[0].z;
                        pathInfo.m_nodes[1] = position2 - data.m_position;
                        pathInfo.m_nodes[1].z = -pathInfo.m_nodes[1].z;
                        pathInfo.m_curveTargets = new Vector3[1];
                        pathInfo.m_curveTargets[0] = a - data.m_position;
                        pathInfo.m_curveTargets[0].z = -pathInfo.m_curveTargets[0].z;
                        pathInfo.m_forbidLaneConnection = new bool[2];
                        pathInfo.m_forbidLaneConnection[0] = ((instance.m_nodes.m_buffer[(int)startNode].m_flags & NetNode.Flags.ForbidLaneConnection) != NetNode.Flags.None);
                        pathInfo.m_forbidLaneConnection[1] = ((instance.m_nodes.m_buffer[(int)endNode].m_flags & NetNode.Flags.ForbidLaneConnection) != NetNode.Flags.None);
                        pathInfo.m_trafficLights = new BuildingInfo.TrafficLights[2];
                        pathInfo.m_trafficLights[0] = /*BuildingDecoration.*/FlagsToTrafficLights(instance.m_nodes.m_buffer[(int)startNode].m_flags);
                        pathInfo.m_trafficLights[1] = /*BuildingDecoration.*/FlagsToTrafficLights(instance.m_nodes.m_buffer[(int)endNode].m_flags);
                        pathInfo.m_yieldSigns = new bool[2];
                        pathInfo.m_yieldSigns[0] = ((instance.m_segments.m_buffer[i].m_flags & NetSegment.Flags.YieldStart) != NetSegment.Flags.None);
                        pathInfo.m_yieldSigns[1] = ((instance.m_segments.m_buffer[i].m_flags & NetSegment.Flags.YieldEnd) != NetSegment.Flags.None);
                        if (info.m_placementMode == BuildingInfo.PlacementMode.Roadside && (position.z > (float)info.m_cellLength * 4f + 8f || position2.z > (float)info.m_cellLength * 4f + 8f))
                        {
                            pathInfo.m_maxSnapDistance = 7.5f;
                        }
                        else
                        {
                            pathInfo.m_maxSnapDistance = 0.1f;
                        }
                        pathInfo.m_invertSegments = ((instance.m_segments.m_buffer[i].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None);
                        fastList.Add(pathInfo);
                        SavedSegments.Add(new IdentificationTool.AssetSegment( (ushort)i )); // ns
                    }
                }
            }
            for (int j = 0; j < 32768; j++)
            {
                if ((instance.m_nodes.m_buffer[j].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) == NetNode.Flags.Created && instance.m_nodes.m_buffer[j].CountSegments() == 0)
                {
                    bool flag = false;
                    foreach (ushort num4 in list2)
                    {
                        if (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)num4].ContainsNode((ushort)j))
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                    {
                        NetInfo info3 = instance.m_nodes.m_buffer[j].Info;
                        Vector3 position3 = instance.m_nodes.m_buffer[j].m_position;
                        BuildingInfo.PathInfo pathInfo2 = new BuildingInfo.PathInfo();
                        pathInfo2.m_netInfo = info3;
                        pathInfo2.m_finalNetInfo = info3;
                        pathInfo2.m_nodes = new Vector3[1];
                        pathInfo2.m_nodes[0] = position3 - data.m_position;
                        pathInfo2.m_nodes[0].z = -pathInfo2.m_nodes[0].z;
                        pathInfo2.m_curveTargets = new Vector3[0];
                        pathInfo2.m_forbidLaneConnection = new bool[1];
                        pathInfo2.m_forbidLaneConnection[0] = ((instance.m_nodes.m_buffer[j].m_flags & NetNode.Flags.ForbidLaneConnection) != NetNode.Flags.None);
                        pathInfo2.m_trafficLights = new BuildingInfo.TrafficLights[1];
                        pathInfo2.m_trafficLights[0] = /*BuildingDecoration.*/FlagsToTrafficLights(instance.m_nodes.m_buffer[j].m_flags);
                        pathInfo2.m_yieldSigns = new bool[0];
                        pathInfo2.m_maxSnapDistance = 0.1f;
                        pathInfo2.m_invertSegments = false;
                        fastList.Add(pathInfo2);
                    }
                }
            }
            info.m_paths = fastList.ToArray();

            // ns:
            if (SavedSegments.Count > 0)
            {
                m_pendingSegments = SavedSegments;
            }
        }

        /* Everything below is pure stock code with no changes whatsoever */

        // Token: 0x060009AB RID: 2475 RVA: 0x00076754 File Offset: 0x00074B54
        private static BuildingInfo.TrafficLights FlagsToTrafficLights(NetNode.Flags flags)
        {
            if ((flags & NetNode.Flags.CustomTrafficLights) == NetNode.Flags.None)
            {
                return BuildingInfo.TrafficLights.Default;
            }
            if ((flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None)
            {
                return BuildingInfo.TrafficLights.ForceOff;
            }
            return BuildingInfo.TrafficLights.ForceOn;
        }

        // Token: 0x060009AC RID: 2476 RVA: 0x00076774 File Offset: 0x00074B74
        private static void TrafficLightsToFlags(BuildingInfo.TrafficLights trafficLights, ref NetNode.Flags flags)
        {
            if (trafficLights != BuildingInfo.TrafficLights.Default)
            {
                if (trafficLights != BuildingInfo.TrafficLights.ForceOn)
                {
                    if (trafficLights == BuildingInfo.TrafficLights.ForceOff)
                    {
                        flags = ((flags & ~NetNode.Flags.TrafficLights) | NetNode.Flags.CustomTrafficLights);
                    }
                }
                else
                {
                    flags |= (NetNode.Flags.TrafficLights | NetNode.Flags.CustomTrafficLights);
                }
            }
            else
            {
                flags &= (NetNode.Flags.Created | NetNode.Flags.Deleted | NetNode.Flags.Original | NetNode.Flags.Disabled | NetNode.Flags.End | NetNode.Flags.Middle | NetNode.Flags.Bend | NetNode.Flags.Junction | NetNode.Flags.Moveable | NetNode.Flags.Untouchable | NetNode.Flags.Outside | NetNode.Flags.Temporary | NetNode.Flags.Double | NetNode.Flags.Fixed | NetNode.Flags.OnGround | NetNode.Flags.Ambiguous | NetNode.Flags.Water | NetNode.Flags.Sewage | NetNode.Flags.ForbidLaneConnection | NetNode.Flags.Underground | NetNode.Flags.Transition | NetNode.Flags.LevelCrossing | NetNode.Flags.OneWayOut | NetNode.Flags.TrafficLights | NetNode.Flags.OneWayIn | NetNode.Flags.Heating | NetNode.Flags.Electricity | NetNode.Flags.Collapsed | NetNode.Flags.DisableOnlyMiddle | NetNode.Flags.AsymForward | NetNode.Flags.AsymBackward);
            }
        }

        private static bool RequireFixedHeight(BuildingInfo buildingInfo, NetInfo info2, Vector3 pos)
        {
            if (info2.m_useFixedHeight)
            {
                return true;
            }
            ItemClass.Service service = info2.m_class.m_service;
            ItemClass.SubService subService = info2.m_class.m_subService;
            ItemClass.Layer layer = info2.m_class.m_layer;
            ItemClass.Service service2 = ItemClass.Service.None;
            ItemClass.SubService subService2 = ItemClass.SubService.None;
            ItemClass.Layer layer2 = ItemClass.Layer.Default;
            if (info2.m_intersectClass != null)
            {
                service2 = info2.m_intersectClass.m_service;
                subService2 = info2.m_intersectClass.m_subService;
                layer2 = info2.m_intersectClass.m_layer;
            }
            if (info2.m_netAI.SupportUnderground() || info2.m_netAI.IsUnderground())
            {
                layer |= (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
                layer2 |= (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
            }
            for (int i = 0; i < buildingInfo.m_paths.Length; i++)
            {
                BuildingInfo.PathInfo pathInfo = buildingInfo.m_paths[i];
                if (pathInfo.m_finalNetInfo != null && pathInfo.m_finalNetInfo.m_useFixedHeight && pathInfo.m_nodes != null && pathInfo.m_nodes.Length != 0)
                {
                    for (int j = 0; j < pathInfo.m_nodes.Length; j++)
                    {
                        if (Vector3.SqrMagnitude(pos - pathInfo.m_nodes[j]) < 0.001f)
                        {
                            NetInfo finalNetInfo = pathInfo.m_finalNetInfo;
                            ItemClass connectionClass = finalNetInfo.GetConnectionClass();
                            if (((service == ItemClass.Service.None || connectionClass.m_service == service) && (subService == ItemClass.SubService.None || connectionClass.m_subService == subService) && (layer == ItemClass.Layer.None || (connectionClass.m_layer & layer) != ItemClass.Layer.None)) || (finalNetInfo.m_intersectClass != null && (service == ItemClass.Service.None || finalNetInfo.m_intersectClass.m_service == service) && (subService == ItemClass.SubService.None || finalNetInfo.m_intersectClass.m_subService == subService) && (layer == ItemClass.Layer.None || (finalNetInfo.m_intersectClass.m_layer & layer) != ItemClass.Layer.None)) || (finalNetInfo.m_netAI.CanIntersect(info2) && connectionClass.m_service == service2 && (subService2 == ItemClass.SubService.None || connectionClass.m_subService == subService2) && (layer2 == ItemClass.Layer.None || (connectionClass.m_layer & layer2) != ItemClass.Layer.None)))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        // Token: 0x060009A7 RID: 2471 RVA: 0x000752A0 File Offset: 0x000736A0
        private static bool FindConnectNode(FastList<ushort> buffer, Vector3 pos, NetInfo info2, out NetTool.ControlPoint point)
        {
            point = default(NetTool.ControlPoint);
            NetManager instance = Singleton<NetManager>.instance;
            ItemClass.Service service = info2.m_class.m_service;
            ItemClass.SubService subService = info2.m_class.m_subService;
            ItemClass.Layer layer = info2.m_class.m_layer;
            ItemClass.Service service2 = ItemClass.Service.None;
            ItemClass.SubService subService2 = ItemClass.SubService.None;
            ItemClass.Layer layer2 = ItemClass.Layer.Default;
            if (info2.m_intersectClass != null)
            {
                service2 = info2.m_intersectClass.m_service;
                subService2 = info2.m_intersectClass.m_subService;
                layer2 = info2.m_intersectClass.m_layer;
            }
            if (info2.m_netAI.SupportUnderground() || info2.m_netAI.IsUnderground())
            {
                layer |= (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
                layer2 |= (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
            }
            for (int i = 0; i < buffer.m_size; i++)
            {
                ushort num = buffer.m_buffer[i];
                if (Vector3.SqrMagnitude(pos - instance.m_nodes.m_buffer[(int)num].m_position) < 0.001f)
                {
                    NetInfo info3 = instance.m_nodes.m_buffer[(int)num].Info;
                    ItemClass connectionClass = info3.GetConnectionClass();
                    if (((service == ItemClass.Service.None || connectionClass.m_service == service) && (subService == ItemClass.SubService.None || connectionClass.m_subService == subService) && (layer == ItemClass.Layer.None || (connectionClass.m_layer & layer) != ItemClass.Layer.None)) || (info3.m_intersectClass != null && (service == ItemClass.Service.None || info3.m_intersectClass.m_service == service) && (subService == ItemClass.SubService.None || info3.m_intersectClass.m_subService == subService) && (layer == ItemClass.Layer.None || (info3.m_intersectClass.m_layer & layer) != ItemClass.Layer.None)) || (connectionClass.m_service == service2 && (subService2 == ItemClass.SubService.None || connectionClass.m_subService == subService2) && (layer2 == ItemClass.Layer.None || (connectionClass.m_layer & layer2) != ItemClass.Layer.None)))
                    {
                        point.m_position = instance.m_nodes.m_buffer[(int)num].m_position;
                        point.m_direction = Vector3.zero;
                        point.m_node = num;
                        point.m_segment = 0;
                        if (info3.m_netAI.IsUnderground())
                        {
                            point.m_elevation = (float)(-(float)instance.m_nodes.m_buffer[(int)num].m_elevation);
                        }
                        else
                        {
                            point.m_elevation = (float)instance.m_nodes.m_buffer[(int)num].m_elevation;
                        }
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
