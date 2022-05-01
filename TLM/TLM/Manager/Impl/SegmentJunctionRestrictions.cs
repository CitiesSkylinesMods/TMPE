namespace TrafficManager.Manager.Impl {
    using System;
    using CSUtil.Commons;

    public struct SegmentJunctionRestrictions {
        public JunctionRestrictions startNodeRestrictions;
        public JunctionRestrictions endNodeRestrictions;

        public bool GetValueOrDefault(JunctionRestrictionFlags flags, bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetValueOrDefault(flags);
        }

        public TernaryBool GetTernaryBool(JunctionRestrictionFlags flags, bool startNode) {
            return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(flags);
        }

        public void SetValue(JunctionRestrictionFlags flags, bool startNode, TernaryBool value) {
            if (startNode)
                startNodeRestrictions.SetValue(flags, value);
            else
                endNodeRestrictions.SetValue(flags, value);
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
