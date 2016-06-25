using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.UI.SubTools {
	public class PrioritySignsTool : SubTool {
		public PrioritySignsTool(TrafficManagerTool mainTool) : base(mainTool) {
			
		}

		public override void OnClickOverlay() {
			
		}

		public override void OnToolGUI(Event e) {
			ShowGUI(false);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
				return;
			}

			// no highlight for existing priority node in sign mode
			if (TrafficPriority.IsPriorityNode(HoveredNodeId))
				return;

			if (HoveredNodeId == 0) return;

			if (!Flags.mayHaveTrafficLight(HoveredNodeId)) return;

			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_segment0];

			Bezier3 bezier;
			bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;
			bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;

			var color = MainTool.GetToolColor(Input.GetMouseButton(0), false);

			NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
				segment.m_endDirection,
				false, false, out bezier.b, out bezier.c);

			MainTool.DrawOverlayBezier(cameraInfo, bezier, color);
		}

		public override void ShowIcons() {
			ShowGUI(true);
		}

		public void ShowGUI(bool viewOnly) {
			try {
				if (viewOnly && !Options.prioritySignsOverlay)
					return;

				bool clicked = !viewOnly ? MainTool.CheckClicked() : false;
				var hoveredSegment = false;
				//Log.Message("_guiPrioritySigns called. num of prio segments: " + TrafficPriority.PrioritySegments.Count);

				HashSet<ushort> nodeIdsWithSigns = new HashSet<ushort>();
				for (ushort segmentId = 0; segmentId < TrafficPriority.TrafficSegments.Length; ++segmentId) {
					var trafficSegment = TrafficPriority.TrafficSegments[segmentId];
					if (trafficSegment == null)
						continue;
					SegmentGeometry geometry = SegmentGeometry.Get(segmentId);

					List<SegmentEnd> prioritySegments = new List<SegmentEnd>();
					if (TrafficLightSimulation.GetNodeSimulation(trafficSegment.Node1) == null) {
						SegmentEnd tmpSeg1 = TrafficPriority.GetPrioritySegment(trafficSegment.Node1, segmentId);
						bool startNode = geometry.StartNodeId() == trafficSegment.Node1;
						if (tmpSeg1 != null && !geometry.IsOutgoingOneWay(startNode)) {
							prioritySegments.Add(tmpSeg1);
							nodeIdsWithSigns.Add(trafficSegment.Node1);
						}
					}
					if (TrafficLightSimulation.GetNodeSimulation(trafficSegment.Node2) == null) {
						SegmentEnd tmpSeg2 = TrafficPriority.GetPrioritySegment(trafficSegment.Node2, segmentId);
						bool startNode = geometry.StartNodeId() == trafficSegment.Node2;
						if (tmpSeg2 != null && !geometry.IsOutgoingOneWay(startNode)) {
							prioritySegments.Add(tmpSeg2);
							nodeIdsWithSigns.Add(trafficSegment.Node2);
						}
					}

					//Log.Message("init ok");

					foreach (var prioritySegment in prioritySegments) {
						var nodeId = prioritySegment.NodeId;
						//Log.Message("_guiPrioritySigns: nodeId=" + nodeId);

						var nodePositionVector3 = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
						var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
						var diff = nodePositionVector3 - camPos;
						if (diff.magnitude > TrafficManagerTool.PriorityCloseLod)
							continue; // do not draw if too distant

						if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode == (ushort)nodeId) {
							nodePositionVector3.x += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startDirection.x * 10f;
							nodePositionVector3.y += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startDirection.y * 10f;
							nodePositionVector3.z += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startDirection.z * 10f;
						} else {
							nodePositionVector3.x += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endDirection.x * 10f;
							nodePositionVector3.y += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endDirection.y * 10f;
							nodePositionVector3.z += Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endDirection.z * 10f;
						}

						var nodeScreenPosition = Camera.main.WorldToScreenPoint(nodePositionVector3);
						nodeScreenPosition.y = Screen.height - nodeScreenPosition.y;
						if (nodeScreenPosition.z < 0)
							continue;
						var zoom = 1.0f / diff.magnitude * 100f * MainTool.GetBaseZoom();
						var size = 110f * zoom;
						var guiColor = GUI.color;
						var nodeBoundingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);
						hoveredSegment = !viewOnly && TrafficManagerTool.IsMouseOver(nodeBoundingBox);

						if (hoveredSegment) {
							// mouse hovering over sign
							guiColor.a = 0.8f;
						} else {
							guiColor.a = 0.5f;
							size = 90f * zoom;
						}
						var nodeDrawingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);

						GUI.color = guiColor;

						bool setUndefinedSignsToMainRoad = false;
						switch (prioritySegment.Type) {
							case SegmentEnd.PriorityType.Main:
								GUI.DrawTexture(nodeDrawingBox, TrafficLightToolTextureResources.SignPriorityTexture2D);
								if (clicked && hoveredSegment) {
									//Log._Debug("Click on node " + nodeId + ", segment " + segmentId + " to change prio type (1)");
									//Log.Message("PrioritySegment.Type = Yield");
									prioritySegment.Type = SegmentEnd.PriorityType.Yield;
									setUndefinedSignsToMainRoad = true;
									clicked = false;
								}
								break;
							case SegmentEnd.PriorityType.Yield:
								GUI.DrawTexture(nodeDrawingBox, TrafficLightToolTextureResources.SignYieldTexture2D);
								if (clicked && hoveredSegment) {
									//Log._Debug("Click on node " + nodeId + ", segment " + segmentId + " to change prio type (2)");
									prioritySegment.Type = SegmentEnd.PriorityType.Stop;
									setUndefinedSignsToMainRoad = true;
									clicked = false;
								}

								break;
							case SegmentEnd.PriorityType.Stop:
								GUI.DrawTexture(nodeDrawingBox, TrafficLightToolTextureResources.SignStopTexture2D);
								if (clicked && hoveredSegment) {
									//Log._Debug("Click on node " + nodeId + ", segment " + segmentId + " to change prio type (3)");
									prioritySegment.Type = SegmentEnd.PriorityType.Main;
									clicked = false;
								}
								break;
							case SegmentEnd.PriorityType.None:
								if (viewOnly)
									break;
								GUI.DrawTexture(nodeDrawingBox, TrafficLightToolTextureResources.SignNoneTexture2D);

								if (clicked && hoveredSegment) {
									//Log._Debug("Click on node " + nodeId + ", segment " + segmentId + " to change prio type (4)");
									//Log.Message("PrioritySegment.Type = None");
									prioritySegment.Type = GetNumberOfMainRoads(nodeId, ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId]) >= 2
										? SegmentEnd.PriorityType.Yield
										: SegmentEnd.PriorityType.Main;
									if (prioritySegment.Type == SegmentEnd.PriorityType.Yield)
										setUndefinedSignsToMainRoad = true;
									clicked = false;
								}
								break;
						}

						if (setUndefinedSignsToMainRoad) {
							foreach (var otherPrioritySegment in TrafficPriority.GetPrioritySegments(nodeId)) {
								if (otherPrioritySegment.SegmentId == prioritySegment.SegmentId)
									continue;
								if (otherPrioritySegment.Type == SegmentEnd.PriorityType.None)
									otherPrioritySegment.Type = SegmentEnd.PriorityType.Main;
							}
						}
					}
				}

				if (viewOnly)
					return;

				ushort hoveredExistingNodeId = 0;
				foreach (ushort nodeId in nodeIdsWithSigns) {
					var nodePositionVector3 = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
					var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
					var diff = nodePositionVector3 - camPos;
					if (diff.magnitude > TrafficManagerTool.PriorityCloseLod)
						continue;

					// draw deletion button
					var nodeScreenPosition = Camera.main.WorldToScreenPoint(nodePositionVector3);
					nodeScreenPosition.y = Screen.height - nodeScreenPosition.y;
					if (nodeScreenPosition.z < 0)
						continue;
					var zoom = 1.0f / diff.magnitude * 100f;
					var size = 100f * zoom;
					var nodeBoundingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);

					var guiColor = GUI.color;
					var nodeCenterHovered = TrafficManagerTool.IsMouseOver(nodeBoundingBox);
					if (nodeCenterHovered) {
						hoveredExistingNodeId = nodeId;
						guiColor.a = 0.8f;
					} else {
						guiColor.a = 0.5f;
					}
					GUI.color = guiColor;

					GUI.DrawTexture(nodeBoundingBox, TrafficLightToolTextureResources.SignRemoveTexture2D);
				}

				// add a new or delete a priority segment node
				if (HoveredNodeId != 0 || hoveredExistingNodeId != 0) {
					bool delete = false;
					if (hoveredExistingNodeId > 0) {
						delete = true;
					}

					// determine if we may add new priority signs to this node
					bool ok = false;
					TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(HoveredNodeId);
					if ((Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
						// no traffic light set
						ok = true;
					} else if (nodeSim == null || !nodeSim.IsTimedLight()) {
						ok = true;
					}

					if (!Flags.mayHaveTrafficLight(HoveredNodeId))
						ok = false;

					if (clicked) {
						//Log._Debug("_guiPrioritySigns: hovered+clicked @ nodeId=" + HoveredNodeId + "/" + hoveredExistingNodeId);

						if (delete) {
							TrafficPriority.RemovePrioritySegments(hoveredExistingNodeId);
						} else if (ok) {
							if (!TrafficPriority.IsPriorityNode(HoveredNodeId)) {
								//Log._Debug("_guiPrioritySigns: adding prio segments @ nodeId=" + HoveredNodeId);
								TrafficLightSimulation.RemoveNodeFromSimulation(HoveredNodeId, false, true);
								Flags.setNodeTrafficLight(HoveredNodeId, false); // TODO refactor!
								TrafficPriority.AddPriorityNode(HoveredNodeId);
							}
						} else if (nodeSim != null && nodeSim.IsTimedLight()) {
							MainTool.ShowTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"), Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position);
						}
					}
				}
			} catch (Exception e) {
				Log.Error(e.ToString());
			}
		}

		private static int GetNumberOfMainRoads(ushort nodeId, ref NetNode node) {
			var numMainRoads = 0;
			for (var s = 0; s < 8; s++) {
				var segmentId2 = node.GetSegment(s);

				if (segmentId2 == 0 ||
					!TrafficPriority.IsPrioritySegment(nodeId, segmentId2))
					continue;
				var prioritySegment2 = TrafficPriority.GetPrioritySegment(nodeId,
					segmentId2);

				if (prioritySegment2.Type == SegmentEnd.PriorityType.Main) {
					numMainRoads++;
				}
			}
			return numMainRoads;
		}

		public override void Cleanup() {
			HashSet<ushort> priorityNodeIds = TrafficPriority.GetPriorityNodes();
			foreach (ushort nodeId in priorityNodeIds) {
				foreach (SegmentEnd end in TrafficPriority.GetPrioritySegments(nodeId)) {
					end.Housekeeping();
				}
			}
		}
	}
}
