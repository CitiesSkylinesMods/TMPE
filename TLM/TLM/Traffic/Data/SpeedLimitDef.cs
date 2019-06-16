using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.UI;
using UnityEngine;

namespace TrafficManager.Traffic.Data {
	/// <summary>
	/// Defines a speed limit value with default Kmph and display value of Mph
	/// for when the option is set to display Mph. The engine still uses kmph.
	/// </summary>
	public struct SpeedLimitDef {
		public readonly ushort Kmph;
		public readonly ushort Mph;

		public static SpeedLimitDef Km10 = new SpeedLimitDef(10, 5);
		public static SpeedLimitDef Km20 = new SpeedLimitDef(20, 12);
		public static SpeedLimitDef Km30 = new SpeedLimitDef(30, 20);
		public static SpeedLimitDef Km40 = new SpeedLimitDef(40, 25);
		public static SpeedLimitDef Km50 = new SpeedLimitDef(50, 30);
		public static SpeedLimitDef Km60 = new SpeedLimitDef(60, 40);
		public static SpeedLimitDef Km70 = new SpeedLimitDef(70, 45);
		public static SpeedLimitDef Km80 = new SpeedLimitDef(80, 50);
		public static SpeedLimitDef Km90 = new SpeedLimitDef(90, 55);
		public static SpeedLimitDef Km100 = new SpeedLimitDef(100, 60);
		public static SpeedLimitDef Km110 = new SpeedLimitDef(110, 70);
		public static SpeedLimitDef Km120 = new SpeedLimitDef(120, 75);
		public static SpeedLimitDef Km130 = new SpeedLimitDef(130, 80);
		public static SpeedLimitDef NoLimit = new SpeedLimitDef(0, 0);

		public static Dictionary<ushort, SpeedLimitDef> KmphToSpeedLimitDef =
			new Dictionary<ushort, SpeedLimitDef> { { 10, Km10 }, { 20, Km20 }, { 30, Km30 }, { 40, Km40 }, { 50, Km50 },
			                                      { 60, Km60 }, { 70, Km70 }, { 80, Km80 }, { 90, Km90 }, { 100, Km100 },
			                                      { 110, Km110 }, { 120, Km120 }, { 130, Km130 }, { 0, NoLimit } };

		public SpeedLimitDef(ushort kmph, ushort mph) {
			Kmph = kmph;
			Mph = mph;
		}

		public static bool DoesListContainKmph(List<SpeedLimitDef> lst, ushort kmph) {
			foreach (var v in lst) {
				if (v.Kmph == kmph) {
					return true;
				}
			}

			return false;
		}

		public static int FindKmphInList(List<SpeedLimitDef> lst, ushort kmph) {
			var index = 0;
			foreach (var v in lst) {
				if (v.Kmph == kmph) {
					return index;
				}

				index++;
			}

			return -1;
		}

		public string ToMphString() {
			if (Mph == 0) {
				return Translation.GetString("Speed_limit_unlimited");
			}

			return Mph + " mph";
		}

		public string ToKmphString() {
			if (Kmph == 0) {
				return Translation.GetString("Speed_limit_unlimited");
			}

			return Kmph + " km/h";
		}
	}
}