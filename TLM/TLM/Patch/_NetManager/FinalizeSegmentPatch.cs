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
	[HarmonyPatch(typeof(NetManager), "FinalizeSegment")]
	public static class FinalizeSegmentPatch {
		/// <summary>
		/// Initiates a segment geometry recalculation when a segment is finalized.
		/// </summary>
		[HarmonyPostfix]
		public static void Postfix(NetManager __instance, ushort segment, ref NetSegment data) {
			SegmentGeometry.Get(segment, true).StartRecalculation(GeometryCalculationMode.Propagate);
		}
	}
}
