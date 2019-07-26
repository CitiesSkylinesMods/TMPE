﻿namespace TrafficManager.Traffic.Data {
    using System;
    using System.Collections.Generic;
    using State;
    using UI;
    using UnityEngine;

    public enum SpeedUnit {
        CurrentlyConfigured, // Currently selected in the options menu
        Kmph,
        Mph
    }

    public enum MphSignStyle {
        SquareUS = 0,
        RoundUK = 1,
        RoundGerman = 2,
    }

    /// <summary>
    /// Defines a speed limit value with default Kmph and display value of Mph
    /// for when the option is set to display Mph. The engine still uses kmph.
    /// </summary>
    public struct SpeedLimit {
        public const float SPEED_TO_KMPH = 50.0f; // 1.0f equals 50 km/h
        private const ushort LOWER_KMPH = 10;
        public const ushort UPPER_KMPH = 140;
        private const ushort KMPH_STEP = 10;

        public const int
            BREAK_PALETTE_COLUMN_KMPH = 8; // palette shows N in a row, then break and another row

        private const float SPEED_TO_MPH = 32.06f; // 50 km/h converted to mph
        private const ushort LOWER_MPH = 5;
        public const ushort UPPER_MPH = 90;
        private const ushort MPH_STEP = 5;

        public const int
            BREAK_PALETTE_COLUMN_MPH = 10; // palette shows M in a row, then break and another row

        private const float LOWER_SPEED = 0.1f;
        private const float UPPER_SPEED = 2 * 10.0f; // 1000 km/h

        /// <summary>
        /// Produces list of speed limits to offer user in the palette
        /// </summary>
        /// <param name="unit">What kind of speed limit list is required</param>
        /// <returns>List from smallest to largest speed with the given unit. Zero (no limit) is not added to the list.
        /// The values are in-game speeds as float.</returns>
        public static List<float> EnumerateSpeedLimits(SpeedUnit unit) {
            var result = new List<float>();
            switch (unit) {
                case SpeedUnit.Kmph:
                    for (var km = LOWER_KMPH; km <= UPPER_KMPH; km += KMPH_STEP) {
                        result.Add(km / SPEED_TO_KMPH);
                    }

                    break;
                case SpeedUnit.Mph:
                    for (var mi = LOWER_MPH; mi <= UPPER_MPH; mi += MPH_STEP) {
                        result.Add(mi / SPEED_TO_MPH);
                    }

                    break;
                case SpeedUnit.CurrentlyConfigured:
                    // Automatically choose from the config
                    return GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                               ? EnumerateSpeedLimits(SpeedUnit.Mph)
                               : EnumerateSpeedLimits(SpeedUnit.Kmph);
            }

            return result;
        }

        public static string ToMphPreciseString(float speed) {
            if (IsZero(speed)) {
                return Translation.GetString("Speed_limit_unlimited");
            }

            return ToMphPrecise(speed) + " MPH";
        }

        public static string ToKmphPreciseString(float speed) {
            if (IsZero(speed)) {
                return Translation.GetString("Speed_limit_unlimited");
            }

            return ToKmphPrecise(speed) + " km/h";
        }

        public static bool NearlyEqual(float a, float b) {
            return Mathf.Abs(a - b) < 0.001f;
        }

        public static bool IsZero(float speed) {
            return Math.Abs(speed) < Constants.VERY_SMALL_FLOAT;
        }

        public static bool IsValidRange(float speed) {
            return IsZero(speed) || (speed >= LOWER_SPEED && speed <= UPPER_SPEED);
        }

        /// <summary>
        /// Convert float game speed to mph and round to nearest STEP
        /// </summary>
        /// <param name="speed">Speed, scale: 1f=32 MPH</param>
        /// <returns>Speed in MPH rounded to nearest 5 MPH</returns>
        public static ushort ToMphRounded(float speed) {
            var mph = speed * SPEED_TO_MPH;
            return (ushort)(Mathf.Round(mph / MPH_STEP) * MPH_STEP);
        }

        public static ushort ToMphPrecise(float speed) {
            return (ushort)Mathf.Round(speed * SPEED_TO_MPH);
        }

        /// <summary>
        /// Convert float game speed to km/h and round to nearest STEP
        /// </summary>
        /// <param name="speed">Speed, scale: 1f=50km/h</param>
        /// <returns>Speed in km/h rounded to nearest 10 km/h</returns>
        public static ushort ToKmphRounded(float speed) {
            var kmph = speed * SPEED_TO_KMPH;
            return (ushort)(Mathf.Round(kmph / KMPH_STEP) * KMPH_STEP);
        }

        public static ushort ToKmphPrecise(float speed) {
            return (ushort)Mathf.Round(speed * SPEED_TO_KMPH);
        }

        /// <summary>
        /// Based on the MPH/KMPH settings round the current speed to the nearest STEP and
        /// then decrease by STEP.
        /// </summary>
        /// <param name="speed">Ingame speed</param>
        /// <returns>Ingame speed decreased by the increment for MPH or KMPH</returns>
        public static float GetPrevious(float speed) {
            if (speed < 0f) {
                return -1f;
            }

            if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph) {
                ushort rounded = ToMphRounded(speed);
                if (rounded == LOWER_MPH) {
                    return 0;
                }

                if (rounded == 0) {
                    return UPPER_MPH / SPEED_TO_MPH;
                }

                return (rounded > LOWER_MPH ? rounded - MPH_STEP : LOWER_MPH) / SPEED_TO_MPH;
            } else {
                ushort rounded = ToKmphRounded(speed);
                if (rounded == LOWER_KMPH) {
                    return 0;
                }

                if (rounded == 0) {
                    return UPPER_KMPH / SPEED_TO_KMPH;
                }

                return (rounded > LOWER_KMPH ? rounded - KMPH_STEP : LOWER_KMPH) / SPEED_TO_KMPH;
            }
        }

        /// <summary>
        /// Based on the MPH/KMPH settings round the current speed to the nearest STEP and
        /// then increase by STEP.
        /// </summary>
        /// <param name="speed">Ingame speed</param>
        /// <returns>Ingame speed increased by the increment for MPH or KMPH</returns>
        public static float GetNext(float speed) {
            if (speed < 0f) {
                return -1f;
            }

            if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph) {
                ushort rounded = ToMphRounded(speed);
                rounded += MPH_STEP;

                if (rounded > UPPER_MPH) {
                    rounded = 0;
                }

                return rounded / SPEED_TO_MPH;
            } else {
                ushort rounded = ToKmphRounded(speed);
                rounded += KMPH_STEP;

                if (rounded > UPPER_KMPH) {
                    rounded = 0;
                }

                return rounded / SPEED_TO_KMPH;
            }
        }

        /// <summary>
        /// For US signs and MPH enabled, scale textures vertically by 1.25f.
        /// Other signs are round.
        /// </summary>
        /// <returns>Multiplier for horizontal sign size</returns>
        public static float GetVerticalTextureScale() {
            return (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph &&
                    GlobalConfig.Instance.Main.MphRoadSignStyle == MphSignStyle.SquareUS)
                       ? 1.25f
                       : 1.0f;
        }
    }
}