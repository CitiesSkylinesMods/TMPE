using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework.UI;
using UnityEngine;

namespace TrafficManager
{
    class UITimedLights : UIPanel
    {
        public static TrafficLightTool trafficLightTool;

        public override void Start()
        {
            trafficLightTool = LoadingExtension.Instance.TrafficLightTool;
            //this makes the panel "visible", I don't know what sprites are available, but found this value to work
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(75, 75, 135, 255);
            this.width = 800;
            this.height = 400;
            this.relativePosition = new Vector3(10.48f, 80f);

            UILabel l = this.AddUIComponent<UILabel>();
            l.text = "Timed Scripts";
            l.relativePosition = new Vector3(65.0f, 5.0f);
        }
    }
}
