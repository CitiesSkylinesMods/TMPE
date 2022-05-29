namespace TrafficManager.API.Manager {
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Traffic.Enums;

    public interface IJunctionRestrictionsManager {

        /// <summary>
        /// Indicates whether the specified flag is configurable.
        /// If more than one flag is specified, indicates whether all are configurable.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        bool IsConfigurable(ushort segmentId, bool startNode, JunctionRestrictionsFlags flags);

        /// <summary>
        /// Returns the set value (not the default) for the specified flag, or <c>null</c> if no value has been set.
        /// If more than one flag is specified, <c>null</c> is returned if they do not all have the same value.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        bool? GetValue(ushort segmentId, bool startNode, JunctionRestrictionsFlags flags);

        /// <summary>
        /// Returns the set value of the specified flag, or the default value if no value is set.
        /// If more than one flag is specified, returns the logical AND of all results.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        bool GetValueOrDefault(ushort segmentId, bool startNode, JunctionRestrictionsFlags flags);

        /// <summary>
        /// Returns the default value for the specified flag.
        /// If more than one flag is specified, returns the logical AND of all the results.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        bool GetDefaultValue(ushort segmentId, bool startNode, JunctionRestrictionsFlags flags);

        /// <summary>
        /// Sets the value of the specified flag(s). If null, clears the set value so that the default will be used.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        /// <param name="flags"></param>
        /// <param name="value"></param>
        /// <returns>true if the operation was successful, false if not configurable</returns>
        bool SetValue(ushort segmentId, bool startNode, JunctionRestrictionsFlags flags, bool? value);

        /// <summary>
        /// Toggles the value of the specified flag. This method fails if more than one flag is specified.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        /// <param name="flags"></param>
        /// <returns>true if the operation was successful, false if not configurable or if more than one flag was specified</returns>
        bool ToggleValue(ushort segmentId, bool startNode, JunctionRestrictionsFlags flags);

        #region Is<Traffic Rule>Configurable

        /// <summary>
        /// This API is dprecated. Please use <see cref="IsConfigurable(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines if u-turn behavior may be controlled at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if u-turns may be customized, <code>false</code> otherwise</returns>
        [Obsolete("Please use IsConfigurable(ushort, bool, JunctionRestrictionFlags)")]
        bool IsUturnAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="IsConfigurable(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines if turn-on-red behavior is enabled for near turns and may be controlled at
        ///     the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if turn-on-red may be customized for near turns,
        ///     <code>false</code> otherwise</returns>
        [Obsolete("Please use IsConfigurable(ushort, bool, JunctionRestrictionFlags)")]
        bool IsNearTurnOnRedAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="IsConfigurable(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines if turn-on-red behavior is enabled for far turns and may be controlled at
        ///     the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if turn-on-red may be customized for far turns,
        ///     <code>false</code> otherwise</returns>
        [Obsolete("Please use IsConfigurable(ushort, bool, JunctionRestrictionFlags)")]
        bool IsFarTurnOnRedAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="IsConfigurable(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines if turn-on-red behavior is enabled for the given turn type and that it may
        ///     be controlled at the given segment end.
        /// </summary>
        /// <param name="near">for near turns?</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if turn-on-red may be customized, <code>false</code> otherwise</returns>
        [Obsolete("Please use IsConfigurable(ushort, bool, JunctionRestrictionFlags)")]
        bool IsTurnOnRedAllowedConfigurable(bool near,
                                            ushort segmentId,
                                            bool startNode,
                                            ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="IsConfigurable(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines if lane changing behavior may be controlled at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if lane changing may be customized, <code>false</code> otherwise</returns>
        [Obsolete("Please use IsConfigurable(ushort, bool, JunctionRestrictionFlags)")]
        bool IsLaneChangingAllowedWhenGoingStraightConfigurable(
            ushort segmentId,
            bool startNode,
            ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="IsConfigurable(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines if entering blocked junctions may be controlled at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if entering blocked junctions may be customized,
        ///     <code>false</code> otherwise</returns>
        [Obsolete("Please use IsConfigurable(ushort, bool, JunctionRestrictionFlags)")]
        bool IsEnteringBlockedJunctionAllowedConfigurable(ushort segmentId,
                                                          bool startNode,
                                                          ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="IsConfigurable(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines if pedestrian crossings may be controlled at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if pedestrian crossings may be customized, <code>false</code>
        ///     otherwise</returns>
        [Obsolete("Please use IsConfigurable(ushort, bool, JunctionRestrictionFlags)")]
        bool IsPedestrianCrossingAllowedConfigurable(ushort segmentId,
                                                     bool startNode,
                                                     ref NetNode node);
        #endregion

        #region GetDefault<Traffic Rule>

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetDefaultValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines the default setting for u-turns at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if u-turns are allowed by default, <code>false</code>
        ///     otherwise</returns>
        [Obsolete("Please use GetDefaultValue(ushort, bool, JunctionRestrictionFlags)")]
        bool GetDefaultUturnAllowed(ushort segmentId, bool startNode, ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetDefaultValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines the default setting for near turn-on-red at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if turn-on-red is allowed for near turns by default,
        ///     <code>false</code> otherwise</returns>
        [Obsolete("Please use GetDefaultValue(ushort, bool, JunctionRestrictionFlags)")]
        bool GetDefaultNearTurnOnRedAllowed(ushort segmentId, bool startNode, ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetDefaultValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines the default setting for far turn-on-red at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if turn-on-red is allowed for far turns by default,
        ///     <code>false</code> otherwise</returns>
        [Obsolete("Please use GetDefaultValue(ushort, bool, JunctionRestrictionFlags)")]
        bool GetDefaultFarTurnOnRedAllowed(ushort segmentId, bool startNode, ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetDefaultValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines the default turn-on-red setting for the given turn type at the given segment end.
        /// </summary>
        /// <param name="near">for near turns?</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if turn-on-red is allowed by default, <code>false</code>
        ///     otherwise</returns>
        [Obsolete("Please use GetDefaultValue(ushort, bool, JunctionRestrictionFlags)")]
        bool GetDefaultTurnOnRedAllowed(bool near,
                                        ushort segmentId,
                                        bool startNode,
                                        ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetDefaultValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines the default setting for straight lane changes at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if straight lane changes are allowed by default,
        ///     <code>false</code> otherwise</returns>
        [Obsolete("Please use GetDefaultValue(ushort, bool, JunctionRestrictionFlags)")]
        bool GetDefaultLaneChangingAllowedWhenGoingStraight(
            ushort segmentId,
            bool startNode,
            ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetDefaultValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines the default setting for entering a blocked junction at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if entering a blocked junction is allowed by default,
        ///     <code>false</code> otherwise</returns>
        [Obsolete("Please use GetDefaultValue(ushort, bool, JunctionRestrictionFlags)")]
        bool GetDefaultEnteringBlockedJunctionAllowed(ushort segmentId,
                                                      bool startNode,
                                                      ref NetNode node);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetDefaultValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines the default setting for pedestrian crossings at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="node">node data</param>
        /// <returns><code>true</code> if crossing the road is allowed by default,
        ///     <c>false</c> otherwise
        /// returns the default value if flag is not set.</returns>
        [Obsolete("Please use GetDefaultValue(ushort, bool, JunctionRestrictionFlags)")]
        bool GetDefaultPedestrianCrossingAllowed(ushort segmentId,
                                                 bool startNode,
                                                 ref NetNode node);
        #endregion

        #region Is<Traffic Rule>Allowed

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValueOrDefault(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines whether u-turns are allowed at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if u-turns are allowed, <c>false</c> otherwise
        /// returns the default value if flag is not set.</returns>
        [Obsolete("Please use GetValueOrDefault(ushort, bool, JunctionRestrictionFlags)")]
        bool IsUturnAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValueOrDefault(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines whether turn-on-red is allowed for near turns at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if turn-on-red is allowed for near turns, <c>false</c> otherwise
        /// returns the default value if flag is not set.</returns>
        [Obsolete("Please use GetValueOrDefault(ushort, bool, JunctionRestrictionFlags)")]
        bool IsNearTurnOnRedAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValueOrDefault(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines whether turn-on-red is allowed for far turns at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if turn-on-red is allowed for far turns, <c>false</c> otherwise
        /// returns the default behaviour if flag is not set.  </returns>
        [Obsolete("Please use GetValueOrDefault(ushort, bool, JunctionRestrictionFlags)")]
        bool IsFarTurnOnRedAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValueOrDefault(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines whether turn-on-red is allowed for the given turn type at the given segment end.
        /// </summary>
        /// <param name="near">for near turns?</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if turn-on-red is allowed, <c>false</c> otherwise
        /// returns the default behaviour if flag is not set.</returns>
        [Obsolete("Please use GetValueOrDefault(ushort, bool, JunctionRestrictionFlags)")]
        bool IsTurnOnRedAllowed(bool near, ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValueOrDefault(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines whether lane changing when going straight is allowed at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if lane changing when going straight is allowed, <c>false</c> otherwise
        /// returns the default behaviour if flag is not set.</returns>
        [Obsolete("Please use GetValueOrDefault(ushort, bool, JunctionRestrictionFlags)")]
        bool IsLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValueOrDefault(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines whether entering a blocked junction is allowed at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if entering a blocked junction is allowed, <code>false</code> otherwise
        /// returns the default behaviour if flag is not set.</returns>
        [Obsolete("Please use GetValueOrDefault(ushort, bool, JunctionRestrictionFlags)")]
        bool IsEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValueOrDefault(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Determines whether crossing the road is allowed at the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if crossing the road is allowed, <c>false</c> otherwise.
        /// returns the default value if flag is not set.</returns>
        [Obsolete("Please use GetValueOrDefault(ushort, bool, JunctionRestrictionFlags)")]
        bool IsPedestrianCrossingAllowed(ushort segmentId, bool startNode);
        #endregion

        #region Get<Traffic Rule>

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Retrieves the u-turn setting for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns>ternary u-turn flag.
        /// returns <c>TernaryBool.Undefined</c> if the flag is not set.</returns>
        [Obsolete("Please use GetValue(ushort, bool, JunctionRestrictionFlags)")]
        TernaryBool GetUturnAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Retrieves the turn-on-red setting for near turns and the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns>ternary turn-on-red flag for near turns.
        /// returns <c>TernaryBool.Undefined</c> if the flag is not set.</returns>
        [Obsolete("Please use GetValue(ushort, bool, JunctionRestrictionFlags)")]
        TernaryBool GetNearTurnOnRedAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Retrieves the turn-on-red setting for far turns and the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns>ternary turn-on-red flag for far turns.
        /// returns <c>TernaryBool.Undefined</c> if the flag is not set.</returns>
        [Obsolete("Please use GetValue(ushort, bool, JunctionRestrictionFlags)")]
        TernaryBool GetFarTurnOnRedAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Retrieves the turn-on-red setting for the given turn type and segment end.
        /// </summary>
        /// <param name="near">for near turns?</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns>ternary turn-on-red flag.
        /// returns <c>TernaryBool.Undefined</c> if the flag is not set.</returns>
        [Obsolete("Please use GetValue(ushort, bool, JunctionRestrictionFlags)")]
        TernaryBool GetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Retrieves the lane changing setting for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns>ternary lane changing flag.
        /// returns <c>TernaryBool.Undefined</c> if the flag is not set.</returns>
        [Obsolete("Please use GetValue(ushort, bool, JunctionRestrictionFlags)")]
        TernaryBool GetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Retrieves the "enter blocked junction" setting for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns>ternary "enter blocked junction" flag.
        /// returns <c>TernaryBool.Undefined</c> if the flag is not set.</returns>
        [Obsolete("Please use GetValue(ushort, bool, JunctionRestrictionFlags)")]
        TernaryBool GetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="GetValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Retrieves the pedestrian crossing setting for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns>ternary pedestrian crossing flag.
        /// returns <c>TernaryBool.Undefined</c> if the flag is not set.</returns>
        [Obsolete("Please use GetValue(ushort, bool, JunctionRestrictionFlags)")]
        TernaryBool GetPedestrianCrossingAllowed(ushort segmentId, bool startNode);
        #endregion

        #region Toggle<Traffic Rule>

        /// <summary>
        /// This API is dprecated. Please use <see cref="ToggleValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Switches the u-turn flag for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use ToggleValue(ushort, bool, JunctionRestrictionFlags)")]
        bool ToggleUturnAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="ToggleValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Switches the turn-on-red flag for near turns and given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use ToggleValue(ushort, bool, JunctionRestrictionFlags)")]
        bool ToggleNearTurnOnRedAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="ToggleValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Switches the turn-on-red flag for far turns and given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use ToggleValue(ushort, bool, JunctionRestrictionFlags)")]
        bool ToggleFarTurnOnRedAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="ToggleValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Switches the turn-on-red flag for the given turn type and segment end.
        /// </summary>
        /// <param name="near">for near turns?</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use ToggleValue(ushort, bool, JunctionRestrictionFlags)")]
        bool ToggleTurnOnRedAllowed(bool near, ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="ToggleValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Switches the lane changing flag for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use ToggleValue(ushort, bool, JunctionRestrictionFlags)")]
        bool ToggleLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="ToggleValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Switches the "enter blocked junction" flag for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use ToggleValue(ushort, bool, JunctionRestrictionFlags)")]
        bool ToggleEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode);

        /// <summary>
        /// This API is dprecated. Please use <see cref="ToggleValue(ushort, bool, JunctionRestrictionsFlags)"/>.<br/>
        /// Switches the pedestrian crossing flag for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use ToggleValue(ushort, bool, JunctionRestrictionFlags)")]
        bool TogglePedestrianCrossingAllowed(ushort segmentId, bool startNode);
        #endregion

        #region Set<Traffic Rule> : bool

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the u-turn flag for the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetUturnAllowed(ushort segmentId, bool startNode, bool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the turn-on-red flag for near turns at the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetNearTurnOnRedAllowed(ushort segmentId, bool startNode, bool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the turn-on-red flag for far turns at the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetFarTurnOnRedAllowed(ushort segmentId, bool startNode, bool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the turn-on-red flag for the given turn type and segment end to the given value.
        /// </summary>
        /// <param name="near">for near turns?</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, bool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the lane changing flag for the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, bool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the "enter blocked junction" flag for the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, bool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the pedestrian crossing flag for the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, bool value);
        #endregion

        #region Set<Traffic Rule> : TernaryBool

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the u-turn flag for the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value. Passing in <c>TernaryBool.Undefined</c> removes the traffic bool.</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetUturnAllowed(ushort segmentId, bool startNode, TernaryBool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the turn-on-red flag for near turns at the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value. Passing in <c>TernaryBool.Undefined</c> removes the traffic bool.</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetNearTurnOnRedAllowed(ushort segmentId, bool startNode, TernaryBool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the turn-on-red flag for far turns at the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value. Passing in <code>TernaryBool.Undefined</code> removes the traffic bool.</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetFarTurnOnRedAllowed(ushort segmentId, bool startNode, TernaryBool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the turn-on-red flag for the given turn type and segment end to the given value.
        /// </summary>
        /// <param name="near">for near turns?</param>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value. Passing in <code>TernaryBool.Undefined</code> removes the traffic bool.</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, TernaryBool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the lane changing flag for the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value. Passing in <code>TernaryBool.Undefined</code> removes the traffic bool.</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, TernaryBool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the "enter blocked junction" flag for the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value. Passing in <code>TernaryBool.Undefined</code> removes the traffic bool.</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, TernaryBool value);

        /// <summary>
        /// This API is dprecated. Please use <see cref="SetValue(ushort, bool, JunctionRestrictionsFlags, bool?)"/>.<br/>
        /// Sets the pedestrian crossing flag for the given segment end to the given value.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <param name="value">new value. Passing in <code>TernaryBool.Undefined</code> removes the traffic bool.</param>
        /// <returns> <c>true</c> on success.
        /// Silently returns <c>true</c> if the given <paramref name="value"/> equals to the flag.
        /// On failure (including when traffic rule is not configurable) returns <c>false</c>
        /// </returns>
        [Obsolete("Please use SetValue(ushort, bool, JunctionRestrictionFlags, bool?)")]
        bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, TernaryBool value);
        #endregion

        /// <summary>
        /// Removes all traffic rules for the input segment end.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        /// <returns><c>true</c> on success, <c>false</c> otherwise</returns>
        bool ClearSegmentEnd(ushort segmentId, bool startNode);

        /// <summary>
        /// Updates the default values for all junction restrictions and segments.
        /// </summary>
        void UpdateAllDefaults();

    }
}