namespace TrafficManager.U.Autosize {
    /// <summary>
    /// A value which can be calculated based on control, parent control, and other global data.
    /// Used for smart control sizes and positioning.
    /// </summary>
    public struct UValue {
        public URule Rule;
        public float Value;

        public UValue(URule r, float v = 0f) {
            Rule = r;
            Value = v;
        }

        /// <summary>
        /// The control will be always sized to the fixed size.
        /// The UI has internal fixed size of 1920x1080 points which is stretched to screen.
        /// </summary>
        public static UValue FixedSize(float f) {
            return new(URule.FixedSize, f);
        }

        /// <summary>Returns multiple of current control width times f.</summary>
        /// <param name="f">The multiplier.</param>
        /// <returns>Value.</returns>
        public static UValue MultipleOfWidth(float f) {
            return new(URule.MultipleOfWidth, f);
        }

        /// <summary>Returns multiple of current control height times f.</summary>
        /// <param name="f">The multiplier.</param>
        /// <returns>Value.</returns>
        public static UValue MultipleOfHeight(float f) {
            return new(URule.MultipleOfHeight, f);
        }

        /// <summary>Adjusts width to fit all children.</summary>
        /// <param name="padding">Padding.</param>
        /// <returns>New UValue.</returns>
        public static UValue FitChildrenWidth(float padding) {
            return new(URule.FitChildrenWidth, padding);
        }

        /// <summary>Adjusts height to fit all children.</summary>
        /// <param name="padding">Padding.</param>
        /// <returns>New UValue.</returns>
        public static UValue FitChildrenHeight(float padding) {
            return new(URule.FitChildrenHeight, padding);
        }
    }
}