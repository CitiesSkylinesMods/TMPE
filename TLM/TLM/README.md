# TM:PE -- /
This is the project root directory.
## Classes
- **CodeProfiler**: Helper class for profiling
- **LoadingExtensions**: Extends the **LoadingExtensionBase** class from the game's API. Performs detouring of game methods and checks for incompatible mods. 
- **Log**: Offers file logging functionality (everything is written into TMPE.log)
- **ThreadingExtension**: (possibly unnecessary)
- **TrafficManagerMod**: Implements the **IUserMod** from the game's API. Mod main class. Defines mod version and required game version.
- **TrafficManagerMode**: Enum to express the menu visibility state (None=closed, Activated=open)