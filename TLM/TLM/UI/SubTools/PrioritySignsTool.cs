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
using CSUtil.Commons;
using TrafficManager.Geometry.Impl;
using TrafficManager.Manager.Impl;
using static TrafficManager.Traffic.Data.PrioritySegment;
using static TrafficManager.Util.SegmentTraverser;
using ColossalFramework.UI;

namespace TrafficManager.UI.SubTools {
	public class PrioritySignsTool : SubTool {
		public enum PrioritySignsMassEditMode {
			MainYield = 0,
			MainStop = 1,
			YieldMain = 2,
			StopMain = 3,
			Delete = 4
		}

		private HashSet<ushort> currentPriorityNodeIds;
		private PrioritySignsMassEditMode massEditMode = PrioritySignsMassEditMode.MainYield;

		public PrioritySignsTool(TrafficManagerTool mainTool) : base(mainTool) {
			currentPriorityNodeIds = new HashSet<ushort>();
		}

		public override void OnPrimaryClickOverlay() {
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
				if (HoveredSegmentId != 0) {
					SelectedNodeId = 0;

					PriorityType primaryPrioType = PriorityType.None;
					PriorityType secondaryPrioType = PriorityType.None;
					switch (massEditMode) {
						case PrioritySignsMassEditMode.MainYield:
							primaryPrioType = PriorityType.Main;
							secondaryPrioType = PriorityType.Yield;
							break;
						case PrioritySignsMassEditMode.MainStop:
							primaryPrioType = PriorityType.Main;
							secondaryPrioType = PriorityType.Stop;
							break;
						case PrioritySignsMassEditMode.YieldMain:
							primaryPrioType = PriorityType.Yield;
							secondaryPrioType = PriorityType.Main;
							break;
						case PrioritySignsMassEditMode.StopMain:
							primaryPrioType = PriorityType.Stop;
							secondaryPrioType = PriorityType.Main;
							break;
						case PrioritySignsMassEditMode.Delete:
						default:
							break;
					}

					SegmentTraverser.Traverse(HoveredSegmentId, TraverseDirection.AnyDirection, TraverseSide.Straight, SegmentStopCriterion.None, delegate (SegmentVisitData data) {
						foreach (bool startNode in Constants.ALL_BOOL) {
							TrafficPriorityManager.Instance.SetPrioritySign(data.curGeo.SegmentId, startNode, primaryPrioType);

							foreach (ushort otherSegmentId in data.curGeo.GetConnectedSegments(startNode)) {
								if (!data.curGeo.IsStraightSegment(otherSegmentId, startNode)) {
									SegmentGeometry otherGeo = SegmentGeometry.Get(otherSegmentId);
									if (otherGeo == null) {
										continue;
									}
									TrafficPriorityManager.Instance.SetPrioritySign(otherSegmentId, otherGeo.StartNodeId() == data.curGeo.GetNodeId(startNode), secondaryPrioType);
								}
							}
						}
						
						return true;
					});

					// cycle mass edit mode
					massEditMode = (PrioritySignsMassEditMode)(((int)massEditMode + 1) % Enum.GetValues(typeof(PrioritySignsMassEditMode)).GetLength(0));

					// update priority node cache
					RefreshCurrentPriorityNodeIds();
				}
				return;
			}

			if (TrafficPriorityManager.Instance.HasNodePrioritySign(HoveredNodeId)) {
				return;
			}

			if (! MayNodeHavePrioritySigns(HoveredNodeId)) {
				return;
			}

			SelectedNodeId = HoveredNodeId;
			Log._Debug($"PrioritySignsTool.OnPrimaryClickOverlay: SelectedNodeId={SelectedNodeId}");
			// update priority node cache
			RefreshCurrentPriorityNodeIds();
		}

		public override void OnToolGUI(Event e) {
			
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
				return;
			}

			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
				// draw hovered segments
				if (HoveredSegmentId != 0) {
					Color color = MainTool.GetToolColor(Input.GetMouseButton(0), false);
					SegmentTraverser.Traverse(HoveredSegmentId, TraverseDirection.AnyDirection, TraverseSide.Straight, SegmentStopCriterion.None, delegate (SegmentVisitData data) {
						NetTool.RenderOverlay(cameraInfo, ref Singleton<NetManager>.instance.m_segments.m_buffer[data.curGeo.SegmentId], color, color);
						return true;
					});
				} else {
					massEditMode = PrioritySignsMassEditMode.MainYield;
				}
				return;
			}
			massEditMode = PrioritySignsMassEditMode.MainYield;

			if (HoveredNodeId == SelectedNodeId) {
				return;
			}

			// no highlight for existing priority node in sign mode
			if (TrafficPriorityManager.Instance.HasNodePrioritySign(HoveredNodeId)) {
				//Log._Debug($"PrioritySignsTool.RenderOverlay: HasNodePrioritySign({HoveredNodeId})=true");
				return;
			}

			if (! TrafficPriorityManager.Instance.MayNodeHavePrioritySigns(HoveredNodeId)) {
				//Log._Debug($"PrioritySignsTool.RenderOverlay: MayNodeHavePrioritySigns({HoveredNodeId})=false");
				return;
			}

			MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
		}

		private void RefreshCurrentPriorityNodeIds() {
			TrafficPriorityManager tpm = TrafficPriorityManager.Instance;

			currentPriorityNodeIds.Clear();
			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				if (!Constants.ServiceFactory.NetService.IsNodeValid((ushort)nodeId)) {
					continue;
				}

				if (!tpm.MayNodeHavePrioritySigns((ushort)nodeId)) {
					continue;
				}

				if (!tpm.HasNodePrioritySign((ushort)nodeId) && nodeId != SelectedNodeId) {
					continue;
				}

				/*if (! MainTool.IsNodeWithinViewDistance(nodeId)) {
					continue;
				}*/

				currentPriorityNodeIds.Add((ushort)nodeId);
			}
			//Log._Debug($"PrioritySignsTool.RefreshCurrentPriorityNodeIds: currentPriorityNodeIds={string.Join(", ", currentPriorityNodeIds.Select(x => x.ToString()).ToArray())}");
		}

		public override void ShowGUIOverlay(bool viewOnly) {
			if (viewOnly && !Options.prioritySignsOverlay)
				return;

			if (UIBase.GetTrafficManagerTool(false)?.GetToolMode() == ToolMode.JunctionRestrictions)
				return;

			ShowGUI(viewOnly);
		}

		public void ShowGUI(bool viewOnly) {
			try {
				TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
				TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;
				TrafficLightManager tlm = TrafficLightManager.Instance;

				Vector3 camPos = Constants.ServiceFactory.SimulationService.CameraPosition;

				bool clicked = !viewOnly ? MainTool.CheckClicked() : false;

				ushort removedNodeId = 0;
				bool showRemoveButton = false;
				foreach (ushort nodeId in currentPriorityNodeIds) {
					if (! Constants.ServiceFactory.NetService.IsNodeValid(nodeId)) {
						continue;
					}

					if (!MainTool.IsNodeWithinViewDistance(nodeId)) {
						continue;
					}

					NodeGeometry nodeGeo = NodeGeometry.Get(nodeId);

					Vector3 nodePos = default(Vector3);
					Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
						nodePos = node.m_position;
						return true;
					});

					foreach (SegmentEndGeometry endGeo in nodeGeo.SegmentEndGeometries) {
						if (endGeo == null) {
							continue;
						}
						if (endGeo.OutgoingOneWay) {
							continue;
						}
						ushort segmentId = endGeo.SegmentId;
						bool startNode = endGeo.StartNode;

						// calculate sign position
						Vector3 signPos = nodePos;

						Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort sId, ref NetSegment segment) {
							signPos += 10f * (startNode ? segment.m_startDirection : segment.m_endDirection);
							return true;
						});

						Vector3 signScreenPos;
						if (! MainTool.WorldToScreenPoint(signPos, out signScreenPos)) {
							continue;
						}

						// draw sign and handle input
						PriorityType sign = prioMan.GetPrioritySign(segmentId, startNode);
						if (viewOnly && sign == PriorityType.None) {
							continue;
						}
						if (!viewOnly && sign != PriorityType.None) {
							showRemoveButton = true;
						}

						if (MainTool.DrawGenericSquareOverlayTexture(TextureResources.PrioritySignTextures[sign], camPos, signPos, 90f, !viewOnly, 0.5f, 0.8f) && clicked) {
							PriorityType? newSign = null;
							switch (sign) {
								case PriorityType.Main:
									newSign = PriorityType.Yield;
									break;
								case PriorityType.Yield:
									newSign = PriorityType.Stop;
									break;
								case PriorityType.Stop:
									newSign = PriorityType.Main;
									break;
								case PriorityType.None:
								default:
									newSign = prioMan.CountPrioritySignsAtNode(nodeId, PriorityType.Main) >= 2 ? PriorityType.Yield : PriorityType.Main;
									break;
							}

							if (newSign != null) {
								SetPrioritySign(segmentId, startNode, (PriorityType)newSign);
							}
						} // draw sign
					} // foreach segment end

					if (viewOnly) {
						continue;
					}

					// draw remove button and handle click
					if (showRemoveButton && MainTool.DrawHoverableSquareOverlayTexture(TextureResources.SignRemoveTexture2D, camPos, nodePos, 90f, 0.5f, 0.8f) && clicked) {
						prioMan.RemovePrioritySignsFromNode(nodeId);
						Log._Debug($"PrioritySignsTool.ShowGUI: Removed priority signs from node {nodeId}");
						removedNodeId = nodeId;
					}
				} // foreach node

				if (removedNodeId != 0) {
					currentPriorityNodeIds.Remove(removedNodeId);
					SelectedNodeId = 0;
				}
			} catch (Exception e) {
				Log.Error(e.ToString());
			}
		}

		public bool SetPrioritySign(ushort segmentId, bool startNode, PriorityType sign) {
			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"PrioritySignsTool.SetPrioritySign: No geometry information available for segment {segmentId}");
				return false;
			}
			ushort nodeId = segGeo.GetNodeId(startNode);

			// check for restrictions
			if (!MayNodeHavePrioritySigns(nodeId)) {
				Log._Debug($"PrioritySignsTool.SetPrioritySign: MayNodeHavePrioritySigns({nodeId})=false");
				return false;
			}

			bool success = TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, sign);
			Log._Debug($"PrioritySignsTool.SetPrioritySign: SetPrioritySign({segmentId}, {startNode}, {sign})={success}");
			if (success && (sign == PriorityType.Stop || sign == PriorityType.Yield)) {
				// make all undefined segments a main road
				Log._Debug($"PrioritySignsTool.SetPrioritySign: flagging remaining segments at node {nodeId} as main road.");
				NodeGeometry nodeGeo = NodeGeometry.Get(nodeId);
				foreach (SegmentEndGeometry endGeo in nodeGeo.SegmentEndGeometries) {
					if (endGeo == null) {
						continue;
					}

					if (endGeo.SegmentId == segmentId) {
						continue;
					}

					if (TrafficPriorityManager.Instance.GetPrioritySign(endGeo.SegmentId, endGeo.StartNode) == PriorityType.None) {
						Log._Debug($"PrioritySignsTool.SetPrioritySign: setting main priority sign for segment {endGeo.SegmentId} @ {nodeId}");
						TrafficPriorityManager.Instance.SetPrioritySign(endGeo.SegmentId, endGeo.StartNode, PriorityType.Main);
					}
				}
			}
			return success;
		}

		public override void Cleanup() {
			//TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			//foreach (PrioritySegment trafficSegment in prioMan.PrioritySegments) {
			//	try {
			//		trafficSegment?.Instance1?.Reset();
			//		trafficSegment?.Instance2?.Reset();
			//	} catch (Exception e) {
			//		Log.Error($"Error occured while performing PrioritySignsTool.Cleanup: {e.ToString()}");
			//	}
			//}

			
		}

		public override void OnActivate() {
			RefreshCurrentPriorityNodeIds();
		}

		public override void Initialize() {
			Cleanup();
			if (Options.prioritySignsOverlay) {
				RefreshCurrentPriorityNodeIds();
			} else {
				currentPriorityNodeIds.Clear();
			}
		}

		private bool MayNodeHavePrioritySigns(ushort nodeId) {
			TrafficPriorityManager.UnableReason reason;
			//Log._Debug($"PrioritySignsTool.MayNodeHavePrioritySigns: Checking if node {nodeId} may have priority signs.");
			if (!TrafficPriorityManager.Instance.MayNodeHavePrioritySigns(nodeId, out reason)) {
				//Log._Debug($"PrioritySignsTool.MayNodeHavePrioritySigns: Node {nodeId} does not allow priority signs: {reason}");
				if (reason == TrafficPriorityManager.UnableReason.HasTimedLight) {
					MainTool.ShowTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"));
				}
				return false;
			}
			//Log._Debug($"PrioritySignsTool.MayNodeHavePrioritySigns: Node {nodeId} allows priority signs");
			return true;
		}
	}
}
