using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager {
	public class PrintTransportLines {
#if XXX
		public void run() {
			for (int i = 0; i < TransportManager.MAX_LINE_COUNT; ++i) {
				/*if (TransportManager.instance.m_lines.m_buffer[i].Info.m_transportType != TransportInfo.TransportType.Tram) {
					continue;
				}*/
				//Log.Message("Transport line " + i + ":");
				if ((TransportManager.instance.m_lines.m_buffer[i].m_flags & TransportLine.Flags.Created) == TransportLine.Flags.None) {
					//Log.Message("\tTransport line is not created.");
				}
				//Log.Message("\tFlags: " + TransportManager.instance.m_lines.m_buffer[i].m_flags + ", cat: " + TransportManager.instance.m_lines.m_buffer[i].Info.category + ", type: " + TransportManager.instance.m_lines.m_buffer[i].Info.m_transportType + ", name: " + TransportManager.instance.GetLineName((ushort)i));
				ushort firstStopNodeId = TransportManager.instance.m_lines.m_buffer[i].m_stops;
				ushort stopNodeId = firstStopNodeId;
				Vector3 lastNodePos = Vector3.zero;
				int index = 1;
				while (stopNodeId != 0) {
					Vector3 pos = NetManager.instance.m_nodes.m_buffer[stopNodeId].m_position;
					if (NetManager.instance.m_nodes.m_buffer[stopNodeId].m_problems != Notification.Problem.None) {
						Log.Message("\tStop node #" + index + " -- " + stopNodeId + ": Flags: " + NetManager.instance.m_nodes.m_buffer[stopNodeId].m_flags + ", Transport line: " + NetManager.instance.m_nodes.m_buffer[stopNodeId].m_transportLine + ", Problems: " + NetManager.instance.m_nodes.m_buffer[stopNodeId].m_problems + " Pos: " + pos + ", Dist. to lat pos: " + (lastNodePos - pos).magnitude);
						Log.Message("\tFlags: " + TransportManager.instance.m_lines.m_buffer[i].m_flags + ", cat: " + TransportManager.instance.m_lines.m_buffer[i].Info.category + ", type: " + TransportManager.instance.m_lines.m_buffer[i].Info.m_transportType + ", name: " + TransportManager.instance.GetLineName((ushort)i));
					}
					lastNodePos = pos;

					ushort nextSegment = TransportLine.GetNextSegment(stopNodeId);
					if (nextSegment != 0) {
						stopNodeId = NetManager.instance.m_segments.m_buffer[(int)nextSegment].m_endNode;
					} else {
						break;
					}
					++index;

					if (stopNodeId == firstStopNodeId) {
						break;
					}

					if (index > 10000) {
						Log.Message("Too many iterations!");
						break;
					}
				}
			}
		}
#endif
	}
}
