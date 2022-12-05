namespace TrafficManager.UI.Helpers;
using System;

// Likely to change or be removed in future
[Flags]
public enum Scope {
    None = 0,
    Global = 1,
    Savegame = 2,
    GlobalOrSavegame = Global | Savegame,
}
