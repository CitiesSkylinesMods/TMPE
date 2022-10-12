namespace TrafficManager.UI.SubTools.PrioritySigns {
    using System;

    /// <summary>Thread safe handling of mass edit overlay.</summary>
    public static class MassEditOverlay {
        private static object _lock = new object();

        private static bool _show = false;

        public static bool Show {
            set {
                lock (_lock) {
                    _show = value;
                }
            }
        }

        private static DateTime _timer = DateTime.MinValue;

        /// <summary>
        /// Show mass edit overlay for the input duration.
        /// Overrides MassEditOverlay.Show when it is set to a UTC time in future.
        /// </summary>
        /// <param name="seconds"> duration. Negative => never, float.MaxValue => always.</param>
        public static void SetTimer(float seconds) {
            DateTime dt;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (seconds == Single.MaxValue) {
                dt = DateTime.MaxValue;
            } else {
                dt = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            }
            lock (_lock) {
                _timer = dt;
            }
        }

        /// <summary>
        /// show overlay for other subtools influenced by mass edit.
        /// </summary>
        public static bool IsActive {
            get {
                bool show;
                DateTime timer;
                lock (_lock) {
                    show = _show;
                    timer = _timer;
                }
                return show || DateTime.UtcNow < timer;
            }
        }
    }
}