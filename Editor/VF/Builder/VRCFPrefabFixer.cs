using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Model;

namespace VF.Builder {
    public class VRCFPrefabFixer {
        /**
         * Unity has two big annoying bugs.
         * 1) If a change is made externally (like via git) to a nested prefab, the change
         *   won't appear on parent prefabs which contain an instant of it, until the parent prefab's asset
         *   is reloaded.
         * 2) If a prefab contains a reference to a ScriptableObject (such as a vrchat menu or vrchat params),
         *   and that object asset doesn't exist, then the reference will still be broken even after the object has
         *   been imported. Reimporting the prefab magically resolves the issue.
         *
         * To combat both of these issues, this method will find all prefabs on the object which contain VRCFury
         * components, then will force-reimport them in bottom-up order.
         */
        public static void Fix(GameObject avatarObj) {
            Debug.Log("Running VRCFury prefab fix pass on " + avatarObj);
            var dependsOn = new Dictionary<string, HashSet<string>>();
            HashSet<string> GetDependsOn(string childPath) {
                if (!dependsOn.ContainsKey(childPath)) dependsOn[childPath] = new HashSet<string>();
                return dependsOn[childPath];
            }
            foreach (var vrcf in avatarObj.GetComponentsInChildren<VRCFury>(true)) {
                string childPath = null;
                for (var obj = vrcf.gameObject; obj != null; obj = PrefabUtility.GetCorrespondingObjectFromSource(obj)) {
                    if (!obj.GetComponent<VRCFury>()) continue;
                    var path = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    if (childPath != null) {
                        GetDependsOn(childPath).Add(path);
                    }
                    GetDependsOn(path);
                    childPath = path;
                }
            }

            var reloadOrder = new List<string>();
            while (dependsOn.Count > 0) {
                var next = dependsOn
                    .Where(pair => pair.Value.Count == 0)
                    .Select(pair => pair.Key)
                    .FirstOrDefault();
                if (next == null) {
                    // There's a loop in the asset dependencies???
                    // Just... pick one I guess
                    next = dependsOn.First().Key;
                }

                reloadOrder.Add(next);
                dependsOn.Remove(next);
                foreach (var l in dependsOn.Values) l.Remove(next);
            }

            foreach (var path in reloadOrder) {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}