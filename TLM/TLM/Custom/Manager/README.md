# TM:PE -- /Custom/Manager
Detoured *Manager classes.
## Classes
- **CustomCitizenManager**: Implements detours for checking if citizens instances are being released. Notifies the ExtCitizenInstanceManager.
- **CustomNetManager**: Implements detours for checking if segments/nodes are being added/updated/removed. Recalculates segment/node geometries if necessary.
- **CustomVehicleManager**: Implements detours for checking if vehicles are begin created/released. Determines the **ExtVehicleType** of a vehicle as soon as it is created. 