using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CSUtil.Redirection {
	public static class HarmonyUtil {
		/// <summary>
		/// Finds the method that is represented by the given harmony method.
		/// </summary>
		/// <param name="method">queried harmony method</param>
		/// <returns>method information</returns>
		public static MethodBase GetOriginalMethod(HarmonyMethod method) {
			if (method.originalType == null) return null;
			if (method.methodName == null)
				return AccessTools.Constructor(method.originalType, method.parameter);
			return AccessTools.Method(method.originalType, method.methodName, method.parameter);
		}
	}
}
