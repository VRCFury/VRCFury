using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VF {
    public class VRCFPackageUtils {
        
        /// <summary>
        /// GUID for package.json
        /// </summary>
        private const string PackageJsonGuid = "da4518ec79a04334b86a18805f1b8d24";
        private static string version;

        /// <summary>
        /// Current version of VRCFury
        /// </summary>
        public static string Version {
            get {
                if (string.IsNullOrEmpty(version)) {
                    string assetPath = AssetDatabase.GUIDToAssetPath(PackageJsonGuid);
                    if(String.IsNullOrEmpty(assetPath))
                        version = "Development";
                    else
                        version = JsonUtility.FromJson<PackageManifestData>(File.ReadAllText(Path.GetFullPath(assetPath))).version;
                }
                
                return version;
            }
        }
        
        /// <summary>
        /// Partial Implementation of the package manifest for deserialization
        /// </summary>
        public class PackageManifestData {
            public string version;
        }
    }
}