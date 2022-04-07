namespace TrafficManager.Manager.Impl.OverlayManagerData {
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    public class ViewportCache {

        private const float MAX_CAMERA_DISTANCE = 300f;
        private const float MAX_MOUSE_DISTANCE = 150f;

        // Flags to inspect during validity checks
        private const NetNode.Flags NODE_FLAGS =
            NetNode.Flags.Created |
            NetNode.Flags.Deleted |
            NetNode.Flags.Collapsed;

        private const NetSegment.Flags SEGMENT_FLAGS =
            NetSegment.Flags.Created |
            NetSegment.Flags.Deleted |
            NetSegment.Flags.Collapsed;

        private const Building.Flags BUILDING_FLAGS =
            Building.Flags.Created |
            Building.Flags.Deleted |
            Building.Flags.Collapsed;

        private static ViewportCache instance;

        public static ViewportCache Instance =>
            instance ??= new ViewportCache();

        // managers
        private static readonly NetManager Networks = NetManager.instance;
        private static readonly BuildingManager Buildings = BuildingManager.instance;
//        private static readonly PropManager Props = PropManager.instance;

        // lookup: id -> object
        private readonly NetNode[] allNodes = Networks.m_nodes.m_buffer;
        private readonly NetSegment[] allSegments = Networks.m_segments.m_buffer;
        private readonly Building[] allBuildings = Buildings.m_buildings.m_buffer;

        // moved
        private readonly FastList<InstanceID> tempMoved = new(); // bc extending a hashset is too expensive
        public HashSet<InstanceID> moved = new();

        // added
        public readonly FastList<InstanceID> newNodes = new();
        public readonly FastList<InstanceID> newSegments = new();
        public readonly FastList<InstanceID> newBuildings = new();
//        public readonly FastList<InstanceID> newProps = new();

        // old
        private HashSet<InstanceID> oldNodes = new();
        private HashSet<InstanceID> oldSegments = new();
        private HashSet<InstanceID> oldBuildings = new();
//        private HashSet<InstanceID> oldProps = new();

        // grids
        private readonly ushort[] nodeGrid = Networks.m_nodeGrid;
        private readonly ushort[] segmentGrid = Networks.m_segmentGrid;
        private readonly ushort[] buildingGrid = Buildings.m_buildingGrid;

        // empty list
        private readonly ReadOnlyCollection<InstanceID> empty = new(new InstanceID[0]);

        // todo: pull from mod option
        private bool useFastestApproach = true;

        // what to scan
        private bool scanNodes;
        private bool scanSegments;
        private bool scanBuildings;

        public ViewportCache() {
            tempMoved.ClearAndEnsureCapacity(ushort.MaxValue * 3);
        }

        /// <summary>
        /// Cache objects that are within camera view.
        /// </summary>
        /// <param name="config">Current overlay config.</param>
        /// <param name="state">Current overlay state.</param>
        /// <remarks>Should be called from <c>SimulationManager.EndRenderingImpl()</c>.</remarks>
        public void ScanVisibleMap(ref OverlayState state) {

            if (state.RefreshMapCache == EntityType.None) {
                // TODO: clear new* lists?
                moved = new();
                return;
            }

            // TODO: if camera zoomed out too far (eg. satellite view) just bail

            var requires = state.RefreshMapCache;

            scanNodes = (requires & EntityType.Node) != 0;
            scanSegments = (requires & EntityType.Segment) != 0;
            scanBuildings = (requires & EntityType.Building) != 0;

            if (useFastestApproach) {
                Scan_Fast(ref state);
            } else {
                Scan_Detailed(ref state);
            }
        }

        private void Scan_Fast(ref OverlayState state) {
            InstanceID id = InstanceID.Empty;

            oldNodes = new(newNodes.m_buffer);
            newNodes.Clear();

            oldSegments = new(newSegments.m_buffer);
            newSegments.Clear();

            oldBuildings = new(newBuildings.m_buffer);
            newBuildings.Clear();

            // m_renderedGroups is refreshed every RenderManager.LateUpdate()
            // OnPreCull might be good point to access?
            FastList<RenderGroup> renderedGroups = RenderManager.instance.m_renderedGroups;
            for (int i = 0; i < renderedGroups.m_size; i++) {
                RenderGroup renderGroup = renderedGroups.m_buffer[i];
                if (renderGroup.m_instanceMask != 0) {
                    int minX = renderGroup.m_x * 270 / 45;
                    int minZ = renderGroup.m_z * 270 / 45;
                    int maxX = (renderGroup.m_x + 1) * 270 / 45 - 1;
                    int maxZ = (renderGroup.m_z + 1) * 270 / 45 - 1;
                    for (int j = minZ; j <= maxZ; j++) {
                        for (int k = minX; k <= maxX; k++) {

                            int cell = j * 270 + k;

                            ushort currentID;

                            if (scanNodes) {
                                currentID = nodeGrid[cell];
                                while (currentID != 0) {
                                    ref NetNode node = ref allNodes[currentID];
                                    if ((node.m_flags & NODE_FLAGS) == NetNode.Flags.Created &&
                                        state.IsInCameraRange(node.m_position, MAX_CAMERA_DISTANCE)) {

                                        id.NetNode = currentID;
                                        if (oldNodes.Contains(id)) {
                                            tempMoved.Add(id);
                                        } else {
                                            newNodes.Add(id);
                                        }
                                    }
                                    currentID = allNodes[currentID].m_nextGridNode;
                                }
                            }

                            if (scanSegments) {
                                currentID = segmentGrid[cell];
                                while (currentID != 0) {
                                    ref NetSegment segment = ref allSegments[currentID];
                                    if ((segment.m_flags & SEGMENT_FLAGS) == NetSegment.Flags.Created &&
                                        state.IsInCameraRange(segment.m_middlePosition, MAX_CAMERA_DISTANCE)) {

                                        id.NetSegment = currentID;
                                        if (oldSegments.Contains(id)) {
                                            tempMoved.Add(id);
                                        } else {
                                            newSegments.Add(id);
                                        }
                                    }
                                    currentID = allSegments[currentID].m_nextGridSegment;
                                }
                            }

                            if (scanBuildings) {
                                currentID = buildingGrid[cell];
                                while (currentID != 0) {
                                    if ((allBuildings[currentID].m_flags & BUILDING_FLAGS) == Building.Flags.Created) {
                                        id.Building = currentID;
                                        if (oldBuildings.Contains(id)) {
                                            tempMoved.Add(id);
                                        } else {
                                            newBuildings.Add(id);
                                        }
                                    }
                                    currentID = allBuildings[currentID].m_nextGridBuilding;
                                }
                            }
                        } // end cell
                    }
                }
            } // end render groups

            moved = new(tempMoved.ToArray());
        }

        private void Scan_Detailed(ref OverlayState state) {
            InstanceID id = InstanceID.Empty;

            oldNodes = new(newNodes.m_buffer);
            newNodes.Clear();

            oldSegments = new(newSegments.m_buffer);
            newSegments.Clear();

            oldBuildings = new(newBuildings.m_buffer);
            newBuildings.Clear();

            GetQualityMinMaxXZ(ref state.CameraInfo, out int minX, out int minZ, out int maxX, out int maxZ);

            for (int j = minZ; j <= maxZ; j++) {

                for (int k = minX; k <= maxX; k++) {

                    int cell = j * 270 + k;

                    ushort currentID;

                    if (scanNodes) {
                        currentID = nodeGrid[cell];
                        while (currentID != 0) {
                            if ((allNodes[currentID].m_flags & NODE_FLAGS) == NetNode.Flags.Created) {
                                id.NetNode = currentID;
                                if (oldNodes.Contains(id)) {
                                    tempMoved.Add(id);
                                } else {
                                    newNodes.Add(id);
                                }
                            }
                            currentID = allNodes[currentID].m_nextGridNode;
                        }
                    }

                    if (scanSegments) {
                        currentID = segmentGrid[cell];
                        while (currentID != 0) {
                            if ((allSegments[currentID].m_flags & SEGMENT_FLAGS) == NetSegment.Flags.Created) {
                                id.NetSegment = currentID;
                                if (oldSegments.Contains(id)) {
                                    tempMoved.Add(id);
                                } else {
                                    newSegments.Add(id);
                                }
                            }
                            currentID = allSegments[currentID].m_nextGridSegment;
                        }
                    }

                    if (scanBuildings) {
                        currentID = buildingGrid[cell];
                        while (currentID != 0) {
                            if ((allBuildings[currentID].m_flags & BUILDING_FLAGS) == Building.Flags.Created) {
                                id.Building = currentID;
                                if (oldBuildings.Contains(id)) {
                                    tempMoved.Add(id);
                                } else {
                                    newBuildings.Add(id);
                                }
                            }
                            currentID = allBuildings[currentID].m_nextGridBuilding;
                        }
                    }
                } // end cell
            }

            moved = new(tempMoved.ToArray());
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1117:Parameters should be on same line or separate lines", Justification = "Readability.")]
        private void GetQualityMinMaxXZ(
            ref RenderManager.CameraInfo cameraInfo,
            out int minX, out int minZ,
            out int maxX, out int maxZ)
        {
            const float maxDistance = 2000f; // needs tuning

            var camPos = cameraInfo.m_position;

            var dirA = cameraInfo.m_directionA;
            var dirB = cameraInfo.m_directionB;
            var dirC = cameraInfo.m_directionC;
            var dirD = cameraInfo.m_directionD;

            float near = cameraInfo.m_near;
            float far = Mathf.Min(maxDistance, cameraInfo.m_far);

            Vector3 nearA = camPos + dirA * near;
            Vector3 nearB = camPos + dirB * near;
            Vector3 nearC = camPos + dirC * near;
            Vector3 nearD = camPos + dirD * near;

            Vector3 farA = camPos + dirA * far;
            Vector3 farB = camPos + dirB * far;
            Vector3 farC = camPos + dirC * far;
            Vector3 farD = camPos + dirD * far;

            Vector3 posMin = Vector3.Min(
                Vector3.Min(Vector3.Min(nearA, nearB), Vector3.Min(nearC, nearD)),
                Vector3.Min(Vector3.Min(farA, farB), Vector3.Min(farC, farD)));

            Vector3 posMax = Vector3.Max(
                Vector3.Max(Vector3.Max(nearA, nearB), Vector3.Max(nearC, nearD)),
                Vector3.Max(Vector3.Max(farA, farB), Vector3.Max(farC, farD)));

            minX = Mathf.Max((int)((posMin.x - 40f) / 64f + 135f), 0);
            minZ = Mathf.Max((int)((posMin.z - 40f) / 64f + 135f), 0);
            maxX = Mathf.Min((int)((posMax.x + 40f) / 64f + 135f), 269);
            maxZ = Mathf.Min((int)((posMax.z + 40f) / 64f + 135f), 269);
        }
    }
}
