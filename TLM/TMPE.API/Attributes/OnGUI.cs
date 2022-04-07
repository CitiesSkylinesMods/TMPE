namespace TrafficManager.API.Attributes {
    using System;
    using System.Diagnostics;

    /// <summary>
    /// This code must only be used in OnGUI step.
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
    public class OnGUI : Attribute {
        public OnGUI(string note) { }
        public OnGUI() { }
    }
}
