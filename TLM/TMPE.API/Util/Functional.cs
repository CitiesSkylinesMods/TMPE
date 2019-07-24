namespace TrafficManager.Util {
    /// <summary>
    /// Represents a void return type which may also carry an error.
    /// </summary>
    /// <typeparam name="VALUE">Value type on success</typeparam>
    /// <typeparam name="ERROR">Error type if failed</typeparam>
    public class VoidResult<ERROR> {
        private bool isOk_;
        private ERROR error_;

        public VoidResult() {
            isOk_ = true;
        }

        public VoidResult(ERROR error) {
            isOk_ = false;
            error_ = error;
        }

        public bool IsOk => isOk_;

        public bool IsError => !isOk_;

        public ERROR Error => error_;
    }

    /// <summary>
    /// Represents a return type which may carry an error instead.
    /// </summary>
    /// <typeparam name="VALUE">Value type on success</typeparam>
    /// <typeparam name="ERROR">Error type if failed</typeparam>
    public class Result<VALUE, ERROR> {
        private readonly bool isOk_;

        private VALUE value_ { get; }

        public ERROR Error { get; }

        public Result(VALUE value) {
            isOk_ = true;
            value_ = value;
        }

        public Result(ERROR error) {
            isOk_ = false;
            Error = error;
        }

        public bool IsOk => isOk_;

        public bool IsError => !isOk_;
   }
}