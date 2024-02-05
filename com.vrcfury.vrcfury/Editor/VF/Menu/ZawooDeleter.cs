using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Menu {
    public static class ZawooDeleter {
        public static void Run(VFGameObject avatarObj) {
            var effects = CleanupAllZawooComponents(avatarObj, false);
            if (effects.Count == 0) {
                EditorUtility.DisplayDialog(
                    "Zawoo Cleanup",
                    "No zawoo objects were found on avatar",
                    "Ok"
                );
                return;
            }
            var doIt = EditorUtility.DisplayDialog(
                "Zawoo Cleanup",
                "This tool is meant to be used to remove broken, old installs of the Zawoo prefab.\n\n" +
                "The following parts will be deleted from your avatar:\n" + string.Join("\n", effects) +
                "\n\nContinue?",
                "Yes, Delete them",
                "Cancel"
            );
            if (!doIt) return;
            CleanupAllZawooComponents(avatarObj, true);
        }
        
        private static List<string> CleanupAllZawooComponents(VFGameObject avatarObj, bool perform = false) {
            return AvatarCleaner.Cleanup(
                avatarObj,
                perform: perform,
                ShouldRemoveAsset: ShouldRemoveAsset,
                ShouldRemoveLayer: ShouldRemoveLayer,
                ShouldRemoveObj: ShouldRemoveObj,
                ShouldRemoveParam: ShouldRemoveParam
            );
        }

        private static bool ShouldRemoveObj(VFGameObject obj) {
            if (obj == null) return false;
            if (PrefabUtility.IsPartOfPrefabInstance(obj) && !PrefabUtility.IsOutermostPrefabInstanceRoot(obj)) {
                // Don't try to remove if it's part of a prefab, because it's probly inside the VRCFury prefab
                return false;
            }
            if (ShouldRemoveAsset(obj)) return true;
            var lower = obj.name.ToLower();
            if (lower.Contains("caninepeen")) return true;
            if (lower.Contains("hybridpeen")) return true;
            if (lower.Contains("hybridanthropeen")) return true;
            if (lower.Contains("peen_low")) return true;
            if (lower.Contains("particles_dynamic")) return true;
            if (lower.Contains("dynamic_penetrator")) return true;
            if (lower.Contains("armature_peen")) return true;
            return false;
        }
        private static bool ShouldRemoveAsset(Object obj) {
            if (obj == null) return false;
            var path = AssetDatabase.GetAssetPath(obj);
            if (path == null) return false;
            var lower = path.ToLower();
            if (lower.Contains("caninepeen")) return true;
            if (lower.Contains("hybridanthropeen")) return true;
            return false;
        }
        private static bool ShouldRemoveLayer(string name) {
            if (name.StartsWith("kcp_")) return true;
            if (name == "State Change") return true;
            if (name == "Particle") return true;
            if (name == "Dynamic") return true;
            return false;
        }
        private static bool ShouldRemoveParam(string name) {
            if (name.StartsWith("caninePeen")) return true;
            if (name.StartsWith("peen")) return true;
            return false;
        }
    }
}