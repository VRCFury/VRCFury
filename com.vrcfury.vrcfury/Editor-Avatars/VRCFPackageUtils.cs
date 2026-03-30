using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VF.Upgradeable;

namespace VF {
    internal static class VRCFPackageUtils {
        [InitializeOnLoadMethod]
        private static void SendToComponents() {
            VrcfUpgradeableMonoBehaviour.currentVrcfVersion = Version;
        }

        public static string Version => GetVersionFromGuid("da4518ec79a04334b86a18805f1b8d24");

        private static readonly Dictionary<string, string> versionCache
            = new Dictionary<string, string>();

        private static string GetVersionFromGuid(string guid) {
            return GetVersionFromPath(AssetDatabase.GUIDToAssetPath(guid));
        }
        
        public static string GetVersionFromId(string id) {
            return GetVersionFromPath($"Packages/{id}/package.json");
        }

        private static string GetVersionFromPath(string path) {
            if (versionCache.TryGetValue(path, out var cached)) {
                return cached;
            }
            versionCache[path] = LoadVersion(path);
            return versionCache[path];
        }

        private static string LoadVersion(string packageJsonPath) {
            try {
                if (!string.IsNullOrEmpty(packageJsonPath)) {
                    var text = File.ReadAllText(packageJsonPath);
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
