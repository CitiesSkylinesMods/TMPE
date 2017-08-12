using ColossalFramework;
using CSUtil.Commons;
using CSUtil.Commons.Benchmark;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Custom.Manager {
	public class CustomNetManager : NetManager {
		public void CustomFinalizeSegment(ushort segment, ref NetSegment data) {
			Vector3 vector = (this.m_nodes.m_buffer[(int)data.m_startNode].m_position + this.m_nodes.m_buffer[(int)data.m_endNode].m_position) * 0.5f;
			int num = Mathf.Clamp((int)(vector.x / 64f + 135f), 0, 269);
			int num2 = Mathf.Clamp((int)(vector.z / 64f + 135f), 0, 269);
			int num3 = num2 * 270 + num;
			ushort num4 = 0;
			ushort num5 = this.m_segmentGrid[num3];
			int num6 = 0;
			while (num5 != 0) {
				if (num5 == segment) {
					if (num4 == 0) {
						this.m_segmentGrid[num3] = data.m_nextGridSegment;
					} else {
						this.m_segments.m_buffer[(int)num4].m_nextGridSegment = data.m_nextGridSegment;
					}
					break;
				}
				num4 = num5;
				num5 = this.m_segments.m_buffer[(int)num5].m_nextGridSegment;
				if (++num6 > 65536) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			data.m_nextGridSegment = 0;

			// NON-STOCK CODE START
#if BENCHMARK
			using (var bm = new Benchmark(null, "StartRecalculation")) {
#endif
				try {
#if DEBUGGEO
					if (GlobalConfig.Instance.Debug.Switches[5])
						Log.Warning($"CustomNetManager: CustomFinalizeSegment {segment}");
#endif
					SegmentGeometry.Get(segment, true).StartRecalculation(GeometryCalculationMode.Propagate);
				} catch (Exception e) {
					Log.Error($"Error occured in CustomNetManager.CustomFinalizeSegment @ seg. {segment}: " + e.ToString());
				}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END
		}

		public void CustomUpdateSegment(ushort segment, ushort fromNode, int level) {
			this.m_updatedSegments[segment >> 6] |= 1uL << (int)segment;
			this.m_segmentsUpdated = true;
			if (level <= 0) {
				ushort startNode = this.m_segments.m_buffer[(int)segment].m_startNode;
				ushort endNode = this.m_segments.m_buffer[(int)segment].m_endNode;
				if (startNode != 0 && startNode != fromNode) {
					this.UpdateNode(startNode, segment, level + 1);
				}
				if (endNode != 0 && endNode != fromNode) {
					this.UpdateNode(endNode, segment, level + 1);
				}
			}

			// NON-STOCK CODE START
#if BENCHMARK
			using (var bm = new Benchmark(null, "StartRecalculation")) {
#endif
				try {
#if DEBUG
					if (GlobalConfig.Instance.Debug.Switches[5])
						Log.Warning($"CustomNetManager: CustomUpdateSegment {segment}");
#endif
					SegmentGeometry.Get(segment, true).StartRecalculation(GeometryCalculationMode.Propagate);
				} catch (Exception e) {
					Log.Error($"Error occured in CustomNetManager.CustomUpdateSegment @ seg. {segment}: " + e.ToString());
				}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END
		}

		// TODO remove
#if DEBUG
		private void CustomMoveNode(ushort node, ref NetNode data, Vector3 position) {
			var connClass = data.Info.GetConnectionClass();
			if (connClass == null || connClass.m_service == ItemClass.Service.PublicTransport) {
				Log.Warning($"CustomNetManager.CustomMoveNode({node}, ..., {position}): old position: {data.m_position} -- flags: {data.m_flags}, problems: {data.m_problems}, transport line: {data.m_transportLine}");
			}

			for (int i = 0; i < 8; i++) {
				ushort segment = data.GetSegment(i);
				if (segment != 0) {
					CustomFinalizeSegment(segment, ref this.m_segments.m_buffer[(int)segment]);
				}
			}
			this.FinalizeNode(node, ref data);
			data.m_position = position;
			this.InitializeNode(node, ref data);
			for (int j = 0; j < 8; j++) {
				ushort segment2 = data.GetSegment(j);
				if (segment2 != 0) {
					InitializeSegment(segment2, ref this.m_segments.m_buffer[(int)segment2]);
				}
			}
			this.UpdateNode(node);
		}

		// TODO remove
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void FinalizeNode(ushort node, ref NetNode data) {
			Log.Error($"CustomNetManager.FinalizeNode called");
		}

		// TODO remove
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void InitializeNode(ushort node, ref NetNode data) {
			Log.Error($"CustomNetManager.InitializeNode called");
		}

		// TODO remove
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void InitializeSegment(ushort segmentId, ref NetSegment data) {
			Log.Error($"CustomNetManager.InitializeNode called");
		}
#endif
	}
}
