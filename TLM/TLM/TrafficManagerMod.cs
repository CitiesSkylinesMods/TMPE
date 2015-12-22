using ICities;
using UnityEngine;

namespace TrafficManager
{
    public class TrafficManagerMod : IUserMod
    {
        public string Name => "Traffic Manager Plus";

        public string Description => "Traffic Junction Manager [v1.3]";

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
