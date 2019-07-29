namespace TrafficManager.API.Traffic.Data {
    using Enums;
    using JetBrains.Annotations;

    /// <summary>
    /// A priority segment specifies the priority signs that are present at each end of a certain segment.
    /// </summary>
    public struct PrioritySegment {
        /// <summary>
        /// Priority sign at start node (default: None)
        /// </summary>
        public PriorityType startType;

        /// <summary>
        /// Priority sign at end node (default: None)
        /// </summary>
        public PriorityType endType;

        public override string ToString() {
            return string.Format(
                "[PrioritySegment\n\tstartType = {0}\n\tendType = {1}\nPrioritySegment]",
                startType,
                endType);
        }

        [UsedImplicitly]
        public PrioritySegment(PriorityType startType, PriorityType endType) {
            this.startType = startType;
            this.endType = endType;
        }

        public void Reset() {
            startType = PriorityType.None;
            endType = PriorityType.None;
        }

        public bool IsDefault() {
            return !HasPrioritySignAtNode(true) && !HasPrioritySignAtNode(false);
        }

        public bool HasPrioritySignAtNode(bool startNode) {
            return startNode
                       ? startType != PriorityType.None
                       : endType != PriorityType.None;
        }
    }
}