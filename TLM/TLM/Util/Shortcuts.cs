namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using ColossalFramework.UI;
    using System.Diagnostics;
    using ColossalFramework.Threading;

    internal static class Shortcuts {
        internal static bool InSimulationThread() =>
            System.Threading.Thread.CurrentThread == SimulationManager.instance.m_simulationThread;

        internal static bool IsMainThread() => Dispatcher.currentSafe == Dispatcher.mainSafe;

        /// <summary>
        /// returns a new calling Clone() on all items.
        /// </summary>
        /// <typeparam name="T">item time must be IClonable</typeparam>
        internal static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable =>
            listToClone.Select(item => (T)item.Clone()).ToList();

        internal static void Swap<T>(ref T a, ref T b) {
            T temp = a;
            a = b;
            b = temp;
        }

        internal static void Swap<T>(this T[] array, int index1, int index2) {
            T temp = array[index1];
            array[index1] = array[index2];
            array[index2] = temp;
        }

        internal static void Swap<T>(this List<T> list, int index1, int index2) {
            T temp = list[index1];
            list[index1] = list[index2];
            list[index2] = temp;
        }
        private static ExtSegmentEnd[] _segEndBuff => segEndMan.ExtSegmentEnds;

        internal static IExtSegmentEndManager segEndMan => Constants.ManagerFactory.ExtSegmentEndManager;

        internal static IExtSegmentManager segMan => Constants.ManagerFactory.ExtSegmentManager;

        [Obsolete]
        internal static bool IsUndergroundNode(this ushort node) => node.ToNode().IsUnderground();

        internal static ref ExtSegmentEnd GetSegEnd(ushort segmentId, ushort nodeId) =>
            ref _segEndBuff[segEndMan.GetIndex(segmentId, nodeId)];

        internal static ref ExtSegmentEnd GetSegEnd(ushort segmentId, bool startNode) =>
            ref _segEndBuff[segEndMan.GetIndex(segmentId, startNode)];

        [Obsolete]
        internal static bool HasJunctionFlag(ushort nodeId) => nodeId.ToNode().IsJunction();


        internal static Func<bool, int> Int = (bool b) => b ? 1 : 0;

        /// <summary>
        /// useful for easily debugging inline functions
        /// to be used like this example:
        /// TYPE inlinefunctionname(...) => expression
        /// TYPE inlinefunctionname(...) => expression.LogRet("message");
        /// </summary>
        internal static T LogRet<T>(this T a, string m) {
            Log._Debug(m + a);
            return a;
        }

#if DEBUG
        private static int[] _frameCounts=  new int[100];
        internal static void LogAndWait(string m, int waitFrames, ushort ID) {
            int frameCount = Time.frameCount;
            int diff = frameCount - _frameCounts[ID];
            if (diff<0 || diff > waitFrames) {
                Log._Debug(m);
                _frameCounts[ID] = frameCount;
            }
        }
#else
        internal static void LogAndWait(string m, ushort ID) {

        }
#endif
        internal static string CenterString(this string stringToCenter, int totalLength) {
            int leftPadding = ((totalLength - stringToCenter.Length) / 2) + stringToCenter.Length;
            return stringToCenter.PadLeft(leftPadding).PadRight(totalLength);
        }

        /// <summary>
        /// Creates and string of all items with enumerable inpute as {item1, item2, item3}
        /// null argument returns "Null".
        /// </summary>
        internal static string ToSTR<T>(this IEnumerable<T> enumerable) {
            if (enumerable == null)
                return "Null";
            string ret = "{ ";
            foreach (T item in enumerable) {
                ret += $"{item}, ";
            }
            ret.Remove(ret.Length - 2, 2);
            ret += " }";
            return ret;
        }

        internal static string ToSTR(this List<LanePos> laneList) =>
            (from lanePos in laneList select lanePos.laneId).ToSTR();

        internal static void AssertEq<T>(T a, T b, string m = "") where T : IComparable<T> {
            if (a.CompareTo(b) != 0) {
                Log.Error($"Assertion failed. Expected {a} == {b} | " + m);
            }
        }

        internal static void AssertNEq<T>(T a, T b, string m = "") where T : IComparable<T> {
            if (a.CompareTo(b) == 0) {
                Log.Error($"Assertion failed. Expected {a} != {b} | " + m);
            }
        }

        internal static void AssertNotNull(object obj, string m = "") {
            if (obj == null) {
                Log.Error("Assertion failed. Expected not null: " + m);
            }
        }

        /// <summary>
        /// Designed for testing enum/flag parameter values in DEBUG builds.
        /// Logs error if <paramref name="value"/> is <c>None</c>, ie. <c>(int)value == 0</c>.
        /// </summary>
        /// <typeparam name="T">Must be some kind of <see cref="Enum"/>.</typeparam>
        /// <param name="value">Value to test.</param>
        /// <param name="m">Name of the value or other informative message.</param>
        /// <example>AssertNotZero((int)laneType);</example>
        /// <remarks>Conditional("DEBUG") - no performance impact on release builds.</remarks>
        /// <exception cref="ArgumentException">
        /// Thrown if <typeparamref name="T"/> is not some kind of <see cref="Enum"/>.
        /// </exception>
        internal static void AssertNotNone<T>(T value, string m = "") where T: Enum {
            if (!typeof(T).IsEnum)
                throw new ArgumentException($"Type '{typeof(T).FullName}' is not an enum");

            T none = default;

            if (value.Equals(none))
                Log.Error("Assertion failed. Enum value must not be None: " + m);
        }

        internal static void Assert(bool con, string m = "") {
            if (!con) {
                Log.Error("Assertion failed: " + m);
            }
        }

        internal static void LogException(this Exception ex, bool showInPanel = false) {
            if (ex is null) {
                Log.Error("null argument ex was passed to Log.Exception()");
                return;
            }
            try {
                Log.Error(ex + "\n\t===================="); // stack trace is printed after this.
                UnityEngine.Debug.LogException(ex);
                if (showInPanel)
                    UIView.ForwardException(ex);
            } catch (Exception ex2) {
                Log.Error(ex2.ToString());
            }
        }

        internal static Color WithAlpha(this Color color, float alpha) {
            color.a = alpha;
            return color;
        }

        internal static bool ShiftIsPressed => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        internal static bool ControlIsPressed => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        internal static bool AltIsPressed => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        #region directions
        internal static bool LHT => Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
        internal static bool RHT => !LHT;

        internal static LaneArrows LaneArrows_Near => RHT ? LaneArrows.Right : LaneArrows.Left;
        internal static LaneArrows LaneArrows_Far  => RHT ? LaneArrows.Left  : LaneArrows.Right;
        internal static LaneArrows LaneArrows_NearForward => LaneArrows_Near | LaneArrows.Forward;
        internal static LaneArrows LaneArrows_FarForward  => LaneArrows_Far  | LaneArrows.Forward;

        internal static ArrowDirection ArrowDirection_Near => RHT ? ArrowDirection.Right : ArrowDirection.Left;
        internal static ArrowDirection ArrowDirection_Far  => RHT ? ArrowDirection.Left  : ArrowDirection.Right;

        internal static ushort GetNearSegment(this ref NetSegment segment, ushort nodeId) =>
            RHT ? segment.GetRightSegment(nodeId) : segment.GetLeftSegment(nodeId);

        internal static ushort GetFarSegment(this ref NetSegment segment, ushort nodeId) =>
            LHT ? segment.GetRightSegment(nodeId) : segment.GetLeftSegment(nodeId);
        #endregion

        /// <summary>
        /// Creates copy of bezier at designated height
        /// </summary>
        /// <param name="bezier">source bezier</param>
        /// <param name="height">height</param>
        /// <returns></returns>
        internal static Bezier3 ForceHeight(this Bezier3 bezier, float height) {
            bezier.a.y = height;
            bezier.b.y = height;
            bezier.c.y = height;
            bezier.d.y = height;
            return bezier;
        }

        /// <summary>
        /// Creates copy of Vector3 with new y
        /// </summary>
        /// <param name="vector">source vector</param>
        /// <param name="newY">new y value</param>
        /// <returns></returns>
        internal static Vector3 ChangeY(this Vector3 vector, float newY) {
            vector.y = newY;
            return vector;
        }
    }
}
