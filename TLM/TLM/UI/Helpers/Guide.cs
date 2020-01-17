using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework;
using ICities;
using UnityEngine;

namespace TrafficManager.UI.Helpers {
    public class GuidePair {
        public GenericGuide m_guide = new GenericGuide();
        public GuideInfo m_info;
        public void Activate() {
            if (m_info != null)
                m_guide?.Activate(m_info);
        }
        public void Deactivate() => m_guide?.Deactivate();

        public GuidePair(string s = "some text here") {
            m_info = new GuideInfo();
            m_info.m_name = s;
            m_info.m_delayType = GuideInfo.Delay.OccurrenceCount;
            m_info.m_displayDelay = 1;
            m_info.m_tag = "Generic";
            m_info.m_icon = "ToolbarIconRoads";
        }

    }


    public class Test {
        public void OnSuccess() {


        }
    }
}
