namespace TrafficManager.Manager.Impl {
    using System;
    using CSUtil.Commons;

    public struct JunctionRestrictions {
        private TernaryBool uturnAllowed;
        private TernaryBool nearTurnOnRedAllowed;
        private TernaryBool farTurnOnRedAllowed;
        private TernaryBool straightLaneChangingAllowed;
        private TernaryBool enterWhenBlockedAllowed;
        private TernaryBool pedestrianCrossingAllowed;

        private bool defaultUturnAllowed;
        private bool defaultNearTurnOnRedAllowed;
        private bool defaultFarTurnOnRedAllowed;
        private bool defaultStraightLaneChangingAllowed;
        private bool defaultEnterWhenBlockedAllowed;
        private bool defaultPedestrianCrossingAllowed;

        public void ClearValue(JunctionRestrictionFlags flags) {
            switch (flags) {
                case JunctionRestrictionFlags.AllowUTurn:
                    uturnAllowed = TernaryBool.Undefined;
                    break;

                case JunctionRestrictionFlags.AllowNearTurnOnRed:
                    nearTurnOnRedAllowed = TernaryBool.Undefined;
                    break;

                case JunctionRestrictionFlags.AllowFarTurnOnRed:
                    farTurnOnRedAllowed = TernaryBool.Undefined;
                    break;

                case JunctionRestrictionFlags.AllowForwardLaneChange:
                    straightLaneChangingAllowed = TernaryBool.Undefined;
                    break;

                case JunctionRestrictionFlags.AllowEnterWhenBlocked:
                    enterWhenBlockedAllowed = TernaryBool.Undefined;
                    break;

                case JunctionRestrictionFlags.AllowPedestrianCrossing:
                    pedestrianCrossingAllowed = TernaryBool.Undefined;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(flags));
            }
        }

        public void SetDefault(JunctionRestrictionFlags flags, bool value) {
            switch (flags) {
                case JunctionRestrictionFlags.AllowUTurn:
                    defaultUturnAllowed = value;
                    break;

                case JunctionRestrictionFlags.AllowNearTurnOnRed:
                    defaultNearTurnOnRedAllowed = value;
                    break;

                case JunctionRestrictionFlags.AllowFarTurnOnRed:
                    defaultFarTurnOnRedAllowed = value;
                    break;

                case JunctionRestrictionFlags.AllowForwardLaneChange:
                    defaultStraightLaneChangingAllowed = value;
                    break;

                case JunctionRestrictionFlags.AllowEnterWhenBlocked:
                    defaultEnterWhenBlockedAllowed = value;
                    break;

                case JunctionRestrictionFlags.AllowPedestrianCrossing:
                    defaultPedestrianCrossingAllowed = value;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(flags));
            }
        }

        public bool GetDefault(JunctionRestrictionFlags flags) {
            switch (flags) {
                case JunctionRestrictionFlags.AllowUTurn:
                    return defaultUturnAllowed;

                case JunctionRestrictionFlags.AllowNearTurnOnRed:
                    return defaultNearTurnOnRedAllowed;

                case JunctionRestrictionFlags.AllowFarTurnOnRed:
                    return defaultFarTurnOnRedAllowed;

                case JunctionRestrictionFlags.AllowForwardLaneChange:
                    return defaultStraightLaneChangingAllowed;

                case JunctionRestrictionFlags.AllowEnterWhenBlocked:
                    return defaultEnterWhenBlockedAllowed;

                case JunctionRestrictionFlags.AllowPedestrianCrossing:
                    return defaultPedestrianCrossingAllowed;

                default:
                    throw new ArgumentOutOfRangeException(nameof(flags));
            }
        }

        public bool HasValue(JunctionRestrictionFlags flags) {
            switch (flags) {
                case JunctionRestrictionFlags.AllowUTurn:
                    return uturnAllowed != TernaryBool.Undefined;

                case JunctionRestrictionFlags.AllowNearTurnOnRed:
                    return nearTurnOnRedAllowed != TernaryBool.Undefined;

                case JunctionRestrictionFlags.AllowFarTurnOnRed:
                    return farTurnOnRedAllowed != TernaryBool.Undefined;

                case JunctionRestrictionFlags.AllowForwardLaneChange:
                    return straightLaneChangingAllowed != TernaryBool.Undefined;

                case JunctionRestrictionFlags.AllowEnterWhenBlocked:
                    return enterWhenBlockedAllowed != TernaryBool.Undefined;

                case JunctionRestrictionFlags.AllowPedestrianCrossing:
                    return pedestrianCrossingAllowed != TernaryBool.Undefined;

                default:
                    throw new ArgumentOutOfRangeException(nameof(flags));
            }
        }

        public TernaryBool GetTernaryBool(JunctionRestrictionFlags flags) {
            switch (flags) {
                case JunctionRestrictionFlags.AllowUTurn:
                    return uturnAllowed;

                case JunctionRestrictionFlags.AllowNearTurnOnRed:
                    return nearTurnOnRedAllowed;

                case JunctionRestrictionFlags.AllowFarTurnOnRed:
                    return farTurnOnRedAllowed;

                case JunctionRestrictionFlags.AllowForwardLaneChange:
                    return straightLaneChangingAllowed;

                case JunctionRestrictionFlags.AllowEnterWhenBlocked:
                    return enterWhenBlockedAllowed;

                case JunctionRestrictionFlags.AllowPedestrianCrossing:
                    return pedestrianCrossingAllowed;

                default:
                    throw new ArgumentOutOfRangeException(nameof(flags));
            }
        }

        public bool IsUturnAllowed() {
            return uturnAllowed == TernaryBool.Undefined
                       ? defaultUturnAllowed
                       : TernaryBoolUtil.ToBool(uturnAllowed);
        }

        public bool IsNearTurnOnRedAllowed() {
            return nearTurnOnRedAllowed == TernaryBool.Undefined
                       ? defaultNearTurnOnRedAllowed
                       : TernaryBoolUtil.ToBool(nearTurnOnRedAllowed);
        }

        public bool IsFarTurnOnRedAllowed() {
            return farTurnOnRedAllowed == TernaryBool.Undefined
                       ? defaultFarTurnOnRedAllowed
                       : TernaryBoolUtil.ToBool(farTurnOnRedAllowed);
        }

        public bool IsLaneChangingAllowedWhenGoingStraight() {
            return straightLaneChangingAllowed == TernaryBool.Undefined
                       ? defaultStraightLaneChangingAllowed
                       : TernaryBoolUtil.ToBool(straightLaneChangingAllowed);
        }

        public bool IsEnteringBlockedJunctionAllowed() {
            return enterWhenBlockedAllowed == TernaryBool.Undefined
                       ? defaultEnterWhenBlockedAllowed
                       : TernaryBoolUtil.ToBool(enterWhenBlockedAllowed);
        }

        public bool IsPedestrianCrossingAllowed() {
            return pedestrianCrossingAllowed == TernaryBool.Undefined
                       ? defaultPedestrianCrossingAllowed
                       : TernaryBoolUtil.ToBool(pedestrianCrossingAllowed);
        }

        public void SetUturnAllowed(TernaryBool value) {
            uturnAllowed = value;
        }

        public void SetNearTurnOnRedAllowed(TernaryBool value) {
            nearTurnOnRedAllowed = value;
        }

        public void SetFarTurnOnRedAllowed(TernaryBool value) {
            farTurnOnRedAllowed = value;
        }

        public void SetLaneChangingAllowedWhenGoingStraight(TernaryBool value) {
            straightLaneChangingAllowed = value;
        }

        public void SetEnteringBlockedJunctionAllowed(TernaryBool value) {
            enterWhenBlockedAllowed = value;
        }

        public void SetPedestrianCrossingAllowed(TernaryBool value) {
            pedestrianCrossingAllowed = value;
        }

        public bool IsDefault() {
            bool uturnIsDefault = uturnAllowed == TernaryBool.Undefined ||
                                  TernaryBoolUtil.ToBool(uturnAllowed) == defaultUturnAllowed;
            bool nearTurnOnRedIsDefault = nearTurnOnRedAllowed == TernaryBool.Undefined ||
                                          TernaryBoolUtil.ToBool(nearTurnOnRedAllowed) ==
                                          defaultNearTurnOnRedAllowed;
            bool farTurnOnRedIsDefault = farTurnOnRedAllowed == TernaryBool.Undefined ||
                                         TernaryBoolUtil.ToBool(farTurnOnRedAllowed) ==
                                         defaultFarTurnOnRedAllowed;
            bool straightChangeIsDefault = straightLaneChangingAllowed == TernaryBool.Undefined ||
                                           TernaryBoolUtil.ToBool(straightLaneChangingAllowed) ==
                                           defaultStraightLaneChangingAllowed;
            bool enterWhenBlockedIsDefault = enterWhenBlockedAllowed == TernaryBool.Undefined ||
                                             TernaryBoolUtil.ToBool(enterWhenBlockedAllowed) ==
                                             defaultEnterWhenBlockedAllowed;
            bool pedCrossingIsDefault = pedestrianCrossingAllowed == TernaryBool.Undefined ||
                                        TernaryBoolUtil.ToBool(pedestrianCrossingAllowed) ==
                                        defaultPedestrianCrossingAllowed;

            return uturnIsDefault && nearTurnOnRedIsDefault && farTurnOnRedIsDefault &&
                   straightChangeIsDefault && enterWhenBlockedIsDefault && pedCrossingIsDefault;
        }

        public void Reset(bool resetDefaults = true) {
            uturnAllowed = TernaryBool.Undefined;
            nearTurnOnRedAllowed = TernaryBool.Undefined;
            farTurnOnRedAllowed = TernaryBool.Undefined;
            straightLaneChangingAllowed = TernaryBool.Undefined;
            enterWhenBlockedAllowed = TernaryBool.Undefined;
            pedestrianCrossingAllowed = TernaryBool.Undefined;

            if (resetDefaults) {
                defaultUturnAllowed = false;
                defaultNearTurnOnRedAllowed = false;
                defaultFarTurnOnRedAllowed = false;
                defaultStraightLaneChangingAllowed = false;
                defaultEnterWhenBlockedAllowed = false;
                defaultPedestrianCrossingAllowed = false;
            }
        }

        public override string ToString() {
            return string.Format(
                "[JunctionRestrictions\n\tuturnAllowed = {0}\n\tnearTurnOnRedAllowed = {1}\n" +
                "\tfarTurnOnRedAllowed = {2}\n\tstraightLaneChangingAllowed = {3}\n\t" +
                "enterWhenBlockedAllowed = {4}\n\tpedestrianCrossingAllowed = {5}\n" +
                "JunctionRestrictions]",
                uturnAllowed,
                nearTurnOnRedAllowed,
                farTurnOnRedAllowed,
                straightLaneChangingAllowed,
                enterWhenBlockedAllowed,
                pedestrianCrossingAllowed);
        }
    }
}