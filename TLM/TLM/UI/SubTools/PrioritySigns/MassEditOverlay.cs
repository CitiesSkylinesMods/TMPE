namespace TrafficManager.UI.SubTools.PrioritySigns {
    using System;

    /// <summary>Thread safe handling of mass edit overlay.</summary>
    public static class MassEditOverlay {
        private static object _lock = new object();

        private static bool _show = false;
        public static bool Show {
            get {
                lock (_lock) {
                    return _show;
                }
            }
            set {
                lock (_lock) {
                    _show = value;
                }
            }
        }

        private static DateTime _timer = DateTime.MinValue;

        /// <summary>
        /// show mass edit over lay for the input duration.
        /// overrides MassEditOVerlay.Show when it is set to a UTC time in future.
        /// seconds is
        /// seconds is
        /// </summary>
        /// <param name="seconds"> duration.
        /// negative => never
        /// float.MaxValue => always
        /// </param>
        public static void SetTimer(float seconds) {
            DateTime dt;
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
        /// show overlay for other subtools influced by mass edit.
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