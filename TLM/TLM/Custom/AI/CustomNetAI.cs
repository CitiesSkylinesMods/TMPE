using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Custom.AI {
	public class CustomNetAI : NetAI {
		public void CustomAfterSplitOrMove(ushort nodeID, ref NetNode data, ushort origNodeID1, ushort origNodeID2) {
			Log._Debug($"CustomNetAI.CustomAfterSplitOrMove called. nodeID={nodeID}, origNodeID1={origNodeID1}, origNodeID2={origNodeID2}");
		}
	}
}
