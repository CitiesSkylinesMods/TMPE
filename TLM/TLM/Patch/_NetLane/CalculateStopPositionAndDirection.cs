namespace TrafficManager.Patch._NetLane; 
using ColossalFramework.Math;
using HarmonyLib;
using System;
using TrafficManager.Util;
using UnityEngine;

[HarmonyPatch(typeof(NetLane), nameof(NetLane.CalculateStopPositionAndDirection))]
internal static class CalculateStopPositionAndDirection {
    internal static bool Prefix(ref NetLane __instance, float laneOffset, float stopOffset, out Vector3 position, out Vector3 direction) {
        try {
            ref Bezier3 bezier = ref __instance.m_bezier;
            position = bezier.Position(laneOffset);
            direction = bezier.Tangent(laneOffset);
            if (stopOffset != 0) {
                Vector3 sideDir = Vector3.Cross(Vector3.up, direction).normalized;

                const float t1 = 0.12f; // bus starts turning from this offset to avoid going over the pavement.
                const float t2_0 = 0.20f; // bus does not finish turning until this offset to avoid going over the pavement
                float t2 = t1 + 0.7f / (__instance.m_length + 1); // bus finishes turning at this offset for a smooth turn (if segment has space).
                t2 = Mathf.Clamp(t2, t2_0, 0.5f);

                float t = laneOffset <= 0.5f ? laneOffset : 1 - laneOffset;
                float d;
                if (t < t1) {
                    d = 0;
                } else if (t > t2) {
                    d = 1;
                } else {
                    // t=[t1:t2] => d=[0:1]
                    d = (t - t1) / (t2 - t1);
                }

                position += sideDir * stopOffset * d;
            }
            return false;
        } catch (Exception ex) {
            ex.LogException(true);
            throw;
        }
    }
}
