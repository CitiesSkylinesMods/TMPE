
namespace TrafficManager.Util {
    using System;
    using ColossalFramework;
    using API.Traffic.Enums;
    using Manager.Impl;
    using TrafficManager.API.Traffic.Data;
    using static Util.SegmentTraverser;
    using static Util.Shortcuts;
    using CSUtil.Commons;
    using TrafficManager.API.Manager;
    using UI.SubTools;
    using System.Collections.Generic;

    //TODO: this is a work around #623
    public static class ClearUtil {
        static IJunctionRestrictionsManager JRMan = JunctionRestrictionsManager.Instance;

        public static void ClearPedestrianCrossingAllowed(ushort segmentId, bool startNode, ref NetNode node) {
            bool currentVal = JRMan.IsPedestrianCrossingAllowed(segmentId, startNode);
            bool defautVal = JRMan.GetDefaultPedestrianCrossingAllowed(segmentId, startNode, ref node);
            if(defautVal != currentVal) {
                JRMan.SetPedestrianCrossingAllowed(segmentId, startNode, defautVal);
            }
        }

        public static void ClearEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, ref NetNode node) {
            bool currentVal = JRMan.IsEnteringBlockedJunctionAllowed(segmentId, startNode);
            bool defautVal = JRMan.GetDefaultEnteringBlockedJunctionAllowed(segmentId, startNode, ref node);
            if (defautVal != currentVal) {
                JRMan.SetEnteringBlockedJunctionAllowed(segmentId, startNode, defautVal);
            }
        }

        public static void ClearLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, ref NetNode node) {
            bool currentVal = JRMan.IsLaneChangingAllowedWhenGoingStraight(segmentId, startNode);
            bool defautVal = JRMan.GetDefaultLaneChangingAllowedWhenGoingStraight(segmentId, startNode, ref node);
            if (defautVal != currentVal) {
                JRMan.SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, defautVal);
            }
        }


    }
}
