using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Custom.AssetManager
{
    /* This is a disgusting hack which will definitely be romoved later. It ensures that the TMPE data are applied not when the intersection is built,
     * but a few millisecods later */
    public class ProvisionalDataLoader
    {
        private static bool timeUp = false;
        private static Timer aTimer;

        private static Configuration config;

        public static void SimulationStep()
        {
            if (timeUp)
            {
                timeUp = false;

                try {
                    SerializableDataExtension.LoadDataState(config,true, out bool error2);
                    if (error2)
                        throw new Exception("Tmpe: error when applying loaded data");
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e);
                }

            }
        }

        public static void StartTimer(Configuration config)
        {
            ProvisionalDataLoader.config = config;
            aTimer = new Timer();
            aTimer.Interval = 50;

            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += (a, b) =>
            {
                timeUp = true;
                aTimer.Stop();
                aTimer.Dispose();
            };

            // Have the timer fire repeated events (true is the default)
            aTimer.AutoReset = false;

            // Start the timer
            aTimer.Enabled = true;
        }
    }
}
