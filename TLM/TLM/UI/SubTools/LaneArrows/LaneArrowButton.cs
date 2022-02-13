namespace TrafficManager.UI.SubTools.LaneArrows {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;

    /// <summary>
    /// Button in Lane Arrows editor, containing a direction arrow.
    /// </summary>
    public class LaneArrowButton: U.BaseUButton {
        public uint LaneId = 0;

        public NetLane.Flags NetlaneFlagsMask = NetLane.Flags.None;

        public bool StartNode;

        internal LaneArrowTool ParentTool;

        public LaneArrows ToggleFlag { get; set; }

        protected override bool IsVisible() => true;

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
            this.UpdateButtonSkinAndTooltip();
        }

        /// <summary>UButton disables this, we want this enabled.</summary>
        /// <returns>True.</returns>
        public override bool CanActivate() => true;

        protected override bool IsActive() {
            if (LaneId == 0) {
                Log.Error("LaneArrowButton: LaneId is 0, too bad");
                return false;
            }

            NetLane.Flags flags = (NetLane.Flags)LaneId.ToLane().m_flags;
            return (flags & NetlaneFlagsMask) == NetlaneFlagsMask;
        }

        protected override string U_OverrideTooltipText() {
            return string.Empty;
        }
    }
}