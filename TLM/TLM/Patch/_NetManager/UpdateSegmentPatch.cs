using ColossalFramework.Math;
using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using UnityEngine;

namespace TrafficManager.Patch._NetManager {
	[HarmonyPatch(typeof(NetManager), "UpdateSegment", new[] { typeof(ushort), typeof(ushort), typeof(int) })]
	public static class UpdateSegmentPatch {
		/// <summary>
		/// Initiates a segment geometry recalculation when a segment is updated.
		/// </summary>
		[HarmonyPostfix]
		public static void Postfix(NetManager __instance, ushort segment, ushort fromNode, int level) {
			SegmentGeometry.Get(segment, true).StartRecalculation(GeometryCalculationMode.Propagate);
		}
	}
}
