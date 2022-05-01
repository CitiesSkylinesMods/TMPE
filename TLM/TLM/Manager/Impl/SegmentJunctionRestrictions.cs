namespace TrafficManager.Manager.Impl {
    using System;
    using CSUtil.Commons;

    public struct SegmentJunctionRestrictions {
        public JunctionRestrictions startNodeRestrictions;
        public JunctionRestrictions endNodeRestrictions;

        public bool GetValueOrDefault(JunctionRestrictionFlags flags, bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetValueOrDefault(flags);
        }

        public bool IsUturnAllowed(bool startNode) {
            return GetValueOrDefault(JunctionRestrictionFlags.AllowUTurn, startNode);
        }

        public bool IsNearTurnOnRedAllowed(bool startNode) {
            return GetValueOrDefault(JunctionRestrictionFlags.AllowNearTurnOnRed, startNode);
        }

        public bool IsFarTurnOnRedAllowed(bool startNode) {
            return GetValueOrDefault(JunctionRestrictionFlags.AllowFarTurnOnRed, startNode);
        }

        public bool IsLaneChangingAllowedWhenGoingStraight(bool startNode) {
            return GetValueOrDefault(JunctionRestrictionFlags.AllowForwardLaneChange, startNode);
        }

        public bool IsEnteringBlockedJunctionAllowed(bool startNode) {
            return GetValueOrDefault(JunctionRestrictionFlags.AllowEnterWhenBlocked, startNode);
        }

        public bool IsPedestrianCrossingAllowed(bool startNode) {
            return GetValueOrDefault(JunctionRestrictionFlags.AllowPedestrianCrossing, startNode);
        }

        public TernaryBool GetTernaryBool(JunctionRestrictionFlags flags, bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(flags);
        }

        public TernaryBool GetUturnAllowed(bool startNode) {
            return GetTernaryBool(JunctionRestrictionFlags.AllowUTurn, startNode);
        }

        public TernaryBool GetNearTurnOnRedAllowed(bool startNode) {
            return GetTernaryBool(JunctionRestrictionFlags.AllowNearTurnOnRed, startNode);
        }

        public TernaryBool GetFarTurnOnRedAllowed(bool startNode) {
            return GetTernaryBool(JunctionRestrictionFlags.AllowFarTurnOnRed, startNode);
        }

        public TernaryBool GetLaneChangingAllowedWhenGoingStraight(bool startNode) {
            return GetTernaryBool(JunctionRestrictionFlags.AllowForwardLaneChange, startNode);
        }

        public TernaryBool GetEnteringBlockedJunctionAllowed(bool startNode) {
            return GetTernaryBool(JunctionRestrictionFlags.AllowEnterWhenBlocked, startNode);
        }

        public TernaryBool GetPedestrianCrossingAllowed(bool startNode) {
            return GetTernaryBool(JunctionRestrictionFlags.AllowPedestrianCrossing, startNode);
        }

        public void SetValue(JunctionRestrictionFlags flags, bool startNode, TernaryBool value) {
            if (startNode)
                startNodeRestrictions.SetValue(flags, value);
            else
                endNodeRestrictions.SetValue(flags, value);
        }

        public void SetUturnAllowed(bool startNode, TernaryBool value) {
            SetValue(JunctionRestrictionFlags.AllowUTurn, startNode, value);
        }

        public void SetNearTurnOnRedAllowed(bool startNode, TernaryBool value) {
            SetValue(JunctionRestrictionFlags.AllowNearTurnOnRed, startNode, value);
        }

        public void SetFarTurnOnRedAllowed(bool startNode, TernaryBool value) {
            SetValue(JunctionRestrictionFlags.AllowFarTurnOnRed, startNode, value);
        }

        public void SetLaneChangingAllowedWhenGoingStraight(bool startNode, TernaryBool value) {
            SetValue(JunctionRestrictionFlags.AllowForwardLaneChange, startNode, value);
        }

        public void SetEnteringBlockedJunctionAllowed(bool startNode, TernaryBool value) {
            SetValue(JunctionRestrictionFlags.AllowEnterWhenBlocked, startNode, value);
        }

        public void SetPedestrianCrossingAllowed(bool startNode, TernaryBool value) {
            SetValue(JunctionRestrictionFlags.AllowPedestrianCrossing, startNode, value);
        }

        public bool IsDefault() {
            return startNodeRestrictions.IsDefault() && endNodeRestrictions.IsDefault();
        }

        public void Reset(bool? startNode = null, bool resetDefaults = true) {
            if (startNode == null || (bool)startNode) {
                startNodeRestrictions.Reset(resetDefaults);
            }

            if (startNode == null || !(bool)startNode) {
                endNodeRestrictions.Reset(resetDefaults);
            }
        }

        public override string ToString() {
            return "[SegmentJunctionRestrictions\n" +
                    $"\tstartNodeRestrictions = {startNodeRestrictions}\n" +
                    $"\tendNodeRestrictions = {endNodeRestrictions}\n" +
                    "SegmentJunctionRestrictions]";
        }
    }
}
