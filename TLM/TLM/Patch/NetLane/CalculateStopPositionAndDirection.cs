namespace TrafficManager.Patch._NetLane {
    using ColossalFramework.Math;
    using HarmonyLib;
    using System;
    using TrafficManager.Util;
    using UnityEngine;

    [HarmonyPatch(typeof(NetLane), nameof(NetLane.CalculateStopPositionAndDirection))]
    public static class CalculateStopPositionAndDirection {
        public static bool Prefix(ref NetLane __instance, float laneOffset, float stopOffset, out Vector3 position, out Vector3 direction) {
            try {
                ref Bezier3 bezier = ref __instance.m_bezier;
                position = bezier.Position(laneOffset);
                direction = bezier.Tangent(laneOffset);
                Vector3 sideDir = Vector3.Cross(Vector3.up, direction).normalized;

                const float t1 = .12f;
                const float t2_0 = 0.20f;
                float t2 = t1 + 0.7f / (__instance.m_length + 1);
                t2 = Mathf.Clamp(t2, t2_0, 0.5f);

                float t = laneOffset <= 0.5f ? laneOffset : 1 - laneOffset;
                float d;
                if (t < t1) {
                    d = 0;
                } else if (t > t2) {
                    d = 1;
                } else {
                    d = (t - t1) / (t2 - t1); // from 0 to 1
                }

                position += sideDir * stopOffset * d;
                return false;
            } catch(Exception ex) {
                ex.LogException(true);
                throw;
            }
        }
    }
}
