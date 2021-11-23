using BenchmarkDotNet.Attributes;

namespace Benchmarks {
    public class IsValidPerfTests {
        [GlobalSetup]
        public void Setup() {
            _ = MyNetSegmentService.Instance;
        }

        [Benchmark]
        public void IdToSegmentWithIsValidExtension() {
            for (ushort i = 0; i < MyNetManager.Instance.m_segments.m_buffer.Length; i++) {
                ref NetSegment netSegment = ref i.ToSegment();
                if (!netSegment.IsValid()) {
                    continue;
                }
            }
        }

        [Benchmark]
        public void IsValidOnServiceInstanceWithIdAsParameter() {
            for (ushort i = 0; i < MyNetManager.Instance.m_segments.m_buffer.Length; i++) {
                if (!MyNetSegmentService.Instance.IsValid(i)) {
                    continue;
                }
            }
        }
    }

    internal class MyNetSegmentService {
        private readonly NetSegment[] _buffer;

        static MyNetSegmentService() {
            Instance = new MyNetSegmentService();
        }

        private MyNetSegmentService() {
            _buffer = MyNetManager.Instance.m_segments.m_buffer;
        }

        internal static MyNetSegmentService Instance { get; }

        internal bool IsValid(ushort netSegmentId) {
            var createdCollapsedDeleted = _buffer[netSegmentId].m_flags
                & (NetSegment.Flags.Created | NetSegment.Flags.Collapsed | NetSegment.Flags.Deleted);

            return createdCollapsedDeleted == NetSegment.Flags.Created;
        }
    }

    internal class MyNetManager {
        static MyNetManager() {
            Instance = new MyNetManager();
        }

        internal MyNetManager() {
            m_segments = new Array16<NetSegment>(36864u);
        }

        internal static MyNetManager Instance { get; }

        internal Array16<NetSegment> m_segments;
    }

    internal static class Shortcuts {
        private static NetSegment[] _segBuffer = MyNetManager.Instance.m_segments.m_buffer;

        internal static ref NetSegment ToSegment(this ushort segmentId) => ref _segBuffer[segmentId];
    }

    internal static class NetSegmentExtensions {
        /// <summary>
        /// Checks if the netSegment is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="netSegment">netSegment</param>
        /// <returns>True if the netSegment is valid, otherwise false.</returns>
        internal static bool IsValid(this ref NetSegment netSegment) =>
            netSegment.m_flags.CheckFlags(
                required: NetSegment.Flags.Created,
                forbidden: NetSegment.Flags.Collapsed | NetSegment.Flags.Deleted);
    }

    internal static class FlagExtensions {
        internal static bool CheckFlags(this NetSegment.Flags value, NetSegment.Flags required, NetSegment.Flags forbidden = 0) =>
            (value & (required | forbidden)) == required;
    }
}
