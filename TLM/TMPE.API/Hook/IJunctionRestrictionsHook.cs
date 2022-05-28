using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.API.Hook {
    public interface IJunctionRestrictionsHook {

        /// <summary>
        /// An event that allows a handler to modify the results of a <c>GetDefault<i>TrafficRule</i></c> method.
        /// </summary>
        event Action<FlagsHookArgs> GetDefaultsHook;

        /// <summary>
        /// An event that allows a handler to modify the results of an <c>Is<i>TrafficRule</i>Configurable</c> method.
        /// </summary>
        event Action<FlagsHookArgs> GetConfigurableHook;

        /// <summary>
        /// Schedules the specified flags for recalculation.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        /// <param name="flags">Specifies which flags to invalidate.</param>
        public void InvalidateFlags(ushort segmentId, bool startNode, JunctionRestrictionFlags flags);

        /// <summary>
        /// Schedles the specified flags for recalculation for all segment ends on the specified node.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="flags">Specifies which flags to invalidate.</param>
        public void InvalidateFlags(ushort nodeId, JunctionRestrictionFlags flags);

        public class FlagsHookArgs {

            /// <summary>
            /// Identifies the segment for which flag data is being returned.
            /// </summary>
            public ushort SegmentId { get; private set; }

            /// <summary>
            /// Identifies the node on the segment for which flag data is being returned.
            /// </summary>
            public bool StartNode { get; private set; }

            /// <summary>
            /// Identifies which flags are being returned. Unnecessary computation may be avoided
            /// by examining this mask to see which flags are being requested.
            /// </summary>
            public JunctionRestrictionFlags Mask { get; private set; }

            /// <summary>
            /// The flag return values. Changes to this property alter the outcome of the underlying operation.
            /// Any flags that not set in the <see cref="Mask"/> property are ignored.
            /// </summary>
            public JunctionRestrictionFlags Result { get; set; }

            public FlagsHookArgs(ushort segmentId, bool startNode, JunctionRestrictionFlags mask, JunctionRestrictionFlags result) {
                SegmentId = segmentId;
                StartNode = startNode;
                Mask = mask;
                Result = result;
            }
        }
    }
}
