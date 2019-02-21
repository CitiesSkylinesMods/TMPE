using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using CSUtil.Commons;

namespace TrafficManager.Util {
    public class ModCompatibilityHelper {
        public static bool IsSteamWorkshopItemSubscribed(ulong itemId) {
                return ContentManagerPanel.subscribedItemsTable.Contains(new PublishedFileId(itemId));
        }
        
        // TODO more checks for incompatible workshop content
        public static void CheckForOriginalTMPE() {
            Log.Info("Checking for original version of TM:PE steamID:583429740");
            if (IsSteamWorkshopItemSubscribed(583429740)) {
                Log.Warning("Found original version of TM:PE steamID:583429740");
                string msg = $"TM:PE v.{TrafficManagerMod.Version} detected that you are subscribing to older version of TM:PE (steam id -> 583429740) made by @LinuxFan. \nPlease unsubscribe it from Steam Workshop and restart the game.";
                UIView.GetAView().panelsLibrary.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Detected Original TM:PE subscription", msg, false);
            }
        }
    }
}
