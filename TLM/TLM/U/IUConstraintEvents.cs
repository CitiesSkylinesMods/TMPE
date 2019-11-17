namespace TrafficManager.U {
    /// <summary>
    /// Implements event processing for constraint move/resize events
    /// </summary>
    public interface IUConstraintEvents {
        void OnUConstraintMove();
        void OnUConstraintResize();
    }
}