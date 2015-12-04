using ICities;
using UnityEngine;

namespace TrafficManager
{
    public class TrafficManagerMod : IUserMod
    {
        public string Name => "Traffic Manager";

        public string Description => "Manage traffic junctions";

        public void OnEnabled()
        {
            Log.Message("TrafficManagerMod Enabled");
        }

        public void OnDisabled()
        {
            Log.Message("TrafficManagerMod Disabled");
        }
    }
}
