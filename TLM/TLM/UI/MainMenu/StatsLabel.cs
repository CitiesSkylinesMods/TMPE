namespace TrafficManager.UI.MainMenu {
    using TrafficManager.Custom.PathFinding;
    using UnityEngine;

    public class StatsLabel : U.ULabel {
        private uint _previousValue = 0;

        public override void Start() {
            base.Start();
            this.text = "0";
            this.suffix = " pathfinds";
            this.textColor = Color.green;
        }

#if QUEUEDSTATS
        public override void Update() {
            uint queued = CustomPathManager.TotalQueuedPathFinds;
            if (queued == _previousValue) {
                return;
            }

            if (queued < 1000) {
                textColor = Color.Lerp(Color.green, Color.yellow, queued / 1000f);
            } else if (queued < 2500) {
                textColor = Color.Lerp(
                    Color.yellow,
                    Color.red,
                    (queued - 1000f) / 1500f);
            } else {
                textColor = Color.red;
            }

            text = queued.ToString();
            _previousValue = queued;
        }
#endif
    }
}