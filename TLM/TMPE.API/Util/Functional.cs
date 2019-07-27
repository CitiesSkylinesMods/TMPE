namespace TrafficManager.Util {
    using JetBrains.Annotations;

    /// <summary>
    /// Represents a void return type which may also carry an error.
    /// </summary>
    /// <typeparam name="VALUE">Value type on success</typeparam>
    /// <typeparam name="TError">Error type if failed</typeparam>
    [UsedImplicitly]
    public class VoidResult<TError> {
        private bool isOk_;
        private TError error_;

        public VoidResult() {
            isOk_ = true;
        }

        public VoidResult(TError error) {
            isOk_ = false;
            error_ = error;
        }

        public bool IsOk => isOk_;

        public bool IsError => !isOk_;

        public TError Error => error_;
    }

    /// <summary>
    /// Represents a return type which may carry an error instead.
    /// </summary>
    /// <typeparam name="TValue">Value type on success</typeparam>
    /// <typeparam name="TError">Error type if failed</typeparam>
    [UsedImplicitly]
    public class Result<TValue, TError> {
        private readonly bool isOk_;

        private TValue value_ { get; }

        public TError Error { get; }

        public Result(TValue value) {
            isOk_ = true;
            value_ = value;
        }

        public Result(TError error) {
            isOk_ = false;
            Error = error;
        }

        public bool IsOk => isOk_;

        public bool IsError => !isOk_;
   }
}