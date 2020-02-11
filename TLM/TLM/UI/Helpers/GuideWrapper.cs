namespace TrafficManager.UI {
    using System;
    using CSUtil.Commons;

    internal class GuideWrapper {
        private GenericGuide m_guide;
        internal GuideInfo m_info;

        public GuideWrapper(string key) {
            m_guide = new GenericGuide();
            m_info = new GuideInfo {
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

        private bool CheckStack() {
            if (System.Threading.Thread.CurrentThread.Name != "Simulation") {
                Log.Error("Guide should be handled from Simulation thread.");
                return false;
            }
            return true;
        }

        internal void Activate() {
            CheckStack();
            Log._Debug("GuideWrapper.Activate was called");
            m_guide.Activate(m_info ?? throw new Exception("m_info is null"));
        }

        internal void Deactivate() {
            CheckStack();
            Log._Debug("GuideWrapper.Deactivate was called");
            m_guide.Deactivate();
        }
    }
}
