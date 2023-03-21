namespace TrafficManager.UI.Helpers;

using ColossalFramework.UI;

/// <summary>
/// Custom version of vanilla dropdown with customizable mouse wheel behavior on hover closed dropdown control
/// </summary>
public class CustomDownDown : UIDropDown {

    /// <summary>
    /// Use mouse wheel when dropdown is closed to change selection
    /// Unlike in vanilla, default is False
    /// </summary>
    public bool MouseWheelSelectItem { get; set; } = false;

    protected override void OnMouseWheel(UIMouseEventParameter p) {
        if (MouseWheelSelectItem) {
            // do default action
            base.OnMouseWheel(p);
        } else {
            // mark as used to prevent scrolling page or selecting different items in the dropdown!
            p.Use();
        }
    }
}