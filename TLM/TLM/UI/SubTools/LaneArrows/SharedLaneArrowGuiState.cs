namespace TrafficManager.UI.SubTools.LaneArrows {
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// A shared state for GUI editing the junction. Only one lane editor can be
    /// active.
    /// </summary>
    internal class SharedLaneArrowsGuiState {
        public GameObject btnCurrentControlButton_;

        public GameObject btnLaneArrowForward_;

        public GameObject btnLaneArrowLeft_;

        public GameObject btnLaneArrowRight_;

        /// <summary>
        /// Used to draw lane on screen which is being edited.
        /// </summary>
        public uint selectedLaneId_;

        public void Reset() {
            selectedLaneId_ = 0;
            btnLaneArrowLeft_ = btnLaneArrowRight_ = btnLaneArrowForward_ = null;
        }

        /// <summary>
        /// When lane arrow button is destroyed we might want to decolorize the control button
        /// </summary>
        public void DestroyLaneArrowButtons() {
            if (btnCurrentControlButton_ != null) {
                btnCurrentControlButton_.GetComponentInParent<Image>().color = Color.white;
            }

            if (btnLaneArrowLeft_ != null) {
                Object.Destroy(btnLaneArrowLeft_);
            }

            if (btnLaneArrowForward_ != null) {
                Object.Destroy(btnLaneArrowForward_);
            }

            if (btnLaneArrowRight_ != null) {
                Object.Destroy(btnLaneArrowRight_);
            }

            btnCurrentControlButton_ = btnLaneArrowLeft_ = btnLaneArrowForward_ = btnLaneArrowRight_ = null;
        }
    }
}