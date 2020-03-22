namespace TrafficManager.U.Autosize {
    public enum USizeRule {
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

        /// <summary>The control will be sized to % screen size what the value will take at 1920x1080.</summary>
        ReferenceSizeAt1080p,

        /// <summary>
        /// Size the control to max extent of its children. The float value in <see cref="USizePosition"/>
        /// defines the padding.
        /// </summary>
        FitChildren,
    }
}