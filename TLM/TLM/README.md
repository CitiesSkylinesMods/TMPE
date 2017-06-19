# TM:PE -- /
This is the project root directory.
## Classes
- **CodeProfiler**: Helper class for profiling
- **Constants**: Well, constant things
- **LoadingExtension**: Extends **LoadingExtensionBase**. Performs detouring of game methods and checks for incompatible mods.
- **ThreadingExtension**: Extends **ThreadingExtensionBase**. Calls several custom **SimulationStep** methods.
- **TrafficManagerMod**: Implements **IUserMod**. Mod main class. Defines mod version and required game version.
- **TrafficManagerMode**: Enum to express the menu visibility state (None=closed, Activated=open)