namespace TrafficManager.U.Autosize {
    using UnityEngine;

    /// <summary>
    /// Defines UI control interface for a control which owns a <see cref="USizePosition"/>.
    /// </summary>
    public interface USizePositionInterface {
        USizePosition SizePosition { get; }
    }
}