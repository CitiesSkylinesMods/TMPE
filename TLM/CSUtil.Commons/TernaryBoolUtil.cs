namespace CSUtil.Commons {
    using System;

    public static class TernaryBoolUtil {
        public static bool ToBool(TernaryBool tb) {
            if (tb == TernaryBool.Undefined) {
                throw new ArgumentException("Cannot determine boolean value for undefined ternary bool");
            }

            return tb == TernaryBool.True;
        }

        public static bool? ToOptBool(TernaryBool tb) {
            if (tb == TernaryBool.Undefined) {
                return null;
            }

            return tb == TernaryBool.True;
        }

        public static TernaryBool ToTernaryBool(bool? b) {
            switch (b) {
                case null:
                default:
                    return TernaryBool.Undefined;
                case false:
                    return TernaryBool.False;
                case true:
                    return TernaryBool.True;
            }
        }
    }
}