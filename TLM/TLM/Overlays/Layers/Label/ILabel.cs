namespace TrafficManager.Manager.Overlays.Layers {
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl.OverlayManagerData;
    using TrafficManager.Overlays.Layers;
    using UnityEngine;

    public interface ILabel {
        Overlays Overlay { get; }

        /// <summary>
        /// Determines where the label should appear, in world
        /// coordinate space.
        /// </summary>
        /// <param name="id">
        /// The <see cref="InstanceID"/> associated with the label.
        /// </param>
        /// <param name="state">
        /// The current overlay state (access to camera, mouse, etc).
        /// </param>
        /// <returns>Return the world position.</returns>
        /// <remarks>
        /// If you want to position a label in screen space,
        /// use <c>state.Camera.ScreenToWorldPoint()</c>.
        /// </remarks>
        Vector3 UpdateLabelWorldPos(ref InstanceID id, ref OverlayState state);

        /// <summary>
        /// Defines the content and visual appearance of the label.
        /// </summary>
        /// <param name="id">
        /// The <see cref="InstanceID"/> associated with the label.
        /// </param>
        /// <param name="mouseInside">
        /// Will be <c>true</c> if mouse is hovering the label.
        /// </param>
        /// <param name="state">
        /// The current overlay state (access to camera, mouse, etc).
        /// </param>
        /// <param name="style">
        /// Always set the following properties every time:
        /// <list type="bullet">
        /// <item>
        /// <term>style.fontSize</term>
        /// <description>
        /// Font size for label (will be scaled automatically
        /// based on distance from camera).
        /// </description>
        /// </item>
        /// <item>
        /// <term>style.normal.textColor</term>
        /// <description>
        /// Text color for label.
        /// </description>
        /// </item>
        /// </list>
        /// Most other style properties are ignored (in particlar
        /// the hovered, active, etc., states do not work).
        /// </param>
        /// <returns>
        /// Return text for the label.
        /// </returns>
        /// <remarks>
        /// Values are cached and the method won't be called again
        /// unless you force a full update of the label.
        /// <list type="bullet">
        /// <item>
        /// Always set the font size!
        /// </item>
        /// <item>
        /// Cache color(s) to avoid spammy instantiation
        /// (and gc) of <c>Color</c> objects.
        /// </item>
        /// <item>
        /// Ensure colors are legible in infoviews.
        /// </item>
        /// </list>
        /// </remarks>
        string UpdateLabelStyle(ref InstanceID id, bool mouseInside, ref OverlayState state, GUIStyle style);

        bool IsLabelInteractive(ref InstanceID id);

        /// <summary>
        /// Called when mouse enters or leaves a label.
        /// </summary>
        /// <param name="id">
        /// The <see cref="InstanceID"/> associated with the label.
        /// </param>
        /// <param name="mouseInside">
        /// Will be <c>true</c> if mouse is hovering the label.
        /// </param>
        /// <param name="state">
        /// The current overlay state (access to camera, mouse, etc).
        /// </param>
        /// <returns>
        /// <para>
        /// The return value updates the task state, for example
        /// you can return <see cref="TaskState.Hidden"/> to hide
        /// the label.
        /// </para>
        /// <para>
        /// If you don't want to alter task state, return <c>null</c>.
        /// </para>
        /// </returns>
        /// <remarks>
        /// Only triggered if the <see cref="IsLabelInteractive(ref InstanceID)"/>
        /// returns <c>true</c>.
        /// </remarks>
        TaskState? OnLabelHovered(ref InstanceID id, bool mouseInside, ref OverlayState state);

        /// <summary>
        /// Called when mouse clicks or unclicks a label. To determine
        /// which button, inspect the <paramref name="state"/> properties.
        /// </summary>
        /// <param name="id">
        /// The <see cref="InstanceID"/> associated with the label.
        /// </param>
        /// <param name="mouseInside">
        /// Will be <c>true</c> if mouse is hovering the label.
        /// </param>
        /// <param name="state">
        /// The current overlay state (access to camera, mouse, etc).
        /// </param>
        /// <returns>
        /// <para>
        /// The return value updates the task state, for example
        /// you can return <see cref="TaskState.Hidden"/> to hide
        /// the label.
        /// </para>
        /// <para>
        /// If you don't want to alter task state, return <c>null</c>.
        /// </para>
        /// </returns>
        /// <remarks>
        /// Only triggered if the <see cref="IsLabelInteractive(ref InstanceID)"/>
        /// returns <c>true</c>.
        /// </remarks>
        TaskState? OnLabelClicked(ref InstanceID id, bool mouseInside, ref OverlayState state);

        void OnLabelHidden(ref InstanceID id);

        void OnLabelDeleted(ref InstanceID id);
    }
}
