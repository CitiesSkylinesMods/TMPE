namespace TrafficManager.Util.Caching {
    using CSUtil.Commons;

    /// <summary>
    /// Caches something in an array (of fixed size). The array can be reset.
    /// <typeparam name="TValue">Type stored in the array</typeparam>
    /// </summary>
    /// <typeparam name="TValue">The value type to be cached.</typeparam>
    public class GenericArrayCache<TValue> {
        public readonly TValue[] Values;
        public int Size;
        private readonly int maxSize_;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericArrayCache{TValue}"/> class.
        /// </summary>
        /// <param name="size">Size of the array to create.</param>
        public GenericArrayCache(int size) {
            maxSize_ = size;
            Values = new TValue[size];
            Size = 0;
        }

        public void Add(TValue value) {
            Log._DebugIf(
                Size >= maxSize_,
                () => $"Adding {value} to GenericArrayCache over the capacity {maxSize_}");
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