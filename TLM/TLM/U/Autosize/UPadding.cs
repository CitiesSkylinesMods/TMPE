namespace TrafficManager.U.Autosize {
    public struct UPadding {
        public float Top;
        public float Right;
        public float Bottom;
        public float Left;

        public static UPadding Default = new UPadding() {
            Top = UConst.UIPADDING, Right = UConst.UIPADDING, Bottom = UConst.UIPADDING,
            Left = UConst.UIPADDING,
        };

        public static UPadding Zero = new UPadding() {
            Top = 0f, Right = 0f, Bottom = 0f, Left = 0f,
        };

        public UPadding(float top, float right, float bottom, float left) {
            Right = right;
            Top = top;
            Left = left;
            Bottom = bottom;
        }
    }
}