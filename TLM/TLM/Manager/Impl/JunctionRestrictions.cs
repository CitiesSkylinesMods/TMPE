namespace TrafficManager.Manager.Impl {
    using System;
    using CSUtil.Commons;

    public struct JunctionRestrictions {

        private JunctionRestrictionFlags values;

        private JunctionRestrictionFlags mask;

        private JunctionRestrictionFlags defaults;


        public void ClearValue(JunctionRestrictionFlags flags) {
            values &= ~flags;
            mask &= ~flags;
        }

        public void SetDefault(JunctionRestrictionFlags flags, bool value) {
            if (value)
                defaults |= flags;
            else
                defaults &= ~flags;
        }

        public bool GetDefault(JunctionRestrictionFlags flags) {
            return (defaults & flags) == flags;
        }

        public bool HasValue(JunctionRestrictionFlags flags) {
            return (mask & flags) == flags;
        }

        public TernaryBool GetTernaryBool(JunctionRestrictionFlags flags) {
            return (mask & flags) == flags
                    ? (values & flags) == flags
                        ? TernaryBool.True
                        : TernaryBool.False
                    : TernaryBool.Undefined;
        }

        public bool GetValueOrDefault(JunctionRestrictionFlags flags) {
            return ((values & flags & mask) | (defaults & flags & ~mask)) == flags;
        }

        public void SetValue(JunctionRestrictionFlags flags, TernaryBool value) {
            switch (value) {
                case TernaryBool.True:
                    values |= flags;
                    mask |= flags;
                    break;

                case TernaryBool.False:
                    values &= ~flags;
                    mask |= flags;
                    break;

                case TernaryBool.Undefined:
                    values &= ~flags;
                    mask &= ~flags;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        public bool IsDefault() {
            return ((values & mask) | (defaults & ~mask)) == defaults;
        }

        public void Reset(bool resetDefaults = true) {
            values = mask = default;

            if (resetDefaults) {
                defaults = default;
            }
        }

        public override string ToString() {
            return string.Format(
                $"[JunctionRestrictions\n\tvalues = {values}\n\tmask = {mask}\n" +
                $"defaults = {defaults}\n" +
                "JunctionRestrictions]");
        }
    }
}