using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VF.Upgradeable;

namespace VF {
    public class VRCFPackageUtils {
        [InitializeOnLoadMethod]
        static void SendToComponents() {
            VrcfUpgradeableMonoBehaviour.currentVrcfVersion = Version;
        }

        // GUID for VRCFury package.json
        private const string PackageJsonGuid = "da4518ec79a04334b86a18805f1b8d24";
        private static string version;

        public static string Version {
            get {
                if (version == null) version = LoadVersion();
                return version;
            }
        }

        private static string LoadVersion() {
            try {
                var assetPath = AssetDatabase.GUIDToAssetPath(PackageJsonGuid);
                if (!string.IsNullOrEmpty(assetPath)) {
                    var text = File.ReadAllText(assetPath);
                    return JsonUtility.FromJson<PackageManifestData>(text).version;
                }
            } catch (Exception) {
                // ignored
            }
            return "???";
        }

        private class PackageManifestData {
            public string version;
        }
    }
}
