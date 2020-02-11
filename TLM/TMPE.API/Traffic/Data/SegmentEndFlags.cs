namespace TrafficManager.API.Traffic.Data {
    using System;
    using CSUtil.Commons;

    /// <summary>
    /// Segment end flags store junction restrictions
    /// </summary>
    public struct SegmentEndFlags {
        public TernaryBool uturnAllowed;
        public TernaryBool nearTurnOnRedAllowed;
        public TernaryBool farTurnOnRedAllowed;
        public TernaryBool straightLaneChangingAllowed;
        public TernaryBool enterWhenBlockedAllowed;
        public TernaryBool pedestrianCrossingAllowed;

        public bool defaultUturnAllowed;
        public bool defaultNearTurnOnRedAllowed;
        public bool defaultFarTurnOnRedAllowed;
        public bool defaultStraightLaneChangingAllowed;
        public bool defaultEnterWhenBlockedAllowed;
        public bool defaultPedestrianCrossingAllowed;

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
                "[SegmentEndFlags\n\tuturnAllowed = {0}\n\tnearTurnOnRedAllowed = {1}\n" +
                "\tfarTurnOnRedAllowed = {2}\n\tstraightLaneChangingAllowed = {3}\n\t" +
                "enterWhenBlockedAllowed = {4}\n\tpedestrianCrossingAllowed = {5}\n" +
                "SegmentEndFlags]",
                uturnAllowed,
                nearTurnOnRedAllowed,
                farTurnOnRedAllowed,
                straightLaneChangingAllowed,
                enterWhenBlockedAllowed,
                pedestrianCrossingAllowed);
        }
    }
}