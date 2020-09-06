namespace TrafficManager.Util {
    /// <summary>Represents a pair of integers.</summary>
    public struct IntVector2 {
        public int x;
        public int y;

        /// <summary>Initializes a new instance of the <see cref="IntVector2"/> struct.</summary>
        /// <param name="x">First value.</param>
        /// <param name="y">Second value.</param>
        public IntVector2(int x, int y) {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntVector2"/> struct with same value.
        /// </summary>
        /// <param name="xy">Both first and second value.</param>
        public IntVector2(int xy) {
            this.x = this.y = xy;
        }
    }
}