using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrafficManager.Util;

namespace SpiralLoopTest {
	class Program {
		static void Main(string[] args) {
			LoopUtil.SpiralLoop(0, 0, 3, 3, delegate(int x, int y) {
				Console.WriteLine($"x={x} y={y}");
				return true;
			});
			Console.ReadLine();
		}
	}
}
