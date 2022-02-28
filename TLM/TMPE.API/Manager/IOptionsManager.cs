namespace TrafficManager.API.Manager {
    using System;

    /// <summary>
    /// Manages mod options
    /// </summary>
    public interface IOptionsManager : ICustomDataManager<byte[]> {
        /// <summary>
        /// Determines if modifications to segments may be published in the current state.
        /// </summary>
        /// <returns>true if changes may be published, false otherwise</returns>
        [Obsolete("Use TMPELifecycle method of same name instead")]
        bool MayPublishSegmentChanges();

        /// <summary>
        /// Get current value of TMPE mod option from <see cref="Options"/>.
        /// </summary>
        /// <typeparam name="TVal">Option type, eg. <c>bool</c>.</typeparam>
        /// <param name="optionName">Name of the option in <see cref="Options"/>.</param>
        /// <param name="value">The option value, if found, otherwise <c>default</c> for <typeparamref name="TVal"/>.</param>
        /// <returns>Returns <c>true</c> if successful, or <c>false</c> if there was a problem (eg. option not found, wrong TVal, etc).</returns>
        bool TryGetOptionByName<TVal>(string optionName, out TVal value);
    }
}