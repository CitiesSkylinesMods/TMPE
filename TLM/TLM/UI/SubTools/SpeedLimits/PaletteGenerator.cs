namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;

    /// <summary>Produces list of speed limits to offer user in the palette.</summary>
    public class PaletteGenerator {
        /// <summary>Produces list of speed limits to offer user in the palette.</summary>
        /// <param name="unit">What kind of speed limit list is required</param>
        /// <returns>List from smallest to largest speed with the given unit. Zero (no limit) is not added to the list.
        /// The values are in-game speeds as float.</returns>
        public static List<SpeedValue> AllSpeedLimits(SpeedUnit unit) {
            var result = new List<SpeedValue>();
            switch (unit) {
                case SpeedUnit.Kmph:
                    for (var km = SpeedLimitsTool.LOWER_KMPH;
                         km <= SpeedLimitsTool.UPPER_KMPH;
                         km += SpeedLimitsTool.KMPH_STEP) {
                        result.Add(SpeedValue.FromKmph(km));
                    }

                    break;
                case SpeedUnit.Mph:
                    for (var mi = SpeedLimitsTool.LOWER_MPH;
                         mi <= SpeedLimitsTool.UPPER_MPH;
                         mi += SpeedLimitsTool.MPH_STEP) {
                        result.Add(SpeedValue.FromMph(mi));
                    }

                    break;
                case SpeedUnit.CurrentlyConfigured:
                    // Automatically choose from the config
                    return GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                        ? AllSpeedLimits(SpeedUnit.Mph)
                        : AllSpeedLimits(SpeedUnit.Kmph);
            }

            return result;
        }
    }
}