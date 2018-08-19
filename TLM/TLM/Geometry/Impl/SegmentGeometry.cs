using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using System.Linq;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.Geometry.Impl {
	/// <summary>
	/// Manages segment geometry data (e.g. if a segment is one-way or not, which incoming/outgoing segments are connected at the start or end node) of one specific segment.
	/// Directional data (left, right, straight) is always given relatively to the managed segment.
	/// The terms "incoming"/"outgoing" refer to vehicles being able to move to/from the managed segment: Vehicles may to go to the managed segment if the other segment
	/// is "incoming". Vehicles may go to the other segment if it is "outgoing".
	/// 
	/// Segment geometry data is primarily updated by the path-finding master thread (see method CustomPathFind.ProcessItemMain and field CustomPathFind.IsMasterPathFind).
	/// However, other methods may manually update geometry data by calling the "Recalculate" method. This is especially necessary for segments that are not visited by the
	/// path-finding algorithm (apparently if a segment is not used by any vehicle)
	/// </summary>
	public class SegmentGeometry : ISegmentGeometry, IEquatable<SegmentGeometry> {
		private static SegmentGeometry[] segmentGeometries;

		public static void PrintDebugInfo() {
			string buf =
			"--------------------------\n" +
			"--- SEGMENT GEOMETRIES ---\n" +
			"--------------------------\n";
			foreach (SegmentGeometry segGeo in segmentGeometries) {
				if (segGeo.Valid) {
					buf += segGeo.ToString() + "\n" +
					"-------------------------\n";
				}
			}
			Log.Info(buf);
		}

		/*public LaneGeometry[] LaneGeometries {
			get; private set;
		} = null;*/

		/// <summary>
		/// The id of the managed segment
		/// </summary>
		public ushort SegmentId {
			get; private set;
		}

		private SegmentEndGeometry startNodeGeometry;
		private SegmentEndGeometry endNodeGeometry;

		public ISegmentEndGeometry StartNodeGeometry {
			get {
				if (startNodeGeometry.Valid)
					return startNodeGeometry;
				else
					return null;
			}
		}

		public ISegmentEndGeometry EndNodeGeometry {
			get {
				if (endNodeGeometry.Valid)
					return endNodeGeometry;
				else
					return null;
			}
		}

		/// <summary>
		/// Indicates that the managed segment is a one-way segment
		/// </summary>
		private bool oneWay = false;

		/// <summary>
		/// Indicates that the managed segment is a highway
		/// </summary>
		private bool highway = false;

		/// <summary>
		/// Indicates that the managed segment has a buslane
		/// </summary>
		private bool buslane = false;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="segmentId">id of the managed segment</param>
		public SegmentGeometry(ushort segmentId) {
			this.SegmentId = segmentId;
			startNodeGeometry = new SegmentEndGeometry(segmentId, true);
			endNodeGeometry = new SegmentEndGeometry(segmentId, false);
		}

		public ushort StartNodeId {
			get {
				ushort nodeId = 0;
				Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segId, ref NetSegment seg) {
					nodeId = seg.m_startNode;
					return true;
				});
				return nodeId;
			}
		}

		public ushort EndNodeId {
			get {
				ushort nodeId = 0;
				Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segId, ref NetSegment seg) {
					nodeId = seg.m_endNode;
					return true;
				});
				return nodeId;
			}
		}

		/// <summary>
		/// Lock object. Acquire this before accessing the HashSets.
		/// </summary>
		//public readonly object Lock = new object();

		/// <summary>
		/// Holds a list of observers which are being notified as soon as the managed segment's geometry is updated (but not neccessarily modified)
		/// </summary>
		//private List<IObserver<SegmentGeometry>> observers = new List<IObserver<SegmentGeometry>>();

		private bool valid = false;

		public override string ToString() {
			return $"[SegmentGeometry ({SegmentId})\n" +
				"\t" + $"IsValid() = {Valid}\n" +
				"\t" + $"oneWay = {oneWay}\n" +
				"\t" + $"highway = {highway}\n" +
				"\t" + $"buslane = {buslane}\n" +
				"\t" + $"StartNodeGeometry = {StartNodeGeometry}\n" +
				"\t" + $"EndNodeGeometry = {EndNodeGeometry}\n" +
				"SegmentGeometry]";
		}

		[Obsolete]
		public static bool IsValid(ushort segmentId) {
			return Constants.ServiceFactory.NetService.IsSegmentValid(segmentId);
		}

		public bool Valid {
			get {
				return IsValid(SegmentId);
			}
		}
		
		public void StartRecalculation(GeometryCalculationMode calcMode) {
			Recalculate(calcMode);
			//RecalculateLaneGeometries(calcMode);
		}

		public void Recalculate(GeometryCalculationMode calcMode) {
#if DEBUGGEO
			bool output = GlobalConfig.Instance.Debug.Switches[5];

			if (output)
				Log._Debug($">>> SegmentGeometry.Recalculate({calcMode}): called for segment {SegmentId}. Valid={Valid}, wasValid={valid}");
#endif

			if (!Valid) {
				if (valid) {
					valid = false;

					if (calcMode == GeometryCalculationMode.Propagate) {
						startNodeGeometry.Recalculate(GeometryCalculationMode.Propagate);
						endNodeGeometry.Recalculate(GeometryCalculationMode.Propagate);
					}

					Cleanup();
					Constants.ManagerFactory.GeometryManager.OnUpdateSegment(this);
					//NotifyObservers();
				}
				return;
			}

			valid = true;
			try {
#if DEBUGGEO
				if (output)
					Log._Debug($"Trying to get a lock for Recalculating geometries of segment {SegmentId}...");
#endif
				//Monitor.Enter(Lock);

#if DEBUGGEO
				if (output)
					Log.Info($"Recalculating geometries of segment {SegmentId} STARTED");
#endif

				Cleanup();

				oneWay = calculateIsOneWay(SegmentId);
				highway = calculateIsHighway(SegmentId);
				buslane = calculateHasBusLane(SegmentId);
				startNodeGeometry.Recalculate(calcMode);
				endNodeGeometry.Recalculate(calcMode);

#if DEBUGGEO
				if (output) {
					Log.Info($"Recalculating geometries of segment {SegmentId} FINISHED (flags={Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_flags})");
					SegmentEndGeometry[] endGeometries = new SegmentEndGeometry[] { startNodeGeometry, endNodeGeometry };
					Log._Debug($"seg. {SegmentId}. oneWay={oneWay}");
					Log._Debug($"seg. {SegmentId}. highway={highway}");

					int i = 0;
					foreach (SegmentEndGeometry endGeometry in endGeometries) {
						if (i == 0)
							Log._Debug("--- end @ start node ---");
						else
							Log._Debug("--- end @ end node ---");
						Log._Debug($"Node {endGeometry.NodeId} (flags={Singleton<NetManager>.instance.m_nodes.m_buffer[endGeometry.NodeId].m_flags})");

						Log._Debug($"seg. {SegmentId}. connectedSegments={ string.Join(", ", endGeometry.ConnectedSegments.Select(x => x.ToString()).ToArray())}");

						Log._Debug($"seg. {SegmentId}. leftSegments={ string.Join(", ", endGeometry.LeftSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. incomingLeftSegments={ string.Join(", ", endGeometry.IncomingLeftSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. outgoingLeftSegments={ string.Join(", ", endGeometry.OutgoingLeftSegments.Select(x => x.ToString()).ToArray())}");

						Log._Debug($"seg. {SegmentId}. rightSegments={ string.Join(", ", endGeometry.RightSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. incomingRightSegments={ string.Join(", ", endGeometry.IncomingRightSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. outgoingRightSegments={ string.Join(", ", endGeometry.OutgoingRightSegments.Select(x => x.ToString()).ToArray())}");

						Log._Debug($"seg. {SegmentId}. straightSegments={ string.Join(", ", endGeometry.StraightSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. incomingStraightSegments={ string.Join(", ", endGeometry.IncomingStraightSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. outgoingStraightSegments={ string.Join(", ", endGeometry.OutgoingStraightSegments.Select(x => x.ToString()).ToArray())}");

						Log._Debug($"seg. {SegmentId}. onlyHighways={endGeometry.OnlyHighways}");
						Log._Debug($"seg. {SegmentId}. outgoingOneWay={endGeometry.OutgoingOneWay}");
						
						++i;
					}
				}
#endif

#if DEBUGGEO
				//Log._Debug($"Recalculation of segment {SegmentId} completed. Valid? {IsValid()}");
#endif
				Constants.ManagerFactory.GeometryManager.OnUpdateSegment(this);
				//NotifyObservers();
			} finally {
#if DEBUGGEO
				if (output)
					Log._Debug($"Lock released after recalculating geometries of segment {SegmentId}");
#endif
				//Monitor.Exit(Lock);
			}
		}

		public ISegmentEndGeometry GetEnd(bool startNode) {
			if (! Valid) {
				return null;
			}

			if (startNode) {
				return StartNodeGeometry;
			} else {
				return EndNodeGeometry;
			}
		}

		public ISegmentEndGeometry GetEnd(ushort nodeId) {
			if (!Valid) {
				return null;
			}

			ushort startNodeId = StartNodeId;
			if (nodeId == startNodeId) {
				return StartNodeGeometry;
			} else {
				ushort endNodeId = EndNodeId;
				if (nodeId == endNodeId)
					return EndNodeGeometry;
			}
			return null;
		}

		/// <summary>
		/// Determines the node id at the given segment end.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort GetNodeId(bool startNode) {
			if (Valid)
				return 0;
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NodeId;
		}

		/// <summary>
		/// Determines all connected segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>		
		[Obsolete]
		public ushort[] GetConnectedSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.ConnectedSegments;
		}

		/// <summary>
		/// Determines all connected right segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>		
		[Obsolete]
		public ushort[] GetRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.RightSegments;
		}

		/// <summary>
		/// Determines all connected left segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>		
		[Obsolete]
		public ushort[] GetLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.LeftSegments;
		}

		/// <summary>
		/// Determines all connected straight segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>		
		[Obsolete]
		public ushort[] GetStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.StraightSegments;
		}

		/// <summary>
		/// Determines all incoming segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort[] GetIncomingSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.IncomingSegments;
		}

		/// <summary>
		/// Determines all incoming straight segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort[] GetIncomingStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.IncomingStraightSegments;
		}

		/// <summary>
		/// Determines all incoming left segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort[] GetIncomingLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.IncomingLeftSegments;
		}

		/// <summary>
		/// Determines all incoming right segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort[] GetIncomingRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.IncomingRightSegments;
		}

		/// <summary>
		/// Determines all outgoing segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort[] GetOutgoingSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingSegments;
		}

		/// <summary>
		/// Determines all outgoing straight segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort[] GetOutgoingStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingStraightSegments;
		}

		/// <summary>
		/// Determines all outgoing left segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort[] GetOutgoingLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingLeftSegments;
		}

		/// <summary>
		/// Determines all outgoing right segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		[Obsolete]
		public ushort[] GetOutgoingRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingRightSegments;
		}

		/// <summary>
		/// Determines the number of segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected segments at the given node</returns>
		[Obsolete]
		public int CountOtherSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumConnectedSegments;
		}

		/// <summary>
		/// Determines the number of left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected left segments at the given node</returns>
		[Obsolete]
		public int CountLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumLeftSegments;
		}

		/// <summary>
		/// Determines the number of right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected right segments at the given node</returns>
		[Obsolete]
		public int CountRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumRightSegments;
		}

		/// <summary>
		/// Determines the number of straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected straight segments at the given node</returns>
		[Obsolete]
		public int CountStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumStraightSegments;
		}

		/// <summary>
		/// Determines the number of incoming segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming left segments at the given node</returns>
		[Obsolete]
		public int CountIncomingSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumIncomingSegments;
		}

		/// <summary>
		/// Determines the number of incoming left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming left segments at the given node</returns>
		[Obsolete]
		public int CountIncomingLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumIncomingLeftSegments;
		}

		/// <summary>
		/// Determines the number of incoming right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming right segments at the given node</returns>
		[Obsolete]
		public int CountIncomingRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumIncomingRightSegments;
		}

		/// <summary>
		/// Determines the number of incoming straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming straight segments at the given node</returns>
		[Obsolete]
		public int CountIncomingStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumIncomingStraightSegments;
		}

		/// <summary>
		/// Determines the number of outgoing segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming left segments at the given node</returns>
		[Obsolete]
		public int CountOutgoingSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumOutgoingSegments;
		}

		/// <summary>
		/// Determines the number of outgoing left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected outgoing left segments at the given node</returns>
		[Obsolete]
		public int CountOutgoingLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumOutgoingLeftSegments;
		}

		/// <summary>
		/// Determines the number of outgoing right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected outgoing right segments at the given node</returns>
		[Obsolete]
		public int CountOutgoingRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumOutgoingRightSegments;
		}

		/// <summary>
		/// Determines the number of outgoing straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected outgoing straight segments at the given node</returns>
		[Obsolete]
		public int CountOutgoingStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumOutgoingStraightSegments;
		}

		/// <summary>
		/// Determines if the managed segment is connected to left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to left segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasLeftSegment(bool startNode) {
			return CountLeftSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to right segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasRightSegment(bool startNode) {
			return CountRightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to straight segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasStraightSegment(bool startNode) {
			return CountStraightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming left segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasIncomingLeftSegment(bool startNode) {
			return CountIncomingLeftSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming right segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasIncomingRightSegment(bool startNode) {
			return CountIncomingRightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming straight segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasIncomingStraightSegment(bool startNode) {
			return CountIncomingStraightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing left segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasOutgoingLeftSegment(bool startNode) {
			return CountOutgoingLeftSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing right segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasOutgoingRightSegment(bool startNode) {
			return CountOutgoingRightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing straight segments at the given node, else false.</returns>
		[Obsolete]
		public bool HasOutgoingStraightSegment(bool startNode) {
			return CountOutgoingStraightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a left segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be left, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the left-hand side of the managed segment at the given node</returns>
		[Obsolete]
		public bool IsLeftSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;

			bool contains = false;
			foreach (ushort segId in endGeometry.LeftSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a right segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be right, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the right-hand side of the managed segment at the given node</returns>
		[Obsolete]
		public bool IsRightSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.IsRightSegment(toSegmentId);
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a straight segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be straight, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected straight-wise to the managed segment at the given node</returns>
		[Obsolete]
		public bool IsStraightSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			
			bool contains = false;
			foreach (ushort segId in endGeometry.StraightSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a left segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be left, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the left-hand side of the managed segment at the given node</returns>
		[Obsolete]
		public bool IsIncomingLeftSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;

			bool contains = false;
			foreach (ushort segId in endGeometry.IncomingLeftSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a right segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be right, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the right-hand side of the managed segment at the given node</returns>
		[Obsolete]
		public bool IsIncomingRightSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			
			bool contains = false;
			foreach (ushort segId in endGeometry.IncomingRightSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a straight segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be straight, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected straight-wise to the managed segment at the given node</returns>
		[Obsolete]
		public bool IsIncomingStraightSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			
			bool contains = false;
			foreach (ushort segId in endGeometry.IncomingStraightSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is only connected to highways at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <returns>true, if, according to the stored geometry data, the managed segment is only connected to highways at the given node, false otherwise</returns>
		[Obsolete]
		public bool HasOnlyHighways(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OnlyHighways;
		}
		
		public bool OneWay {
			get {
				return oneWay;
			}
		}

		public bool Highway {
			get {
				return highway;
			}
		}

		public bool BusLane {
			get {
				return buslane;
			}
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an outgoing one-way road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an outgoing one-way road at the given node, false otherwise</returns>
		[Obsolete]
		public bool IsOutgoingOneWay(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingOneWay;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an incoming one-way road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an incoming one-way road at the given node, false otherwise</returns>
		[Obsolete]
		public bool IsIncomingOneWay(bool startNode) {
			return (OneWay && !IsOutgoingOneWay(startNode));
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an incoming road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an incoming road at the given node, false otherwise</returns>
		[Obsolete]
		public bool IsIncoming(bool startNode) {
			return !IsOutgoingOneWay(startNode);
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an outgoing road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an outgoing road at the given node, false otherwise</returns>
		[Obsolete]
		public bool IsOutgoing(bool startNode) {
			return !IsIncomingOneWay(startNode);
		}

		/// <summary>
		/// Determines the relative direction of the other segment relatively to the managed segment at the given node, according to the stored geometry information.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="otherSegmentId">other segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>relative direction of the other segment relatively to the managed segment at the given node</returns>
		[Obsolete]
		public ArrowDirection GetDirection(ushort otherSegmentId, bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;

			return endGeometry.GetDirection(otherSegmentId);
		}

		/// <summary>
		/// Determines if highway merging/splitting rules are activated at the managed segment for the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode"></param>
		/// <returns></returns>
		[Obsolete]
		public bool AreHighwayRulesEnabled(bool startNode) {
			if (!Options.highwayRules)
				return false;
			if (!IsIncomingOneWay(startNode))
				return false;
			if (!(Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].Info.m_netAI is RoadBaseAI))
				return false;
			if (!((RoadBaseAI)Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].Info.m_netAI).m_highwayRules)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;

			if (endGeometry.NumConnectedSegments <= 1)
				return false;

			bool nextAreOnlyOneWayHighways = true;
			foreach (ushort otherSegmentId in endGeometry.ConnectedSegments) {
				if (Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.m_netAI is RoadBaseAI) {
					if (! SegmentGeometry.Get(otherSegmentId).OneWay || !((RoadBaseAI)Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.m_netAI).m_highwayRules) {
						nextAreOnlyOneWayHighways = false;
						break;
					}
				} else {
					nextAreOnlyOneWayHighways = false;
					break;
				}
			}

			return nextAreOnlyOneWayHighways;
		}

		/// <summary>
		/// Calculates if the given segment is an outgoing one-way road at the given node.
		/// </summary>
		/// <param name="segmentId">segment to check</param>
		/// <param name="nodeId">node the given segment shall be checked at</param>
		/// <returns>true, if the given segment is an outgoing one-way road at the given node, false otherwise</returns>
		[Obsolete]
		internal static bool calculateIsOutgoingOneWay(ushort segmentId, ushort nodeId) { // TODO move to SegmentEnd
			if (!IsValid(segmentId))
				return false;

			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			NetInfo.Direction dir = Constants.ServiceFactory.NetService.GetFinalSegmentEndDirection(segmentId, ref instance.m_segments.m_buffer[segmentId], instance.m_segments.m_buffer[segmentId].m_startNode == nodeId);

			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				bool validLane = (info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
				// TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check

				if (validLane) {
					if ((info.m_lanes[laneIndex].m_finalDirection & dir) != NetInfo.Direction.None) {
						return false;
					}
				}

				laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
				laneIndex++;
			}

			return true;
		}

		/// <summary>
		/// Calculates if the given segment is a one-way road.
		/// </summary>
		/// <returns>true, if the managed segment is a one-way road, false otherwise</returns>
		private static bool calculateIsOneWay(ushort segmentId) {
			if (!IsValid(segmentId))
				return false;

			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			var hasForward = false;
			var hasBackward = false;

			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				bool validLane = (info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
				// TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check

				if (validLane) {
					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
						hasForward = true;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						hasBackward = true;
					}

					if (hasForward && hasBackward) {
						return false;
					}
				}

				laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
				laneIndex++;
			}

			return true;
		}

		/// <summary>
		/// Calculates if the given segment has a buslane.
		/// </summary>
		/// <param name="segmentId">segment to check</param>
		/// <returns>true, if the given segment has a buslane, false otherwise</returns>
		private static bool calculateHasBusLane(ushort segmentId) {
			if (!IsValid(segmentId))
				return false;

			bool ret = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				ret = calculateHasBusLane(segment.Info);
				return true;
			});
			return ret;
		}

		/// <summary>
		/// Calculates if the given segment info describes a segment having a bus lane
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <returns></returns>
		public static bool calculateHasBusLane(NetInfo segmentInfo) {
			for (int laneIndex = 0; laneIndex < segmentInfo.m_lanes.Length; ++laneIndex) {
				if (segmentInfo.m_lanes[laneIndex].m_laneType == NetInfo.LaneType.TransportVehicle &&
					(segmentInfo.m_lanes[laneIndex].m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
					return true;
				}
			}

			return false;
		}

		// TODO move to SegmentEndGeometry
		[Obsolete]
		public static void calculateOneWayAtNode(ushort segmentId, ushort nodeId, out bool isOneway, out bool isOutgoingOneWay) {
			if (!IsValid(segmentId)) {
				isOneway = false;
				isOutgoingOneWay = false;
				return;
			}

			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			var dir = NetInfo.Direction.Forward;
			if (instance.m_segments.m_buffer[segmentId].m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);

			var hasForward = false;
			var hasBackward = false;
			isOutgoingOneWay = true;
			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				bool validLane = (info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
				// TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check

				if (validLane) {
					if ((info.m_lanes[laneIndex].m_finalDirection & dir2) != NetInfo.Direction.None) {
						isOutgoingOneWay = false;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
						hasForward = true;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						hasBackward = true;
					}
				}

				laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
				laneIndex++;
			}

			isOneway = !(hasForward && hasBackward);
			if (!isOneway)
				isOutgoingOneWay = false;
		}

		/// <summary>
		/// Calculates if the given segment is a highway
		/// </summary>
		/// <param name="segmentId"></param>
		/// <returns></returns>
		public static bool calculateIsHighway(ushort segmentId) {
			if (!IsValid(segmentId))
				return false;

			bool ret = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				ret = calculateIsHighway(segment.Info);
				return true;
			});
			return ret;
		}

		/// <summary>
		/// Calculates if the given segment info describes a highway segment
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <returns></returns>
		public static bool calculateIsHighway(NetInfo segmentInfo) {
			if (segmentInfo.m_netAI is RoadBaseAI)
				return ((RoadBaseAI)segmentInfo.m_netAI).m_highwayRules;
			return false;
		}

		/// <summary>
		/// Clears the segment geometry data.
		/// </summary>
		private void Cleanup() {
			highway = false;
			oneWay = false;
			buslane = false;

			try {
				//Monitor.Enter(Lock);

				startNodeGeometry.Cleanup();
				endNodeGeometry.Cleanup();

				// reset highway lane arrows
				//Flags.removeHighwayLaneArrowFlagsAtSegment(SegmentId); // TODO refactor

				// clear default vehicle type cache
				VehicleRestrictionsManager.Instance.ClearCache(SegmentId);
			} finally {
				//Monitor.Exit(Lock);
			}
		}

		public bool Equals(SegmentGeometry otherSegGeo) {
			if (otherSegGeo == null) {
				return false;
			}
			return SegmentId == otherSegGeo.SegmentId;
		}

		public override bool Equals(object other) {
			if (other == null) {
				return false;
			}
			if (!(other is SegmentGeometry)) {
				return false;
			}
			return Equals((SegmentGeometry)other);
		}

		public override int GetHashCode() {
			int prime = 31;
			int result = 1;
			result = prime * result + SegmentId.GetHashCode();
			return result;
		}

		// static methods

		static SegmentGeometry() {
			segmentGeometries = new SegmentGeometry[NetManager.MAX_SEGMENT_COUNT];
		}

		internal static void OnBeforeLoadData() {
			Log._Debug($"Building {segmentGeometries.Length} segment geometries...");
			for (int i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i] = new SegmentGeometry((ushort)i);
			}
			for (int i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i].Recalculate(GeometryCalculationMode.Init);
			}
			/*for (int i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i].RecalculateLaneGeometries(GeometryCalculationMode.Init);
			}*/
			Log._Debug($"Calculated segment geometries.");
		}

		internal static void OnBeforeSaveData() {
			/*Log._Debug($"Recalculating all segment geometries...");
			for (int i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i].Recalculate(false);
			}
			Log._Debug($"Calculated segment geometries.");*/
		}

		public static SegmentGeometry Get(ushort segmentId, bool ignoreInvalid=false) {
			if (segmentGeometries == null) {
				return null;
			}

			SegmentGeometry segGeo = segmentGeometries[segmentId];
			if (segGeo == null || (! ignoreInvalid && ! segGeo.valid)) {
				return null;
			}
			return segGeo;
		}
	}
}
