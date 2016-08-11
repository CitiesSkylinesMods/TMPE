# TM:PE -- /Custom/PathFinding
Detoured path-finding classes.
## Classes
- **CustomPathFind**: Implements modifications made with the lane changer and lane connector tool (ProcessItemMain), implements improved algorithms for lane selection at city roads and highways (ProcessItemMain) and implements the Advanced Vehicle AI (ProcessItemCosts) by reading current densities and speeds and calculating appropriate costs hereof. 
- **CustomPathManager**: Initiates custom path-finding where the **ExtVehicleType** of a vehicle is additionally passed to the **CustomPathFind** instance.