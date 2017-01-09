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
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using TrafficManager.Util;

namespace TrafficManager.UI.SubTools {
	public class PrioritySignsTool : SubTool {
		private HashSet<ushort> currentPrioritySegmentIds;
		SegmentEnd[] prioritySegments = new SegmentEnd[2];

		public PrioritySignsTool(TrafficManagerTool mainTool) : base(mainTool) {
			currentPrioritySegmentIds = new HashSet<ushort>();
		}

		public override void OnPrimaryClickOverlay() {
			
		}

		public override void OnToolGUI(Event e) {
			//ShowGUI(false);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
				return;
			}

			// no highlight for existing priority node in sign mode
			if (TrafficPriorityManager.Instance.IsPriorityNode(HoveredNodeId, false))
				return;

			if (HoveredNodeId == 0) return;

			if (!Flags.mayHaveTrafficLight(HoveredNodeId)) return;

			MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
		}

		private void RefreshCurrentPrioritySegmentIds() {
			currentPrioritySegmentIds.Clear();
			for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
				if (!NetUtil.IsSegmentValid(segmentId))
					continue;

				var trafficSegment = TrafficPriorityManager.Instance.TrafficSegments[segmentId];
				if (trafficSegment == null)
					continue;

				currentPrioritySegmentIds.Add((ushort)segmentId);
			}
		}

		public override void ShowGUIOverlay(bool viewOnly) {
			if (viewOnly && !Options.prioritySignsOverlay)
				return;

			if (TrafficManagerTool.GetToolMode() == ToolMode.JunctionRestrictions)
				return;

			ShowGUI(viewOnly);
		}

		public void ShowGUI(bool viewOnly) {
			try {
				TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
				TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;
				TrafficLightManager tlm = TrafficLightManager.Instance;

				bool clicked = !viewOnly ? MainTool.CheckClicked() : false;
				var hoveredSegment = false;
				//Log.Message("_guiPrioritySigns called. num of prio segments: " + TrafficPriority.PrioritySegments.Count);

				HashSet<ushort> nodeIdsWithSigns = new HashSet<ushort>();
				foreach (ushort segmentId in currentPrioritySegmentIds) {
					var trafficSegment = prioMan.TrafficSegments[segmentId];
					if (trafficSegment == null)
						continue;
					SegmentGeometry geometry = SegmentGeometry.Get(segmentId);

					prioritySegments[0] = null;
					prioritySegments[1] = null;

					if (tlsMan.GetNodeSimulation(trafficSegment.Node1) == null) {
						SegmentEnd tmpSeg1 = prioMan.GetPrioritySegment(trafficSegment.Node1, segmentId);
						bool startNode = geometry.StartNodeId() == trafficSegment.Node1;
						if (tmpSeg1 != null && !geometry.IsOutgoingOneWay(startNode)) {
							prioritySegments[0] = tmpSeg1;
							nodeIdsWithSigns.Add(trafficSegment.Node1);
							prioMan.AddPriorityNode(trafficSegment.Node1);
						}
					}
					if (tlsMan.GetNodeSimulation(trafficSegment.Node2) == null) {
						SegmentEnd tmpSeg2 = prioMan.GetPrioritySegment(trafficSegment.Node2, segmentId);
						bool startNode = geometry.StartNodeId() == trafficSegment.Node2;
						if (tmpSeg2 != null && !geometry.IsOutgoingOneWay(startNode)) {
							prioritySegments[1] = tmpSeg2;
							nodeIdsWithSigns.Add(trafficSegment.Node2);
							prioMan.AddPriorityNode(trafficSegment.Node2);
						}
					}

					//Log.Message("init ok");

					foreach (var prioritySegment in prioritySegments) {
						if (prioritySegment == null)
							continue;

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
							foreach (var otherPrioritySegment in prioMan.GetPrioritySegments(nodeId)) {
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
					var zoom = 1.0f / diff.magnitude * 100f * MainTool.GetBaseZoom();
					var size = 90f * zoom;
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
					if (hoveredExistingNodeId != 0) {
						delete = true;
					}

					// determine if we may add new priority signs to this node
					bool ok = false;
					TrafficLightSimulation nodeSim = tlsMan.GetNodeSimulation(HoveredNodeId);
					if ((Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
						// no traffic light set
						ok = true;
					} else if (nodeSim == null || !nodeSim.IsTimedLight()) {
						ok = true;
					}

					if (!Flags.mayHaveTrafficLight(HoveredNodeId))
						ok = false;

					if (clicked) {
						Log._Debug("_guiPrioritySigns: hovered+clicked @ nodeId=" + HoveredNodeId + "/" + hoveredExistingNodeId + " ok=" + ok);

						if (delete) {
							prioMan.RemovePrioritySegments(hoveredExistingNodeId);
							RefreshCurrentPrioritySegmentIds();
						} else if (ok) {
							//if (!prioMan.IsPriorityNode(HoveredNodeId)) {
								Log._Debug("_guiPrioritySigns: adding prio segments @ nodeId=" + HoveredNodeId);
								tlsMan.RemoveNodeFromSimulation(HoveredNodeId, false, true);
								tlm.RemoveTrafficLight(HoveredNodeId);
								prioMan.AddPriorityNode(HoveredNodeId);
								RefreshCurrentPrioritySegmentIds();
							//}
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
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			var numMainRoads = 0;
			for (var s = 0; s < 8; s++) {
				var segmentId2 = node.GetSegment(s);

				if (segmentId2 == 0 ||
					!prioMan.IsPrioritySegment(nodeId, segmentId2))
					continue;
				var prioritySegment2 = prioMan.GetPrioritySegment(nodeId,
					segmentId2);

				if (prioritySegment2.Type == SegmentEnd.PriorityType.Main) {
					numMainRoads++;
				}
			}
			return numMainRoads;
		}

		public override void Cleanup() {
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			foreach (TrafficSegment trafficSegment in prioMan.TrafficSegments) {
				try {
					trafficSegment?.Instance1?.Reset();
					trafficSegment?.Instance2?.Reset();
				} catch (Exception e) {
					Log.Error($"Error occured while performing PrioritySignsTool.Cleanup: {e.ToString()}");
				}
			}

			RefreshCurrentPrioritySegmentIds();
		}

		public override void Initialize() {
			Cleanup();
		}
	}
}
