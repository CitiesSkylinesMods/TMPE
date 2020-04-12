namespace TrafficManager.U.Autosize {
    public enum URule {
        /// <summary>Do not produce any value.</summary>
        Ignore,

        /// <summary>
        /// The control will be always sized to the fixed size.
        /// The UI has internal fixed size of 1920x1080 points which is stretched to screen.
        /// </summary>
        FixedSize,

        /// <summary>The control will be sized to % of screen width.</summary>
        FractionScreenWidth,

        /// <summary>The control will be sized to % of control's own width.</summary>
        MultipleOfWidth,

        /// <summary>The control will be sized to % of screen height.</summary>
        FractionScreenHeight,

        /// <summary>The control will be sized to % of control's own height.</summary>
        MultipleOfHeight,

        /// <summary>
        /// Size the control to max width of its children. The float value is ignored.
        /// Padding can be set in the <see cref="UResizerConfig"/>
        /// </summary>
        FitChildrenWidth,

        /// <summary>
        /// Size the control to max height of its children. The float value is ignored.
        /// Padding can be set in the <see cref="UResizerConfig"/>
        /// </summary>
        FitChildrenHeight,
    }
}