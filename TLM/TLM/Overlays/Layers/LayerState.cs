namespace TrafficManager.Overlays.Layers {
    public enum LayerState : byte {
        None = 0,

        NeedsReposition = 1 << 0,
        NeedsUpdate = 1 << 1,
        NeedsRender = 1 << 2,

        AllNeeds = NeedsReposition | NeedsUpdate | NeedsRender,
    }
}
