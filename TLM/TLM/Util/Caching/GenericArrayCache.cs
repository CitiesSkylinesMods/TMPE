namespace TrafficManager.Util.Caching {
    using CSUtil.Commons;

    /// <summary>
    /// Caches something in an array (of fixed size). The array can be reset.
    /// <typeparam name="TValue">Type stored in the array</typeparam>
    /// </summary>
    public class GenericArrayCache<TValue> {
        public TValue[] Values;
        public int Size;
        private readonly int MaxSize;

        public GenericArrayCache(int size) {
            MaxSize = size;
            Values = new TValue[size];
            Size = 0;
        }

        public void Add(TValue value) {
            Log._DebugIf(
                Size >= MaxSize,
                     () => $"Adding {value} to GenericArrayCache over the capacity {MaxSize}");
            Values[Size] = value;
            Size++;
        }

        /// <summary>
        /// Resets the array to begin anew
        /// </summary>
        public void Clear() {
            Size = 0;
        }
    }
}