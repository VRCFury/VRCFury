using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace VF.Updater {
    public static class Markers {
        private static async Task<string> GetAppRootDir() {
            return Path.GetDirectoryName(await AsyncUtils.InMainThread(() => Application.dataPath));
        }

        public static async Task<Marker> ManualUpdateInProgress() { 
            return new Marker(await GetAppRootDir() + "/Temp/vrcfUpdated");
        }
        
        public static async Task<Marker> UpgradeFromLegacyInProgress() { 
            return new Marker(await GetAppRootDir() + "/Temp/vrcfLegacyUpgrade");
        }
        
        public static async Task<Marker> FreshInstallInProgress() { 
            return new Marker(await GetAppRootDir() + "/Temp/vrcfInstalling");
        }

        public static async Task<Marker> UpdaterJustUpdated() { 
            return new Marker(await GetAppRootDir() + "/Temp/vrcfUpdateAll");
        }
    }
}
