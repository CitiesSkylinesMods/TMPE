namespace TrafficManager.U.Autosize {
    public enum USizeRule {
        /// <summary>Do not produce any value.</summary>
        Ignore,

        /// <summary>The control will be always sized to the fixed size.</summary>
        FixedSize,

        /// <summary>The control will be sized to % of screen width.</summary>
        FractionScreenWidth,

        /// <summary>The control will be sized to % of control's own width.</summary>
        MultipleOfWidth,

        /// <summary>The control will be sized to % of screen height.</summary>
        FractionScreenHeight,

        /// <summary>The control will be sized to % of control's own height.</summary>
        MultipleOfHeight,

        /// <summary>The control will be sized to % screen width what the value will take at 1920x1080.</summary>
        ReferenceWidthAt1080P,

        /// <summary>The control will be sized to % screen height what the value will take at 1920x1080.</summary>
        ReferenceHeightAt1080P,

        /// <summary>
        /// Size the control to max width of its children. The float value in <see cref="USizePosition"/>
        /// defines the padding.
        /// </summary>
        FitChildrenWidth,

        /// <summary>
        /// Size the control to max height of its children. The float value in <see cref="USizePosition"/>
        /// defines the padding.
        /// </summary>
        FitChildrenHeight,
    }
}