using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace VF.Updater {
    public static class Markers {
        private static async Task<string> GetAppRootDir() {
            return Path.GetDirectoryName(await AsyncUtils.InMainThread(() => Application.dataPath));
        }

        public static async Task<string> ManualUpdateInProgressMarker() { 
            return await GetAppRootDir() + "/Temp/vrcfUpdated";
        }
        
        public static async Task<string> InstallInProgressMarker() { 
            return await GetAppRootDir() + "/Temp/vrcfInstalling";
        }

        public static async Task<string> UpdaterJustUpdatedMarker() { 
            return await GetAppRootDir() + "/Temp/vrcfUpdateAll";
        }
    }
}