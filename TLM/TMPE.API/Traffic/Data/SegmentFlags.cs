namespace TrafficManager.API.Traffic.Data {
    using System;
    using CSUtil.Commons;

    /// <summary>
    /// Segment flags hold both segment end flags
    /// </summary>
    public struct SegmentFlags {
        public SegmentEndFlags startNodeFlags;
        public SegmentEndFlags endNodeFlags;

        public bool IsUturnAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.IsUturnAllowed()
                       : endNodeFlags.IsUturnAllowed();
        }

        public bool IsNearTurnOnRedAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.IsNearTurnOnRedAllowed()
                       : endNodeFlags.IsNearTurnOnRedAllowed();
        }

        public bool IsFarTurnOnRedAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.IsFarTurnOnRedAllowed()
                       : endNodeFlags.IsFarTurnOnRedAllowed();
        }

        public bool IsLaneChangingAllowedWhenGoingStraight(bool startNode) {
            return startNode
                       ? startNodeFlags.IsLaneChangingAllowedWhenGoingStraight()
                       : endNodeFlags.IsLaneChangingAllowedWhenGoingStraight();
        }

        public bool IsEnteringBlockedJunctionAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.IsEnteringBlockedJunctionAllowed()
                       : endNodeFlags.IsEnteringBlockedJunctionAllowed();
        }

        public bool IsPedestrianCrossingAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.IsPedestrianCrossingAllowed()
                       : endNodeFlags.IsPedestrianCrossingAllowed();
        }

        public TernaryBool GetUturnAllowed(bool startNode) {
            return startNode ? startNodeFlags.uturnAllowed : endNodeFlags.uturnAllowed;
        }

        public TernaryBool GetNearTurnOnRedAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.nearTurnOnRedAllowed
                       : endNodeFlags.nearTurnOnRedAllowed;
        }

        public TernaryBool GetFarTurnOnRedAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.farTurnOnRedAllowed
                       : endNodeFlags.farTurnOnRedAllowed;
        }

        public TernaryBool GetLaneChangingAllowedWhenGoingStraight(bool startNode) {
            return startNode
                       ? startNodeFlags.straightLaneChangingAllowed
                       : endNodeFlags.straightLaneChangingAllowed;
        }

        public TernaryBool GetEnteringBlockedJunctionAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.enterWhenBlockedAllowed
                       : endNodeFlags.enterWhenBlockedAllowed;
        }

        public TernaryBool GetPedestrianCrossingAllowed(bool startNode) {
            return startNode
                       ? startNodeFlags.pedestrianCrossingAllowed
                       : endNodeFlags.pedestrianCrossingAllowed;
        }

        public void SetUturnAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeFlags.SetUturnAllowed(value);
            } else {
                endNodeFlags.SetUturnAllowed(value);
            }
        }

        public void SetNearTurnOnRedAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeFlags.SetNearTurnOnRedAllowed(value);
            } else {
                endNodeFlags.SetNearTurnOnRedAllowed(value);
            }
        }

        public void SetFarTurnOnRedAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeFlags.SetFarTurnOnRedAllowed(value);
            } else {
                endNodeFlags.SetFarTurnOnRedAllowed(value);
            }
        }

        public void SetLaneChangingAllowedWhenGoingStraight(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeFlags.SetLaneChangingAllowedWhenGoingStraight(value);
            } else {
                endNodeFlags.SetLaneChangingAllowedWhenGoingStraight(value);
            }
        }

        public void SetEnteringBlockedJunctionAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeFlags.SetEnteringBlockedJunctionAllowed(value);
            } else {
                endNodeFlags.SetEnteringBlockedJunctionAllowed(value);
            }
        }

        public void SetPedestrianCrossingAllowed(bool startNode, TernaryBool value) {
            if (startNode) {
                startNodeFlags.SetPedestrianCrossingAllowed(value);
            } else {
                endNodeFlags.SetPedestrianCrossingAllowed(value);
            }
        }

        public bool IsDefault() {
            return startNodeFlags.IsDefault() && endNodeFlags.IsDefault();
        }

        public void Reset(bool? startNode = null, bool resetDefaults = true) {
            if (startNode == null || (bool)startNode) {
                startNodeFlags.Reset(resetDefaults);
            }

            if (startNode == null || !(bool)startNode) {
                endNodeFlags.Reset(resetDefaults);
            }
        }

        public override string ToString() {
            return string.Format(
                "[SegmentFlags\n\tstartNodeFlags = {0}\n\tendNodeFlags = {1}\nSegmentFlags]",
                startNodeFlags,
                endNodeFlags);
        }
    }
}