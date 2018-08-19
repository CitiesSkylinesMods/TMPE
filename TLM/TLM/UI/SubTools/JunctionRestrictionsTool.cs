#define DEBUGCONNx

using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using TrafficManager.Manager;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using TrafficManager.Geometry.Impl;
using ColossalFramework.UI;

namespace TrafficManager.UI.SubTools {
	public class JunctionRestrictionsTool : SubTool {

		private HashSet<ushort> currentRestrictedNodeIds;
		private bool overlayHandleHovered;
		private readonly float junctionRestrictionsSignSize = 80f;

		public JunctionRestrictionsTool(TrafficManagerTool mainTool) : base(mainTool) {
			currentRestrictedNodeIds = new HashSet<ushort>();
		}

		public override void OnToolGUI(Event e) {
			/*if (SelectedNodeId != 0) {
				overlayHandleHovered = false;
			}
			ShowSigns(false);*/
		}

		public override void RenderInfoOverlay(RenderManager.CameraInfo cameraInfo) {
			
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			if (SelectedNodeId != 0) {
				// draw selected node
				MainTool.DrawNodeCircle(cameraInfo, SelectedNodeId, true);
			}

			if (HoveredNodeId != 0 && HoveredNodeId != SelectedNodeId && (Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Bend)) != NetNode.Flags.None) {
				// draw hovered node
				MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
			}
		}

		public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
			if (viewOnly && !Options.junctionRestrictionsOverlay)
				return;

			if (SelectedNodeId != 0) {
				overlayHandleHovered = false;
			}

			ShowSigns(viewOnly);
		}

		private void ShowSigns(bool viewOnly) {
			bool debug = false;
#if DEBUG
			debug = !viewOnly && GlobalConfig.Instance.Debug.Switches[11];
#endif
			NetManager netManager = Singleton<NetManager>.instance;
			var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;

			if (!viewOnly && SelectedNodeId != 0) {
				currentRestrictedNodeIds.Add(SelectedNodeId);
			}

			ushort updatedNodeId = 0;
			bool handleHovered = false;
			bool cursorInPanel = this.IsCursorInPanel();
			foreach (ushort nodeId in currentRestrictedNodeIds) {
				if (!Constants.ServiceFactory.NetService.IsNodeValid(nodeId)) {
					continue;
				}

				Vector3 nodePos = netManager.m_nodes.m_buffer[nodeId].m_position;
				var diff = nodePos - camPos;

				if (diff.magnitude > TrafficManagerTool.MaxOverlayDistance)
					continue; // do not draw if too distant

				Vector3 screenPos;
				bool visible = MainTool.WorldToScreenPoint(nodePos, out screenPos);

				if (! visible)
					continue;

				bool viewOnlyNode = true;
				if (!viewOnly && nodeId == SelectedNodeId)
					viewOnlyNode = false;

				// draw junction restrictions
				bool update;
				if (drawSignHandles(debug, nodeId, ref netManager.m_nodes.m_buffer[nodeId], viewOnlyNode, !cursorInPanel, ref camPos, out update))
					handleHovered = true;

				if (update) {
					updatedNodeId = nodeId;
				}
			}
			overlayHandleHovered = handleHovered;

			if (updatedNodeId != 0) {
				RefreshCurrentRestrictedNodeIds(updatedNodeId);
			}
		}

		public override void OnPrimaryClickOverlay() {
			bool debug = false;
#if DEBUG
			debug = GlobalConfig.Instance.Debug.Switches[11];
#endif
			if (HoveredNodeId == 0) return;
			if (overlayHandleHovered) return;
			if (!debug && (Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags & (NetNode.Flags.Junction | NetNode.Flags.Bend)) == NetNode.Flags.None)
				return;

			SelectedNodeId = HoveredNodeId;
			MainTool.CheckClicked(); // prevent accidential activation of signs on node selection (TODO improve this!)
		}

		public override void OnSecondaryClickOverlay() {
			SelectedNodeId = 0;
		}

		public override void OnActivate() {
#if DEBUGCONN
			Log._Debug("TppLaneConnectorTool: OnActivate");
#endif
			SelectedNodeId = 0;
			RefreshCurrentRestrictedNodeIds();
		}

		public override void Cleanup() {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[11];
#endif
			foreach (ushort nodeId in currentRestrictedNodeIds) {
				JunctionRestrictionsManager.Instance.RemoveJunctionRestrictionsIfNecessary(nodeId);
			}
			RefreshCurrentRestrictedNodeIds();
		}

		public override void Initialize() {
			base.Initialize();
			Cleanup();
			if (Options.junctionRestrictionsOverlay) {
				RefreshCurrentRestrictedNodeIds();
			} else {
				currentRestrictedNodeIds.Clear();
			}
		}

		private void RefreshCurrentRestrictedNodeIds(ushort forceNodeId=0) {
			if (forceNodeId == 0) {
				currentRestrictedNodeIds.Clear();
			} else {
				currentRestrictedNodeIds.Remove(forceNodeId);
			}

			for (uint nodeId = (forceNodeId == 0 ? 1u : forceNodeId); nodeId <= (forceNodeId == 0 ? NetManager.MAX_NODE_COUNT - 1 : forceNodeId); ++nodeId) {
				if (!Constants.ServiceFactory.NetService.IsNodeValid((ushort)nodeId)) {
					continue;
				}

				if (JunctionRestrictionsManager.Instance.HasJunctionRestrictions((ushort)nodeId)) {
					currentRestrictedNodeIds.Add((ushort)nodeId);
				}
			}
		}

		private bool drawSignHandles(bool debug, ushort nodeId, ref NetNode node, bool viewOnly, bool handleClick, ref Vector3 camPos, out bool stateUpdated) {
			bool hovered = false;
			stateUpdated = false;

			if (viewOnly && !Options.junctionRestrictionsOverlay && MainTool.GetToolMode() != ToolMode.JunctionRestrictions)
				return false;

			//NetManager netManager = Singleton<NetManager>.instance;
			var guiColor = GUI.color;

			Vector3 nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;

			for (int i = 0; i < 8; ++i) {
				ushort segmentId = node.GetSegment(i);
				if (segmentId == 0)
					continue;

				SegmentGeometry geometry = SegmentGeometry.Get(segmentId);
				if (geometry == null) {
					Log.Error($"JunctionRestrictionsTool.drawSignHandles: No geometry information available for segment {segmentId}");
					continue;
				}
				bool startNode = geometry.StartNodeId == nodeId;
				bool incoming = geometry.IsIncoming(startNode);

				int numSignsPerRow = incoming ? 2 : 1;

				NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

				ItemClass connectionClass = segmentInfo.GetConnectionClass();
				if (connectionClass.m_service != ItemClass.Service.Road)
					continue; // only for road junctions

				// draw all junction restriction signs
				Vector3 segmentCenterPos = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_bounds.center;
				Vector3 yu = (segmentCenterPos - nodePos).normalized;
				Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
				float f = viewOnly ? 6f : 7f; // reserved sign size in game coordinates

				Vector3 centerStart = nodePos + yu * (viewOnly ? 5f : 14f);
				Vector3 zero = centerStart - 0.5f * (float)(numSignsPerRow-1) * f * xu; // "top left"
				if (viewOnly) {
					if (Constants.ServiceFactory.SimulationService.LeftHandDrive)
						zero -= xu * 8f;
					else
						zero += xu * 8f;
				}

				bool signHovered;
				int x = 0;
				int y = 0;
				bool hasSignInFirstRow = false;

				// draw "lane-changing when going straight allowed" sign at (0; 0)
				bool allowed = JunctionRestrictionsManager.Instance.IsLaneChangingAllowedWhenGoingStraight(segmentId, startNode);
				bool configurable = Constants.ManagerFactory.JunctionRestrictionsManager.IsLaneChangingAllowedWhenGoingStraightConfigurable(segmentId, startNode, ref node);
				if (
					debug || (configurable &&
					(!viewOnly || allowed != Constants.ManagerFactory.JunctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight(segmentId, startNode, ref node)))
				) {
					DrawSign(viewOnly, !configurable, ref camPos, ref xu, ref yu, f, ref zero, x, y, guiColor, allowed ? TextureResources.LaneChangeAllowedTexture2D : TextureResources.LaneChangeForbiddenTexture2D, out signHovered);
					if (signHovered && handleClick) {
						hovered = true;
						if (MainTool.CheckClicked()) {
							JunctionRestrictionsManager.Instance.ToggleLaneChangingAllowedWhenGoingStraight(segmentId, startNode);
							stateUpdated = true;
						}
					}

					++x;
					hasSignInFirstRow = true;
				}

				// draw "u-turns allowed" sign at (1; 0)
				allowed = JunctionRestrictionsManager.Instance.IsUturnAllowed(segmentId, startNode);
				configurable = Constants.ManagerFactory.JunctionRestrictionsManager.IsUturnAllowedConfigurable(segmentId, startNode, ref node);
				if (
					debug ||
					(configurable &&
					(!viewOnly || allowed != Constants.ManagerFactory.JunctionRestrictionsManager.GetDefaultUturnAllowed(segmentId, startNode, ref node)))
				) {
					DrawSign(viewOnly, !configurable, ref camPos, ref xu, ref yu, f, ref zero, x, y, guiColor, allowed ? TextureResources.UturnAllowedTexture2D : TextureResources.UturnForbiddenTexture2D, out signHovered);
					if (signHovered && handleClick) {
						hovered = true;

						if (MainTool.CheckClicked()) {
							if (!JunctionRestrictionsManager.Instance.ToggleUturnAllowed(segmentId, startNode)) {
								// TODO MainTool.ShowTooltip(Translation.GetString("..."), Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position);
							} else {
								stateUpdated = true;
							}
						}
					}

					hasSignInFirstRow = true;
				}

				x = 0;
				if (hasSignInFirstRow) {
					++y;
				}

				// draw "entering blocked junctions allowed" sign at (0; 1)
				allowed = JunctionRestrictionsManager.Instance.IsEnteringBlockedJunctionAllowed(segmentId, startNode);
				configurable = Constants.ManagerFactory.JunctionRestrictionsManager.IsEnteringBlockedJunctionAllowedConfigurable(segmentId, startNode, ref node);
				if (
					debug ||
					(configurable &&
					(!viewOnly || allowed != Constants.ManagerFactory.JunctionRestrictionsManager.GetDefaultEnteringBlockedJunctionAllowed(segmentId, startNode, ref node)))
				) {
					DrawSign(viewOnly, !configurable, ref camPos, ref xu, ref yu, f, ref zero, x, y, guiColor, allowed ? TextureResources.EnterBlockedJunctionAllowedTexture2D : TextureResources.EnterBlockedJunctionForbiddenTexture2D, out signHovered);
					if (signHovered && handleClick) {
						hovered = true;

						if (MainTool.CheckClicked()) {
							JunctionRestrictionsManager.Instance.ToggleEnteringBlockedJunctionAllowed(segmentId, startNode);
							stateUpdated = true;
						}
					}

					++x;
				}

				// draw "pedestrian crossing allowed" sign at (1; 1)
				allowed = JunctionRestrictionsManager.Instance.IsPedestrianCrossingAllowed(segmentId, startNode);
				configurable = Constants.ManagerFactory.JunctionRestrictionsManager.IsPedestrianCrossingAllowedConfigurable(segmentId, startNode, ref node);
				if (
					debug ||
					(configurable &&
					(!viewOnly || !allowed))
				) {
					DrawSign(viewOnly, !configurable, ref camPos, ref xu, ref yu, f, ref zero, x, y, guiColor, allowed ? TextureResources.PedestrianCrossingAllowedTexture2D : TextureResources.PedestrianCrossingForbiddenTexture2D, out signHovered);
					if (signHovered && handleClick) {
						hovered = true;

						if (MainTool.CheckClicked()) {
							JunctionRestrictionsManager.Instance.TogglePedestrianCrossingAllowed(segmentId, startNode);
							stateUpdated = true;
						}
					}
				}
			}

			guiColor.a = 1f;
			GUI.color = guiColor;

			return hovered;
		}

		private void DrawSign(bool viewOnly, bool small, ref Vector3 camPos, ref Vector3 xu, ref Vector3 yu, float f, ref Vector3 zero, int x, int y, Color guiColor, Texture2D signTexture, out bool hoveredHandle) {
			Vector3 signCenter = zero + f * (float)x * xu + f * (float)y * yu; // in game coordinates

			Vector3 signScreenPos;
			bool visible = MainTool.WorldToScreenPoint(signCenter, out signScreenPos);

			if (!visible) {
				hoveredHandle = false;
				return;
			}

			Vector3 diff = signCenter - camPos;

			var zoom = 1.0f / diff.magnitude * 100f * MainTool.GetBaseZoom();
			var size = (small ? 0.75f : 1f) * (viewOnly ? 0.8f : 1f) * junctionRestrictionsSignSize * zoom;

			var boundingBox = new Rect(signScreenPos.x - size / 2, signScreenPos.y - size / 2, size, size);
			hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);
			guiColor.a = MainTool.GetHandleAlpha(hoveredHandle);

			GUI.color = guiColor;
			GUI.DrawTexture(boundingBox, signTexture);
		}
	}
}
