using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VF {
    public class VRCFPackageUtils {
        
        // GUID for VRCFury package.json
        private const string PackageJsonGuid = "da4518ec79a04334b86a18805f1b8d24";
        private static string version;

        public static string Version {
            get {
                if (version == null) {
                    var assetPath = AssetDatabase.GUIDToAssetPath(PackageJsonGuid);
                    if(string.IsNullOrEmpty(assetPath))
                        version = "???";
                    else
                        version = JsonUtility.FromJson<PackageManifestData>(File.ReadAllText(Path.GetFullPath(assetPath))).version;
                }

                return version;
            }
        }

        private class PackageManifestData {
            public string version;
        }
    }
}