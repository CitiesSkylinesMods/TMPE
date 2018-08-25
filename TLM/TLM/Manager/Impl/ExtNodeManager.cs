using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager.Impl {
	public class ExtNodeManager : AbstractCustomManager, IExtNodeManager {
		public static ExtNodeManager Instance { get; private set; } = null;

		static ExtNodeManager() {
			Instance = new ExtNodeManager();
		}
		
		/// <summary>
		/// All additional data for nodes
		/// </summary>
		public ExtNode[] ExtNodes { get; private set; } = null;

		private ExtNodeManager() {
			ExtNodes = new ExtNode[NetManager.MAX_NODE_COUNT];
			for (uint i = 0; i < ExtNodes.Length; ++i) {
				ExtNodes[i] = new ExtNode((ushort)i);
			}
		}

		public bool IsValid(ushort nodeId) {
			return Services.NetService.IsNodeValid(nodeId);
		}

		public void AddSegment(ushort nodeId, ushort segmentId) {
			if (ExtNodes[nodeId].segmentIds.Add(segmentId) && ExtNodes[nodeId].removedSegmentEndId != null) {
				SegmentEndReplacement replacement = new SegmentEndReplacement();
				replacement.oldSegmentEndId = ExtNodes[nodeId].removedSegmentEndId;
				replacement.newSegmentEndId = new SegmentEndId(segmentId, (bool)Services.NetService.IsStartNode(segmentId, nodeId));
				ExtNodes[nodeId].removedSegmentEndId = null;

				Constants.ManagerFactory.GeometryManager.OnSegmentEndReplacement(replacement);
			}
		}

		public void RemoveSegment(ushort nodeId, ushort segmentId) {
			if (ExtNodes[nodeId].segmentIds.Remove(segmentId)) {
				ExtNodes[nodeId].removedSegmentEndId = new SegmentEndId(segmentId, (bool)Services.NetService.IsStartNode(segmentId, nodeId));
			}
		}

		public void Reset(ushort nodeId) {
			Reset(ref ExtNodes[nodeId]);
		}

		protected void Reset(ref ExtNode node) {
			node.Reset();
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Extended node data:");
			for (uint i = 0; i < ExtNodes.Length; ++i) {
				if (! IsValid((ushort)i)) {
					continue;
				}
				Log._Debug($"Node {i}: {ExtNodes[i]}");
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < ExtNodes.Length; ++i) {
				Reset(ref ExtNodes[i]);
			}
		}
	}
}
