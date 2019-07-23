namespace CSUtil.Commons {
    public static class ArrowDirectionUtil {
        public static ArrowDirection InvertLeftRight(ArrowDirection dir) {
            switch (dir) {
                case ArrowDirection.Left:
                    return ArrowDirection.Right;
                case ArrowDirection.Right:
                    return ArrowDirection.Left;
            }

            return dir;
        }
    }
}