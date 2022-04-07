namespace TrafficManager.API.Attributes {
    using System;
    using System.Diagnostics;

    /// <summary>
    /// This event/code is generally only invoked as a
    /// result of mouse/keyboard interaction.
    /// </summary>
    /// <remarks>
    /// This attribute is cosmetic and will be removed
    /// from non-DEBUG builds.
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Constructor |
        AttributeTargets.Field |
        AttributeTargets.Method |
        AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = true)]
    [Conditional("DEBUG")]
    public class Cold : Attribute {
        public Cold(string note) { }
        public Cold() { }
    }
}
