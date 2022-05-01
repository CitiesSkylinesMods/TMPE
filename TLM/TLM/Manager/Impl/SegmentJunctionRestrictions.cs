namespace TrafficManager.Manager.Impl {
    using System;
    using CSUtil.Commons;

    public struct SegmentJunctionRestrictions {
        public JunctionRestrictions startNodeRestrictions;
        public JunctionRestrictions endNodeRestrictions;

        public bool IsUturnAllowed(bool startNode) {
            return startNode
                       ? startNodeRestrictions.IsUturnAllowed()
                       : endNodeRestrictions.IsUturnAllowed();
        }

        public bool IsNearTurnOnRedAllowed(bool startNode) {
            return startNode
                       ? startNodeRestrictions.IsNearTurnOnRedAllowed()
                       : endNodeRestrictions.IsNearTurnOnRedAllowed();
        }

        public bool IsFarTurnOnRedAllowed(bool startNode) {
            return startNode
                       ? startNodeRestrictions.IsFarTurnOnRedAllowed()
                       : endNodeRestrictions.IsFarTurnOnRedAllowed();
        }

        public bool IsLaneChangingAllowedWhenGoingStraight(bool startNode) {
            return startNode
                       ? startNodeRestrictions.IsLaneChangingAllowedWhenGoingStraight()
                       : endNodeRestrictions.IsLaneChangingAllowedWhenGoingStraight();
        }

        public bool IsEnteringBlockedJunctionAllowed(bool startNode) {
            return startNode
                       ? startNodeRestrictions.IsEnteringBlockedJunctionAllowed()
                       : endNodeRestrictions.IsEnteringBlockedJunctionAllowed();
        }

        public bool IsPedestrianCrossingAllowed(bool startNode) {
            return startNode
                       ? startNodeRestrictions.IsPedestrianCrossingAllowed()
                       : endNodeRestrictions.IsPedestrianCrossingAllowed();
        }

        public TernaryBool GetUturnAllowed(bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(JunctionRestrictionFlags.AllowUTurn);
        }

        public TernaryBool GetNearTurnOnRedAllowed(bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(JunctionRestrictionFlags.AllowNearTurnOnRed);
        }

        public TernaryBool GetFarTurnOnRedAllowed(bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(JunctionRestrictionFlags.AllowFarTurnOnRed);
        }

        public TernaryBool GetLaneChangingAllowedWhenGoingStraight(bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(JunctionRestrictionFlags.AllowForwardLaneChange);
        }

        public TernaryBool GetEnteringBlockedJunctionAllowed(bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(JunctionRestrictionFlags.AllowEnterWhenBlocked);
        }

        public TernaryBool GetPedestrianCrossingAllowed(bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(JunctionRestrictionFlags.AllowPedestrianCrossing);
        }

        public void SetUturnAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeRestrictions.SetUturnAllowed(value);
            } else {
                endNodeRestrictions.SetUturnAllowed(value);
            }
        }

        public void SetNearTurnOnRedAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeRestrictions.SetNearTurnOnRedAllowed(value);
            } else {
                endNodeRestrictions.SetNearTurnOnRedAllowed(value);
            }
        }

        public void SetFarTurnOnRedAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeRestrictions.SetFarTurnOnRedAllowed(value);
            } else {
                endNodeRestrictions.SetFarTurnOnRedAllowed(value);
            }
        }

        public void SetLaneChangingAllowedWhenGoingStraight(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeRestrictions.SetLaneChangingAllowedWhenGoingStraight(value);
            } else {
                endNodeRestrictions.SetLaneChangingAllowedWhenGoingStraight(value);
            }
        }

        public void SetEnteringBlockedJunctionAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeRestrictions.SetEnteringBlockedJunctionAllowed(value);
            } else {
                endNodeRestrictions.SetEnteringBlockedJunctionAllowed(value);
            }
        }

        public void SetPedestrianCrossingAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeRestrictions.SetPedestrianCrossingAllowed(value);
            } else {
                endNodeRestrictions.SetPedestrianCrossingAllowed(value);
            }
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
