namespace TrafficManager.API.Attributes {
    using System;
    using System.Diagnostics;

    /// <summary>
    /// This event/code is on the hotpath and is likely
    /// to be invoked every frame. Performance critical!
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
    public class Hot : Attribute {
        public Hot(string note) { }
        public Hot() { }
    }
}
