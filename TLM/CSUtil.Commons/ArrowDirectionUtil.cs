using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSUtil.Commons {
	public class ArrowDirectionUtil {
		public static ArrowDirection InvertLeftRight(ArrowDirection dir) {
			if (dir == ArrowDirection.Left)
				dir = ArrowDirection.Right;
			else if (dir == ArrowDirection.Right)
				dir = ArrowDirection.Left;
			return dir;
		}

		/// <summary>
		/// Calculates the direction of <paramref name="toRelDir"/> in relation to ArrowDirection.TURN.
		/// </summary>
		/// <param name="fromDir">source direction</param>
		/// <param name="toRelDir">target direction, relative to <paramref name="fromDir"/></param>
		/// <returns></returns>
		public static ArrowDirection MakeAbsolute(ArrowDirection fromDir, ArrowDirection toRelDir) {
			if (fromDir == ArrowDirection.None) {
				// invalid direction
				return ArrowDirection.None;
			}

			if (toRelDir == ArrowDirection.None) {
				// invalid direction
				return ArrowDirection.None;
			}

			if (fromDir == ArrowDirection.Turn) {
				// toRelDir is already relative to TURN
				return toRelDir;
			}

			if (toRelDir == ArrowDirection.Turn) {
				// toRelDir is fromDir
				return fromDir;
			}

			int fromDirBase0 = (int)fromDir - 1;
			int toRelDirBase1 = (int)toRelDir;
			/*
			 * Direction | Base 0 | Base 1
			 * ==========+========+=======
			 * Left      | 0      | 1
			 * Forward   | 1      | 2
			 * Right     | 2      | 3
			 * 
			 *
			 * Direction 1 | Direction 2 | Dir. 1 B0 | Dir. 2 B1 | Sum | (Sum + 1) % 4 | Desired dir.
			 * ============+=============+===========+===========+=====|===============+=============
			 * Left        | Left        | 0         | 1         | 1   | 2             | (Forward, 2)
			 * Left        | Forward     | 0         | 2         | 2   | 3             | (Right, 3)
			 * Left        | Right       | 0         | 3         | 3   | 0             | (Turn, 4)
			 * Forward     | Left        | 1         | 1         | 2   | 3             | (Right, 3)
			 * Forward     | Forward     | 1         | 2         | 3   | 0             | (Turn, 4)
			 * Forward     | Right       | 1         | 3         | 4   | 1             | (Left, 1)
			 * Right       | Left        | 2         | 1         | 3   | 0             | (Turn, 4)
			 * Right       | Forward     | 2         | 2         | 4   | 1             | (Left, 1)
			 * Right       | Right       | 2         | 3         | 5   | 2             | (Forward, 2)
			 */

			int ret = (fromDirBase0 + toRelDirBase1 + 1) % 4;
			if (ret == 0) {
				ret = 4;
			}
			return (ArrowDirection)ret;
		}
	}
}
