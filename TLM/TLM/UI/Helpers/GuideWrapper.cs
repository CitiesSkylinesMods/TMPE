namespace TrafficManager.UI {
    using System;
    using CSUtil.Commons;
    using TrafficManager.State;

    internal class GuideWrapper {
        private GenericGuide guide_;
        internal GuideInfo Info;

        public GuideWrapper(string key) {
            guide_ = new GenericGuide();
            Info = new GuideInfo {
                // These values are game defaults:
                m_delayType = GuideInfo.Delay.OccurrenceCount,
                m_displayDelay = 1,
                m_repeatDelay = 3,
                m_overrideOptions = true,
                m_icon = "ToolbarIconZoomOutGlobe",
                m_tag = "Generic",
                m_name = key,
            };
        }

        /// <summary>
        /// Check if the thread is correct.
        /// The return value is disregarded, so its now void.
        /// </summary>
        private void CheckStack() {
            if (System.Threading.Thread.CurrentThread.Name != "Simulation") {
                Log.Error("Guide should be handled from Simulation thread.");
            }
        }

        internal void Activate() {
            CheckStack();

            if (GlobalConfig.Instance.Debug.Guide) {
                Log._Debug("GuideWrapper.Activate was called");
            }

            guide_.Activate(Info ?? throw new Exception("m_info is null"));
        }

        internal void Deactivate() {
            CheckStack();

            if (GlobalConfig.Instance.Debug.Guide) {
                Log._Debug("GuideWrapper.Deactivate was called");
            }

            guide_.Deactivate();
        }
    }
}
