namespace TrafficManager.U.Autosize {
    public struct UPadding {
        public float Top;
        public float Right;
        public float Bottom;
        public float Left;

        public static UPadding Zero() {
            return new() {
                Right = 0f,
                Top = 0f,
                Left = 0f,
                Bottom = 0f,
            };
        }

        /// <summary>Set padding on all sides to <see cref="UConst.UIPADDING"/> (4f).</summary>
        public static UPadding Const() {
            return SameValue(UConst.UIPADDING);
        }

        public static UPadding SameValue(float v) {
            return new() {
                Right = v,
                Top = v,
                Left = v,
                Bottom = v,
            };
        }

        public UPadding(float top, float right, float bottom, float left) {
            Right = right;
            Top = top;
            Left = left;
            Bottom = bottom;
        }
    }
}