# TM:PE -- /UI
User interface classes.
## Classes
- **CameraCtrl**: Allows to move the player camera to certain objects (used for debugging)
- **SubTool**: Represents a tool that can be selected through the main menu.
- **TextureResources**: Holds references to all required textures.
- **ToolMode**: Enum. Allows to describe which sub tool is currently active.
- **TrafficManagerTool**: Central UI controller. Manages active sub tools and forwards UI update calls to the sub tools.
- **Translation**: Internationalization functions.
- **TransportDemandViewMode**: Enum. Represents the currently active demand mode when the public transport info view is active together with the Parking AI
- **UIBase**: Holds an instance of the main menu and main menu button
- **UIMainMenuButton**: the main menu button
- **UITransportDemand**: transport demand view mode toggle window