namespace TrafficManager.API.Attributes {
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Slow code in this path may result in lag spikes
    /// due to batch iterations.
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
    public class Spike : Attribute {
        public Spike(string note) { }
        public Spike() { }
    }
}
