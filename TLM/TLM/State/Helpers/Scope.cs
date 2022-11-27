namespace TrafficManager.State.Helpers; 
using System;
[Flags]
public enum Scope {
    None = 0,
    Global = 1,
    Savegame = 2,
    Both = Global | Savegame,
}
