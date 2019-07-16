namespace CSUtil.Commons {
    public static class LogicUtil {
        public static bool CheckFlags(uint flags, uint flagMask, uint? expectedResult = null) {
            var res = flags & flagMask;
            if (expectedResult == null) {
                return res != 0;
            }

            return res == expectedResult;
        }
    }
}