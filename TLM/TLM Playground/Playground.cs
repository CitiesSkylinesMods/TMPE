using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager {
	class Playground {
		static void Main(string[] args) {
			Console.WriteLine("NaN: " + Mathf.RoundToInt(Single.NaN));
			Console.WriteLine("PositiveInfinity: " + Mathf.RoundToInt(Single.PositiveInfinity));
			Console.WriteLine("NegativeInfinity: " + Mathf.RoundToInt(Single.NegativeInfinity));
			Console.WriteLine("NegativeInfinity < 0: " + (Single.NegativeInfinity < 0));
			Console.WriteLine("PositiveInfinity < 0: " + (Single.PositiveInfinity < 0));
			Console.WriteLine("NaN < 0: " + (Single.NaN < 0));
			Console.WriteLine("NegativeInfinity > 1: " + (Single.NegativeInfinity > 1f));
			Console.WriteLine("PositiveInfinity > 1: " + (Single.PositiveInfinity > 1f));
			Console.WriteLine("NaN > 1: " + (Single.NaN > 1f));

			HashSet<ushort> hs = new HashSet<ushort>();
			hs.Add(123);
			hs.Add(456);
			Console.WriteLine(string.Join(", ", hs.Select(x => x.ToString()).ToArray()));

			Console.ReadLine();
		}
	}
}
