namespace TrafficManager.Util {
    using Manager.Impl;
    using TrafficManager.API.Manager;

    // TODO: this is a hack around #623. Thefore this does not exactly clears traffic rules
    // (which is the intention here) but merely sets them to whatever the default value is. 
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