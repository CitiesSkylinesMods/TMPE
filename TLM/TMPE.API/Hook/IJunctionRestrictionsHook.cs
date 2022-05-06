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
        event Action<FlagsHookArgs> GetDefaults;

        /// <summary>
        /// An event that allows a handler to modify the results of an <c>Is<i>TrafficRule</i>Configurable</c> method.
        /// </summary>
        event Action<FlagsHookArgs> GetConfigurable;

        /// <summary>
        /// Invalidates the specified flags so that they will be recalculated.
        /// Recalculation is not guaranteed to happen immediately, but is guaranteed to happen
        /// before their next use.
        /// </summary>
        /// <param name="flags"></param>
        public void InvalidateFlags(JunctionRestrictionFlags flags);

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
            /// </summary>
            public JunctionRestrictionFlags Result { get; set; }

            internal FlagsHookArgs(ushort segmentId, bool startNode, JunctionRestrictionFlags mask, JunctionRestrictionFlags result) {
                SegmentId = segmentId;
                StartNode = startNode;
                Mask = mask;
                Result = result;
            }
        }
    }
}
