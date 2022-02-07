namespace TrafficManager.API.Manager {
    /// <summary>
    /// Manages mod options
    /// </summary>
    public interface IOptionsManager : ICustomDataManager<byte[]> {
        /// <summary>
        /// Determines if modifications to segments may be published in the current state.
        /// </summary>
        /// <returns>true if changes may be published, false otherwise</returns>
        bool MayPublishSegmentChanges();

        /// <summary>
        /// Determine if TM:PE mod options in <see cref="Options"/> are safe to query.
        /// </summary>
        /// <returns>Returns <c>true</c> if safe to query, otherwise <c>false</c>.</returns>
        /// <remarks>Options are only safe to query in editor/game, except while loading/saving.</remarks>
        public bool OptionsAreSafeToQuery();

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