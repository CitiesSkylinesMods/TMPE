namespace TrafficManager.API.Util {
    using JetBrains.Annotations;
    using UnityEngine;

    /// <summary>
    /// Represents a void return type which may also carry an error.
    /// Use `return default;` to return OK.
    /// </summary>
    /// <typeparam name="VALUE">Value type on success.</typeparam>
    /// <typeparam name="TError">Error type if failed.</typeparam>
    [UsedImplicitly]
    public class VoidResult<TError> {
        private bool isOk_;
        private TError error_;

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

        private TError error_ { get; }

        public Result(TValue value) {
            isOk_ = true;
            value_ = value;
        }

        public Result(TError error) {
            isOk_ = false;
            error_ = error;
        }

        public bool IsOk => isOk_;

        public bool IsError => !isOk_;

        /// <summary>
        /// On success, retrieve the value
        /// </summary>
        public TValue Value {
            get {
                Debug.Assert(IsOk);
                return value_;
            }
        }

        /// <summary>
        /// On error retrieve the error
        /// </summary>
        public TError Error {
            get {
                Debug.Assert(IsError);
                return error_;
            }
        }
    }
}