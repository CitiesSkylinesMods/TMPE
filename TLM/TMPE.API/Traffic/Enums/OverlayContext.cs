namespace TrafficManager.API.Traffic.Enums {

    /// <summary>
    /// The context indicates what caused the overlay to be displayed.
    /// <list type="bullet">
    /// <item>
    /// <term>None</term> <description>No overlay (inactive mode)</description>
    /// </item>
    /// <item>
    /// <term>Info</term> <description>Autoamtic based on info view</description>
    /// </item>
    /// <item>
    /// <term>Tool</term> <description>Automeric based on tool controller</description>
    /// </item>
    /// <item>
    /// <term>Custom</term>
    /// <description>
    /// Either TMPE (toolbar, subtool, etc) or an external mod.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Overlay precedence is: Custom > Tool > Info > None.
    /// </para>
    /// <para>
    /// Automatic overlays will only start/stop if there is no custom overlay active.
    /// </para>
    /// <para>
    /// Custom overlays remain active until told otherwise via <see cref="OverlayManager"/>
    /// methods such as <c>TurnOn()</c> and <c>TurnOff()</c>.
    /// </para>
    /// </remarks>
    public enum OverlayContext : byte {
        None = 0,
        Info = 1 << 0, // InfoManager.instance.CurrentMode
        Tool = 1 << 1, // ToolsModifierControl.toolController?.CurrentTool
        Custom = 1 << 2, // OverlayManager.Instance: TurnOn(), TurnOff()
    }

}
