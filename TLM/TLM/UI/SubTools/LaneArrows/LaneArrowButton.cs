namespace TrafficManager.UI.SubTools.LaneArrows {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;

    /// <summary>
    /// Button in Lane Arrows editor, containing a direction arrow.
    /// </summary>
    public class LaneArrowButton: U.Button.BaseUButton {
        public uint LaneId = 0;

        public NetLane.Flags NetlaneFlagsMask = NetLane.Flags.None;

        public bool StartNode;

        internal LaneArrowTool ParentTool;

        public LaneArrows ToggleFlag { get; set; }

        public override bool IsVisible() => true;

        public override void HandleClick(UIMouseEventParameter p) {
            if (LaneId == 0) {
                Log.Error("LaneArrowButton.Click: LaneId is 0, too bad");
                return;
            }
            LaneArrowManager.Instance.ToggleLaneArrows(
                this.LaneId,
                StartNode,
                ToggleFlag,
                out var res);
            ParentTool.InformUserAboutPossibleFailure(res);
            this.UpdateButtonImageAndTooltip();
        }

        /// <summary>UButton disables this, we want this enabled.</summary>
        /// <returns>True.</returns>
        public override bool CanActivate() => true;

        public override string ButtonName => "TMPE_LaneArrow";

        public override bool IsActive() {
            if (LaneId == 0) {
                Log.Error("LaneArrowButton: LaneId is 0, too bad");
                return false;
            }
            NetLane[] lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;
            NetLane.Flags flags = (NetLane.Flags)lanesBuffer[LaneId].m_flags;
            return (flags & NetlaneFlagsMask) == NetlaneFlagsMask;
        }

        public override string GetTooltip() {
            return string.Empty;
        }
    }
}