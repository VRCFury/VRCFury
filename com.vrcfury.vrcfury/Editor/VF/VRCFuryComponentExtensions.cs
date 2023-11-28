using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Upgradeable;

namespace VF {
    public static class VRCFuryComponentExtensions {
        private static HashSet<string> reimported = new HashSet<string>();

        /**
         * 
         * Unity doesn't try to re-deserialize assets after updating vrcfury, leaving components in a broken state.
         * If we find a broken component, schedule a reimport of it to try and resolve the issue.
         */
        private static void DelayReimport(VRCFuryComponent c) {
            string GetPath() {
                if (c == null) return null;
                var path = AssetDatabase.GetAssetPath(c);
                if (reimported.Contains(path)) return null;
                return path;
            }

            if (GetPath() == null) return;
            EditorApplication.delayCall += () => {
                if (!c.IsBroken()) return;
                var path = GetPath();
                if (path == null) return;
                reimported.Add(path);
                Debug.Log("Reimporting VRCFury asset that unity thinks is corrupted: " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            };
        }
        
        public static void Upgrade(this VRCFuryComponent c) {
            if (c.IsBroken()) return;
            if (PrefabUtility.IsPartOfPrefabInstance(c)) return;
            if (IUpgradeableUtility.UpgradeRecursive(c)) {
                EditorUtility.SetDirty(c);
            }
        }
        
        public static bool IsBroken(this VRCFuryComponent c) {
            return c.GetBrokenMessage() != null;
        }
        public static string GetBrokenMessage(this VRCFuryComponent c) {
            if (IUpgradeableUtility.IsTooNew(c)) {
                DelayReimport(c);
                return $"This component was created with a newer version of VRCFury ({c.Version} > {c.GetLatestVersion()}";
            }
            if (UnitySerializationUtils.ContainsNullsInList(c)) {
                DelayReimport(c);
                return "Found a null list on a child object";
            }
            return null;
        }
    }
}
