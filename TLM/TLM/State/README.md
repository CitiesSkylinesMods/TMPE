# TM:PE -- /State
Configuration and classes for loading and saving.
## Classes
- **Configuration**: Serializable classes that is used to store the custom game state together with the savegame data.
- **Flags**: Custom game state storage backend (deprecated, marked for deletion: Custom game state information should be accessed through the *Manager classes).
- **GlobalConfig**: Represents the global configuration that is stored in TMPE_GlobalConfig.xml
- **Options**: Holds player-set options. Builds the options dialog.
- **SerializableDataExtension**: Main load/save class. Serializes/Deserializes **Configuration** instances to/from savegame data.