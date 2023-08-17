using UnityEditor;
using VF.Component;
using VF.Upgradeable;

namespace VF {
    public static class VRCFuryComponentExtensions {
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
                return $"This component was created with a newer version of VRCFury ({c.Version} > {c.GetLatestVersion()}";
            }
            if (UnitySerializationUtils.ContainsNullsInList(c)) {
                return "Found a null list on a child object";
            }
            return null;
        }
    }
}
