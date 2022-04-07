namespace TrafficManager.Overlays.Layers {
    using System;

    [Flags]
    public enum TaskState : byte {
        /// <summary>
        /// Do not return this from <c>OnHover</c>
        /// or <c>OnClick</c> as it will result in
        /// task deletion. Return <c>null</c> instead.
        /// </summary>
        None = 0,

        /// <summary>
        /// The task is deleted.
        /// </summary>
        Deleted = 0,

        /// <summary>
        /// Task is hidden.
        /// Will not be rendered.
        /// </summary>
        /// <remarks>
        /// While <see cref="EveryFrame"/> is active
        /// <see cref="Hidden"/> will be reset each frame.
        /// </remarks>
        Hidden = 1 << 1,

        /// <summary>
        /// Update every frame. Specify what should
        /// hapen with either PositionChange or
        /// OverlayChange.
        /// </summary>
        EveryFrame = 1 << 2,

        /// <summary>
        /// Position changed (eg. due to camera move).
        /// Partial update required.
        /// </summary>
        NeedsReposition = 1 << 3,

        /// <summary>
        /// Overlay instance data changed.
        /// A full update is required.
        /// </summary>
        NeedsUpdate = 1 << 4,

        /// <summary>
        /// Mouse hovering task.
        /// </summary>
        Hovered = 1 << 5,

        /// <summary>
        /// Mouse clicked task.
        /// </summary>
        Clicked = 1 << 6,

        // internal use only
        RepositionEveryFrame = NeedsReposition | EveryFrame,
        UpdateEveryFrame = NeedsUpdate | EveryFrame,
        HoveredOrClicked = Hovered | Clicked,
    }
}
