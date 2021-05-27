namespace TrafficManager.UI {
    /// <summary>
    /// SubTool represents TM:PE operation modes, grouped under a single TrafficManagerTool
    /// (which is a CO.DefaultTool).
    /// </summary>
    public abstract class TrafficManagerSubTool {
        public TrafficManagerSubTool(TrafficManagerTool mainTool) {
            MainTool = mainTool;
        }

        protected TrafficManagerTool MainTool { get; }

        /// <summary>Current hovered node. Overridden by some tools.</summary>
        protected virtual ushort HoveredNodeId {
            get => TrafficManagerTool.HoveredNodeId;
            set => TrafficManagerTool.HoveredNodeId = value;
        }

        /// <summary>Current hovered segment. Overridden by some tools.</summary>
        protected virtual ushort HoveredSegmentId {
            get => TrafficManagerTool.HoveredSegmentId;
            set => TrafficManagerTool.HoveredSegmentId = value;
        }

        protected ushort SelectedNodeId {
            get => TrafficManagerTool.SelectedNodeId;
            set => TrafficManagerTool.SelectedNodeId = value;
        }

        protected ushort SelectedSegmentId {
            get => TrafficManagerTool.SelectedSegmentId;
            set => TrafficManagerTool.SelectedSegmentId = value;
        }

        /// <summary>
        /// Tool reset code, can be run multiple times during the gameplay.
        /// Brings the tool to initial clean state and informs it that it is activated now.
        /// </summary>
        public abstract void ActivateTool();

        /// <summary>Tool has been switched off by user selecting another tool or hitting Esc.</summary>
        public abstract void DeactivateTool();

        /// <summary>Called every frame to display tool-specific overlay.</summary>
        /// <param name="cameraInfo">Camera.</param>
        public abstract void RenderOverlay(RenderManager.CameraInfo cameraInfo);

        /// <summary>
        /// Called whenever the mouse left click happened on the world, while the tool was active.
        /// </summary>
        public abstract void OnToolLeftClick();

        /// <summary>
        /// Called whenever the mouse right click happened on the world, while the tool was active.
        /// </summary>
        public abstract void OnToolRightClick();

        public abstract void UpdateEveryFrame();
    }
}