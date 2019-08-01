﻿namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System;
    using System.Collections.Generic;
    using API.Traffic.Data;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using Manager.Impl;
    using State;
    using Textures;
    using Traffic;
    using UnityEngine;
    using Util;

    public class SpeedLimitsTool : SubTool {
        public const int
            BREAK_PALETTE_COLUMN_KMPH = 8; // palette shows N in a row, then break and another row

        public const int
            BREAK_PALETTE_COLUMN_MPH = 10; // palette shows M in a row, then break and another row

        private const ushort LOWER_KMPH = 10;
        public const ushort UPPER_KMPH = 140;
        public const ushort KMPH_STEP = 10;

        private const ushort LOWER_MPH = 5;
        public const ushort UPPER_MPH = 90;
        public const ushort MPH_STEP = 5;

        /// <summary>Visible sign size, slightly reduced from 100 to accomodate another column for MPH</summary>
        private const int GUI_SPEED_SIGN_SIZE = 80;
        private readonly float speedLimitSignSize = 70f;

        private bool cursorInSecondaryPanel;

        /// <summary>Currently selected speed limit on the limits palette</summary>
        private SpeedValue currentPaletteSpeedLimit = new SpeedValue(-1f);

        private readonly Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir =
            new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();

        private Rect paletteWindowRect =
            TrafficManagerTool.MoveGUI(new Rect(0, 0, 10 * (GUI_SPEED_SIGN_SIZE + 5), 150));

        private Rect defaultsWindowRect = TrafficManagerTool.MoveGUI(new Rect(0, 80, 50, 50));
        private readonly HashSet<ushort> currentlyVisibleSegmentIds;
        private bool defaultsWindowVisible;
        private int currentInfoIndex = -1;
        private SpeedValue currentSpeedLimit = new SpeedValue(-1f);

        private Texture2D RoadTexture {
            get {
                if (roadTexture == null) {
                    roadTexture = new Texture2D(GUI_SPEED_SIGN_SIZE, GUI_SPEED_SIGN_SIZE);
                }

                return roadTexture;
            }
        }

        private Texture2D roadTexture;
        private bool showLimitsPerLane;

        public SpeedLimitsTool(TrafficManagerTool mainTool)
            : base(mainTool)
        {
            currentlyVisibleSegmentIds = new HashSet<ushort>();
        }

        public override bool IsCursorInPanel() {
            return base.IsCursorInPanel() || cursorInSecondaryPanel;
        }

        public override void OnActivate() { }

        public override void OnPrimaryClickOverlay() { }

        public override void OnToolGUI(Event e) {
            base.OnToolGUI(e);

            string unitTitle = string.Format(
                " ({0})",
                GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                    ? Translation.GetString("Miles_per_hour")
                    : Translation.GetString("Kilometers_per_hour"));

            paletteWindowRect.width = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                                          ? 10 * (GUI_SPEED_SIGN_SIZE + 5)
                                          : 8 * (GUI_SPEED_SIGN_SIZE + 5);

            paletteWindowRect = GUILayout.Window(
                254,
                paletteWindowRect,
                GuiSpeedLimitsWindow,
                Translation.GetString("Speed_limits") + unitTitle,
                WindowStyle);

            if (defaultsWindowVisible) {
                defaultsWindowRect = GUILayout.Window(
                    258,
                    defaultsWindowRect,
                    _guiDefaultsWindow,
                    Translation.GetString("Default_speed_limits"),
                    WindowStyle);
            }

            cursorInSecondaryPanel = paletteWindowRect.Contains(Event.current.mousePosition)
                                     || (defaultsWindowVisible
                                         && defaultsWindowRect.Contains(
                                             Event.current.mousePosition));

            // overlayHandleHovered = false;
            // ShowSigns(false);
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) { }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly && !Options.speedLimitsOverlay) {
                return;
            }

            ShowSigns(viewOnly);
        }

        public override void Cleanup() {
            segmentCenterByDir.Clear();
            currentlyVisibleSegmentIds.Clear();
            lastCamPos = null;
            lastCamRot = null;
            currentInfoIndex = -1;
            currentSpeedLimit = new SpeedValue(-1f);
        }

        private Quaternion? lastCamRot;
        private Vector3? lastCamPos;

        private void ShowSigns(bool viewOnly) {
            Quaternion camRot = Camera.main.transform.rotation;
            Vector3 camPos = Camera.main.transform.position;

            NetManager netManager = Singleton<NetManager>.instance;
            SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;

            if (lastCamPos == null
                || lastCamRot == null
                || !lastCamRot.Equals(camRot)
                || !lastCamPos.Equals(camPos))
            {
                // cache visible segments
                currentlyVisibleSegmentIds.Clear();

                for (uint segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                    if (!Constants.ServiceFactory.NetService.IsSegmentValid((ushort)segmentId)) {
                        continue;
                    }

                    // if ((netManager.m_segments.m_buffer[segmentId].m_flags &
                    // NetSegment.Flags.Untouchable) != NetSegment.Flags.None) continue;
                    if ((netManager.m_segments.m_buffer[segmentId].m_bounds.center - camPos)
                        .magnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE) {
                        continue; // do not draw if too distant
                    }

                    bool visible = MainTool.WorldToScreenPoint(
                        netManager.m_segments.m_buffer[segmentId].m_bounds.center,
                        out Vector3 _);

                    if (!visible) {
                        continue;
                    }

                    if (!speedLimitManager.MayHaveCustomSpeedLimits(
                            (ushort)segmentId,
                            ref netManager.m_segments.m_buffer[segmentId])) {
                        continue;
                    }

                    currentlyVisibleSegmentIds.Add((ushort)segmentId);
                } // end for all segments

                lastCamPos = camPos;
                lastCamRot = camRot;
            }

            bool handleHovered = false;

            foreach (ushort segmentId in currentlyVisibleSegmentIds) {
                bool visible = MainTool.WorldToScreenPoint(
                    netManager.m_segments.m_buffer[segmentId].m_bounds.center,
                    out Vector3 _);

                if (!visible) {
                    continue;
                }

                // draw speed limits
                if (MainTool.GetToolMode() == ToolMode.VehicleRestrictions &&
                    segmentId == SelectedSegmentId) {
                    continue;
                }

                // no speed limit overlay on selected segment when in vehicle restrictions mode
                drawSpeedLimitHandles(
                    segmentId,
                    ref netManager.m_segments.m_buffer[segmentId],
                    viewOnly,
                    ref camPos);
            }
        }

        /// <summary>
        /// The window for setting the defaullt speeds per road type
        /// </summary>
        /// <param name="num"></param>
        private void _guiDefaultsWindow(int num) {
            List<NetInfo> mainNetInfos = SpeedLimitManager.Instance.GetCustomizableNetInfos();

            if (mainNetInfos == null || mainNetInfos.Count <= 0) {
                Log._Debug($"mainNetInfos={mainNetInfos?.Count}");
                DragWindow(ref defaultsWindowRect);
                return;
            }

            bool updateRoadTex = false;

            if (currentInfoIndex < 0 || currentInfoIndex >= mainNetInfos.Count) {
                currentInfoIndex = 0;
                updateRoadTex = true;
                Log._Debug($"set currentInfoIndex to 0");
            }

            NetInfo info = mainNetInfos[currentInfoIndex];

            if (updateRoadTex) {
                UpdateRoadTex(info);
            }

            if (currentSpeedLimit.GameUnits < 0f) {
                currentSpeedLimit = new SpeedValue(
                    SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info));
                Log._Debug($"set currentSpeedLimit to {currentSpeedLimit}");
            }

            // Log._Debug($"currentInfoIndex={currentInfoIndex} currentSpeedLimitIndex={currentSpeedLimitIndex}");
            // Road type label
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.Label(Translation.GetString("Road_type") + ":");
            GUILayout.EndVertical();

            // switch between NetInfos
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("←", GUILayout.Width(50))) {
                currentInfoIndex =
                    (currentInfoIndex + mainNetInfos.Count - 1) % mainNetInfos.Count;
                info = mainNetInfos[currentInfoIndex];
                currentSpeedLimit = new SpeedValue(
                    SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info));
                UpdateRoadTex(info);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            // NetInfo thumbnail
            GUILayout.Box(RoadTexture, GUILayout.Height(GUI_SPEED_SIGN_SIZE));
            GUILayout.FlexibleSpace();

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("→", GUILayout.Width(50))) {
                currentInfoIndex = (currentInfoIndex + 1) % mainNetInfos.Count;
                info = mainNetInfos[currentInfoIndex];
                currentSpeedLimit = new SpeedValue(
                    SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info));
                UpdateRoadTex(info);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            var centeredTextStyle = new GUIStyle("label") { alignment = TextAnchor.MiddleCenter };

            // NetInfo name
            GUILayout.Label(info.name, centeredTextStyle);

            // Default speed limit label
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.Label(Translation.GetString("Default_speed_limit") + ":");
            GUILayout.EndVertical();

            // switch between speed limits
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("←", GUILayout.Width(50))) {
                // currentSpeedLimit = (currentSpeedLimitIndex +
                //     SpeedLimitManager.Instance.AvailableSpeedLimits.Count - 1)
                //     % SpeedLimitManager.Instance.AvailableSpeedLimits.Count;
                currentSpeedLimit = GetPrevious(currentSpeedLimit);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            // speed limit sign
            GUILayout.Box(SpeedLimitTextures.GetSpeedLimitTexture(currentSpeedLimit),
                          GUILayout.Width(GUI_SPEED_SIGN_SIZE),
                          GUILayout.Height(GUI_SPEED_SIGN_SIZE));
            GUILayout.Label(GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                                ? Translation.GetString("Miles_per_hour")
                                : Translation.GetString("Kilometers_per_hour"));

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("→", GUILayout.Width(50))) {
                // currentSpeedLimitIndex = (currentSpeedLimitIndex + 1) %
                //     SpeedLimitManager.Instance.AvailableSpeedLimits.Count;
                currentSpeedLimit = GetNext(currentSpeedLimit);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // Save & Apply
            GUILayout.BeginVertical();
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            // Close button. TODO: Make more visible or obey 'Esc' pressed or something
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("X", GUILayout.Width(80))) {
                defaultsWindowVisible = false;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(Translation.GetString("Save"), GUILayout.Width(70))) {
                SpeedLimitManager.Instance.FixCurrentSpeedLimits(info);
                SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimit(info, currentSpeedLimit.GameUnits);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                Translation.GetString("Save") + " & " + Translation.GetString("Apply"),
                GUILayout.Width(160))) {
                SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimit(info, currentSpeedLimit.GameUnits);
                SpeedLimitManager.Instance.ClearCurrentSpeedLimits(info);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            DragWindow(ref defaultsWindowRect);
        }

        private void UpdateRoadTex(NetInfo info) {
            if (info != null) {
                if (info.m_Atlas != null && info.m_Atlas.material != null &&
                    info.m_Atlas.material.mainTexture != null &&
                    info.m_Atlas.material.mainTexture is Texture2D mainTex)
                {
                    UITextureAtlas.SpriteInfo spriteInfo = info.m_Atlas[info.m_Thumbnail];

                    if (spriteInfo != null && spriteInfo.texture != null &&
                        spriteInfo.texture.width > 0 && spriteInfo.texture.height > 0) {
                        try {
                            roadTexture = new Texture2D(
                                spriteInfo.texture.width,
                                spriteInfo.texture.height,
                                TextureFormat.ARGB32,
                                false);

                            roadTexture.SetPixels(
                                0,
                                0,
                                roadTexture.width,
                                roadTexture.height,
                                mainTex.GetPixels(
                                    (int)(spriteInfo.region.x * mainTex.width),
                                    (int)(spriteInfo.region.y * mainTex.height),
                                    (int)(spriteInfo.region.width * mainTex.width),
                                    (int)(spriteInfo.region.height * mainTex.height)));

                            roadTexture.Apply();
                            return;
                        }
                        catch (Exception e) {
                            Log.Warning(
                                $"Could not get texture from NetInfo {info.name}: {e.ToString()}");
                        }
                    }
                }
            }

            // fallback to "noimage" texture
            roadTexture = TextureResources.NoImageTexture2D;
        }

        /// <summary>
        /// The window for selecting and applying a speed limit
        /// </summary>
        /// <param name="num"></param>
        private void GuiSpeedLimitsWindow(int num) {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Color oldColor = GUI.color;
            List<SpeedValue> allSpeedLimits = EnumerateSpeedLimits(SpeedUnit.CurrentlyConfigured);
            allSpeedLimits.Add(new SpeedValue(0)); // add last item: no limit

            bool showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
            var column = 0u; // break palette to a new line at breakColumn
            int breakColumn = showMph ? BREAK_PALETTE_COLUMN_MPH : BREAK_PALETTE_COLUMN_KMPH;

            foreach (SpeedValue speedLimit in allSpeedLimits) {
                // Highlight palette item if it is very close to its float speed
                if (FloatUtil.NearlyEqual(currentPaletteSpeedLimit.GameUnits, speedLimit.GameUnits)) {
                    GUI.color = Color.gray;
                }

                GuiSpeedLimitsWindow_AddButton(showMph, speedLimit);
                GUI.color = oldColor;

                // TODO: This can be calculated from SpeedLimit MPH or KMPH limit constants
                column++;
                if (column % breakColumn == 0) {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //---------------------
            // UI buttons row
            //---------------------
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(Translation.GetString("Default_speed_limits"),
                                 GUILayout.Width(200))) {
                TrafficManagerTool.ShowAdvisor(this.GetType().Name + "_Defaults");
                defaultsWindowVisible = true;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //---------------------
            // Checkboxes row
            //---------------------
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            showLimitsPerLane = GUILayout.Toggle(
                showLimitsPerLane,
                Translation.GetString("Show_lane-wise_speed_limits"));

            GUILayout.FlexibleSpace();

            // Display MPH checkbox, if ticked will save global config
            bool displayMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
            displayMph = GUILayout.Toggle(displayMph, Translation.GetString("Display_speed_limits_mph"));

            if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph != displayMph) {
                OptionsGeneralTab.SetDisplayInMph(displayMph);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            DragWindow(ref paletteWindowRect);
        }

        /// <summary>Helper to create speed limit sign + label below converted to the opposite unit</summary>
        /// <param name="showMph">Config value from GlobalConfig.I.M.ShowMPH</param>
        /// <param name="speedLimit">The float speed to show</param>
        private void GuiSpeedLimitsWindow_AddButton(bool showMph, SpeedValue speedLimit) {
            // The button is wrapped in vertical sub-layout and a label for MPH/KMPH is added
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            float signSize = TrafficManagerTool.AdaptWidth(GUI_SPEED_SIGN_SIZE);
            if (GUILayout.Button(
                SpeedLimitTextures.GetSpeedLimitTexture(speedLimit),
                GUILayout.Width(signSize),
                GUILayout.Height(signSize * GetVerticalTextureScale()))) {
                currentPaletteSpeedLimit = speedLimit;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // For MPH setting display KM/H below, for KM/H setting display MPH
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                showMph
                    ? ToKmphPreciseString(speedLimit)
                    : ToMphPreciseString(speedLimit));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private bool drawSpeedLimitHandles(ushort segmentId,
                                           ref NetSegment segment,
                                           bool viewOnly,
                                           ref Vector3 camPos)
        {
            if (viewOnly && !Options.speedLimitsOverlay) {
                return false;
            }

            Vector3 center = segment.m_bounds.center;
            NetManager netManager = Singleton<NetManager>.instance;
            var hovered = false;
            SpeedValue speedLimitToSet = viewOnly
                                             ? new SpeedValue(-1f)
                                             : currentPaletteSpeedLimit;
            bool showPerLane = showLimitsPerLane;

            if (!viewOnly) {
                showPerLane = showLimitsPerLane ^
                              (Input.GetKey(KeyCode.LeftControl) ||
                               Input.GetKey(KeyCode.RightControl));
            }

            // US signs are rectangular, all other are round
            float speedLimitSignVerticalScale = GetVerticalTextureScale();

            if (showPerLane) {
                // show individual speed limit handle per lane
                int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(
                    segmentId,
                    null,
                    out int numDirections,
                    SpeedLimitManager.VEHICLE_TYPES);

                NetInfo segmentInfo = segment.Info;
                Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;
                Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;

                // if ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) {
                //        xu = -xu; }
                float f = viewOnly ? 4f : 7f; // reserved sign size in game coordinates
                Vector3 zero = center - (0.5f * (numLanes - 1 + numDirections - 1) * f * xu);

                uint x = 0;
                IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(
                    segmentId,
                    ref segment,
                    null,
                    SpeedLimitManager.LANE_TYPES,
                    SpeedLimitManager.VEHICLE_TYPES);
                bool onlyMonorailLanes = sortedLanes.Count > 0;

                if (!viewOnly) {
                    foreach (LanePos laneData in sortedLanes) {
                        byte laneIndex = laneData.laneIndex;
                        NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                        if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) ==
                            VehicleInfo.VehicleType.None) {
                            onlyMonorailLanes = false;
                            break;
                        }
                    }
                }

                var directions = new HashSet<NetInfo.Direction>();
                int sortedLaneIndex = -1;

                foreach (LanePos laneData in sortedLanes) {
                    ++sortedLaneIndex;
                    uint laneId = laneData.laneId;
                    byte laneIndex = laneData.laneIndex;

                    NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                    if (!directions.Contains(laneInfo.m_finalDirection)) {
                        if (directions.Count > 0) {
                            ++x; // space between different directions
                        }

                        directions.Add(laneInfo.m_finalDirection);
                    }

                    SpeedValue laneSpeedLimit = new SpeedValue(
                        SpeedLimitManager.Instance.GetCustomSpeedLimit(laneId));

                    bool hoveredHandle = MainTool.DrawGenericOverlayGridTexture(
                        SpeedLimitTextures.GetSpeedLimitTexture(laneSpeedLimit),
                        camPos,
                        zero,
                        f,
                        f,
                        xu,
                        yu,
                        x,
                        0,
                        speedLimitSignSize,
                        speedLimitSignSize * speedLimitSignVerticalScale,
                        !viewOnly);

                    if (!viewOnly
                        && !onlyMonorailLanes
                        && (laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) !=
                        VehicleInfo.VehicleType.None)
                    {
                        Texture2D tex1 = RoadUITextures.VehicleInfoSignTextures[
                            LegacyExtVehicleType.ToNew(ExtVehicleType.PassengerTrain)];
                        MainTool.DrawStaticSquareOverlayGridTexture(
                            tex1,
                            camPos,
                            zero,
                            f,
                            xu,
                            yu,
                            x,
                            1,
                            speedLimitSignSize);
                    }

                    if (hoveredHandle) {
                        hovered = true;
                    }

                    if (hoveredHandle && Input.GetMouseButton(0) && !IsCursorInPanel()) {
                        SpeedLimitManager.Instance.SetSpeedLimit(
                            segmentId,
                            laneIndex,
                            laneInfo,
                            laneId,
                            speedLimitToSet.GameUnits);

                        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                            int slIndexCopy = sortedLaneIndex;

                            SegmentLaneTraverser.Traverse(
                                segmentId,
                                SegmentTraverser.TraverseDirection.AnyDirection,
                                SegmentTraverser.TraverseSide.AnySide,
                                SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                                SegmentTraverser.SegmentStopCriterion.Junction,
                                SpeedLimitManager.LANE_TYPES,
                                SpeedLimitManager.VEHICLE_TYPES,
                                data => {
                                    if (data.SegVisitData.Initial) {
                                        return true;
                                    }

                                    if (slIndexCopy != data.SortedLaneIndex) {
                                        return true;
                                    }

                                    Constants.ServiceFactory.NetService.ProcessSegment(
                                        data.SegVisitData.CurSeg.segmentId,
                                        (ushort curSegmentId, ref NetSegment curSegment) =>
                                        {
                                            NetInfo.Lane curLaneInfo = curSegment.Info.m_lanes[
                                                data.CurLanePos.laneIndex];

                                            SpeedLimitManager.Instance.SetSpeedLimit(
                                                curSegmentId,
                                                data.CurLanePos.laneIndex,
                                                curLaneInfo,
                                                data.CurLanePos.laneId,
                                                speedLimitToSet.GameUnits);
                                            return true;
                                        });

                                    return true;
                                });
                        }
                    }

                    ++x;
                }
            } else {
                // draw speedlimits over mean middle points of lane beziers

                if (!segmentCenterByDir.TryGetValue(
                        segmentId,
                        out Dictionary<NetInfo.Direction, Vector3> segCenter)) {
                    segCenter = new Dictionary<NetInfo.Direction, Vector3>();
                    segmentCenterByDir.Add(segmentId, segCenter);
                    TrafficManagerTool.CalculateSegmentCenterByDir(segmentId, segCenter);
                }

                foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
                    bool visible = MainTool.WorldToScreenPoint(e.Value, out Vector3 screenPos);

                    if (!visible) {
                        continue;
                    }

                    float zoom = 1.0f / (e.Value - camPos).magnitude * 100f * MainTool.GetBaseZoom();
                    float size = (viewOnly ? 0.8f : 1f) * speedLimitSignSize * zoom;
                    Color guiColor = GUI.color;
                    var boundingBox = new Rect(screenPos.x - (size / 2),
                                               screenPos.y - (size / 2),
                                               size,
                                               size * speedLimitSignVerticalScale);
                    bool hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);

                    guiColor.a = TrafficManagerTool.GetHandleAlpha(hoveredHandle);

                    if (hoveredHandle) {
                        // mouse hovering over sign
                        hovered = true;
                    }

                    // Draw something right here, the road sign texture
                    GUI.color = guiColor;
                    SpeedValue displayLimit = new SpeedValue(
                        SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, e.Key));
                    Texture2D tex = SpeedLimitTextures.GetSpeedLimitTexture(displayLimit);

                    GUI.DrawTexture(boundingBox, tex);

                    if (hoveredHandle && Input.GetMouseButton(0) && !IsCursorInPanel()) {
                        // change the speed limit to the selected one
                        // Log._Debug($"Setting speed limit of segment {segmentId}, dir {e.Key.ToString()}
                        //     to {speedLimitToSet}");
                        SpeedLimitManager.Instance.SetSpeedLimit(segmentId,
                                                                 e.Key,
                                                                 currentPaletteSpeedLimit.GameUnits);

                        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                            NetInfo.Direction normDir = e.Key;
                            if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
                                normDir = NetInfo.InvertDirection(normDir);
                            }

                            SegmentLaneTraverser.Traverse(
                                segmentId,
                                SegmentTraverser.TraverseDirection.AnyDirection,
                                SegmentTraverser.TraverseSide.AnySide,
                                SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                                SegmentTraverser.SegmentStopCriterion.Junction,
                                SpeedLimitManager.LANE_TYPES,
                                SpeedLimitManager.VEHICLE_TYPES,
                                data =>
                                {
                                    if (data.SegVisitData.Initial) {
                                        return true;
                                    }

                                    bool reverse =
                                        data.SegVisitData.ViaStartNode
                                        == data.SegVisitData.ViaInitialStartNode;

                                    ushort otherSegmentId = data.SegVisitData.CurSeg.segmentId;
                                    NetInfo otherSegmentInfo =
                                        netManager.m_segments.m_buffer[otherSegmentId].Info;
                                    byte laneIndex = data.CurLanePos.laneIndex;
                                    NetInfo.Lane laneInfo = otherSegmentInfo.m_lanes[laneIndex];

                                    NetInfo.Direction otherNormDir = laneInfo.m_finalDirection;

                                    if ((netManager.m_segments.m_buffer[otherSegmentId].m_flags
                                         & NetSegment.Flags.Invert)
                                        != NetSegment.Flags.None ^ reverse)
                                    {
                                        otherNormDir = NetInfo.InvertDirection(otherNormDir);
                                    }

                                    if (otherNormDir == normDir) {
                                        SpeedLimitManager.Instance.SetSpeedLimit(
                                            otherSegmentId,
                                            laneInfo.m_finalDirection,
                                            speedLimitToSet.GameUnits);
                                    }

                                    return true;
                                });
                        }
                    }

                    guiColor.a = 1f;
                    GUI.color = guiColor;
                }
            }

            return hovered;
        }

        public static string ToMphPreciseString(SpeedValue speed) {
            return FloatUtil.IsZero(speed.GameUnits)
                       ? Translation.GetString("Speed_limit_unlimited")
                       : speed.ToMphPrecise().ToString();
        }

        public static string ToKmphPreciseString(SpeedValue speed) {
            return FloatUtil.IsZero(speed.GameUnits)
                       ? Translation.GetString("Speed_limit_unlimited")
                       : speed.ToKmphPrecise().ToString();
        }

        /// <summary>
        /// Produces list of speed limits to offer user in the palette
        /// </summary>
        /// <param name="unit">What kind of speed limit list is required</param>
        /// <returns>List from smallest to largest speed with the given unit. Zero (no limit) is not added to the list.
        /// The values are in-game speeds as float.</returns>
        public static List<SpeedValue> EnumerateSpeedLimits(SpeedUnit unit) {
            var result = new List<SpeedValue>();
            switch (unit) {
                case SpeedUnit.Kmph:
                    for (var km = LOWER_KMPH; km <= UPPER_KMPH; km += KMPH_STEP) {
                        result.Add(SpeedValue.FromKmph(km));
                    }

                    break;
                case SpeedUnit.Mph:
                    for (var mi = LOWER_MPH; mi <= UPPER_MPH; mi += MPH_STEP) {
                        result.Add(SpeedValue.FromMph(mi));
                    }

                    break;
                case SpeedUnit.CurrentlyConfigured:
                    // Automatically choose from the config
                    return GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                               ? EnumerateSpeedLimits(SpeedUnit.Mph)
                               : EnumerateSpeedLimits(SpeedUnit.Kmph);
            }

            return result;
        }

        /// <summary>
        /// Based on the MPH/KMPH settings round the current speed to the nearest STEP and
        /// then decrease by STEP.
        /// </summary>
        /// <param name="speed">Ingame speed</param>
        /// <returns>Ingame speed decreased by the increment for MPH or KMPH</returns>
        public static SpeedValue GetPrevious(SpeedValue speed) {
            if (speed.GameUnits < 0f) {
                return new SpeedValue(-1f);
            }

            if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph) {
                MphValue rounded = speed.ToMphRounded(MPH_STEP);
                if (rounded.Mph == LOWER_MPH) {
                    return new SpeedValue(0);
                }

                if (rounded.Mph == 0) {
                    return SpeedValue.FromMph(UPPER_MPH);
                }

                return SpeedValue.FromMph(rounded.Mph > LOWER_MPH
                                              ? (ushort)(rounded.Mph - MPH_STEP)
                                              : LOWER_MPH);
            } else {
                KmphValue rounded = speed.ToKmphRounded(KMPH_STEP);
                if (rounded.Kmph == LOWER_KMPH) {
                    return new SpeedValue(0);
                }

                if (rounded.Kmph == 0) {
                    return SpeedValue.FromKmph(UPPER_KMPH);
                }

                return SpeedValue.FromKmph(rounded.Kmph > LOWER_KMPH
                                               ? (ushort)(rounded.Kmph - KMPH_STEP)
                                               : LOWER_KMPH);
            }
        }

        /// <summary>
        /// Based on the MPH/KMPH settings round the current speed to the nearest STEP and
        /// then increase by STEP.
        /// </summary>
        /// <param name="speed">Ingame speed</param>
        /// <returns>Ingame speed increased by the increment for MPH or KMPH</returns>
        public static SpeedValue GetNext(SpeedValue speed) {
            if (speed.GameUnits < 0f) {
                return new SpeedValue(-1f);
            }

            if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph) {
                MphValue rounded = speed.ToMphRounded(MPH_STEP);
                rounded += MPH_STEP;

                if (rounded.Mph > UPPER_MPH) {
                    rounded = new MphValue(0);
                }

                return SpeedValue.FromMph(rounded);
            } else {
                KmphValue rounded = speed.ToKmphRounded(KMPH_STEP);
                rounded += KMPH_STEP;

                if (rounded.Kmph > UPPER_KMPH) {
                    rounded = new KmphValue(0);
                }

                return SpeedValue.FromKmph(rounded);
            }
        }

        /// <summary>
        /// For US signs and MPH enabled, scale textures vertically by 1.25f.
        /// Other signs are round.
        /// </summary>
        /// <returns>Multiplier for horizontal sign size</returns>
        public static float GetVerticalTextureScale() {
            return (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph &&
                    GlobalConfig.Instance.Main.MphRoadSignStyle == MphSignStyle.SquareUS)
                       ? 1.25f
                       : 1.0f;
        }

    } // end class
}