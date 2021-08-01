namespace TrafficManager.U.Autosize {
    /// <summary>
    /// Defines how <see cref="UResizer.Stack"/> will operate.
    /// </summary>
    public enum UStackMode {
        None,

        /// <summary>
        /// The stacked control will be placed to the bottom left corner of the
        /// reference control or the previous sibling.
        /// </summary>
        Below,

        /// <summary>
        /// The stacked control will be placed to the left side of the parent, below the
        /// reference control or the previous sibling.
        /// </summary>
        NewRowBelow,

        /// <summary>
        /// The stacked control will be placed above the reference control or the previous sibling.
        /// </summary>
        Above,

        /// <summary>
        /// The stacked control will be placed at the right of the reference control or the previous sibling.
        /// </summary>
        ToTheRight,

        /// <summary>
        /// The stacked control will be placed at the left of the reference control or the previous sibling.
        /// </summary>
        ToTheLeft,
    }
}