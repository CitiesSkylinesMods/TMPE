namespace TrafficManager.Manager {
    using CSUtil.Commons;
    using TrafficManager.API.Manager;

    /// <summary>
    /// Abstract manager class, supports events before/after loading/saving.
    ///
    /// Event sequences:
    ///
    /// Startup / Level loading:
    ///		1. OnInit (TODO) ->
    ///		2. {Flags|NodeGeometry|SegmentGeometry}.OnBeforeLoadData ->
    ///		3. OnBeforeLoadData ->
    ///		4. (SerializableDataExtension loads custom game data) ->
    ///		5. OnAfterLoadData ->
    ///		6. (LoadingManager sets up detours) ->
    ///		7. OnLevelLoading
    ///	Saving:
    ///		1. OnBeforeSaveData ->
    ///		2. (SerializableDataExtension saves custom game data) ->
    ///		3. OnAfterSaveData
    ///	Level unloading:
    ///		1. (LoadingManager releases detours) ->
    ///		2. OnLevelUnloading
    /// </summary>
    public abstract class AbstractCustomManager : ICustomManager {
        /// <summary>
        /// Performs actions after game data has been loaded
        /// </summary>
        public virtual void OnAfterLoadData() {
        }

        /// <summary>
        /// Performs actions after game data has been saved
        /// </summary>
        public virtual void OnAfterSaveData() {
        }

        /// <summary>
        /// Performs actions before game data is going to be loaded
        /// </summary>
        public virtual void OnBeforeLoadData() {
        }

        /// <summary>
        /// Performs actions before game data is going to be saved
        /// </summary>
        public virtual void OnBeforeSaveData() {
        }

        /// <summary>
        /// Performs actions after a game has been loaded
        /// </summary>
        public virtual void OnLevelLoading() {
        }

        /// <summary>
        /// Performs actions after a game has been unloaded
        /// </summary>
        public virtual void OnLevelUnloading() {
        }

        /// <summary>
        /// Prints information for debugging purposes
        /// </summary>
        protected virtual void InternalPrintDebugInfo() {
        }

        public void PrintDebugInfo() {
            Log._Debug($"=== {GetType().Name}.PrintDebugInfo() *START* ===");
            InternalPrintDebugInfo();
            Log._Debug($"=== {GetType().Name}.PrintDebugInfo() *END* ===");
        }
    }
}