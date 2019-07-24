namespace CSUtil.Commons {
    public static class LogicUtil {
        public static bool CheckFlags(uint flags, uint flagMask, uint? expectedResult = null) {
            uint res = flags & flagMask;
            return expectedResult == null ? res != 0 : res == expectedResult;
        }
    }
}