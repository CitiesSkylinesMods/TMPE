using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSUtil.Commons {
	public static class LogicUtil {
		public static bool CheckFlags(uint flags, uint flagMask, uint? expectedResult=null) {
			uint res = flags & flagMask;
			if (expectedResult == null) {
				return res != 0;
			} else {
				return res == expectedResult;
			}
		}
	}
}
