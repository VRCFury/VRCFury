using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Inspector;
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
        public static void Fix(ICollection<VFGameObject> objs) {
            Debug.Log("Running VRCFury prefab fix pass on " + objs);

            var dependsOn = new Dictionary<string, HashSet<string>>();
            HashSet<string> GetDependsOn(string childPath) {
                if (!dependsOn.ContainsKey(childPath)) dependsOn[childPath] = new HashSet<string>();
                return dependsOn[childPath];
            }
            foreach (var sceneVrcf in objs.SelectMany(o => o.GetComponentsInSelfAndChildren<VRCFury>())) {
                string childPath = null;
                for (var vrcf = sceneVrcf; vrcf != null; vrcf = PrefabUtility.GetCorrespondingObjectFromSource(vrcf)) {
                    var path = AssetDatabase.GetAssetPath(vrcf);
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
                    // There's a loop in the asset dependencies???
                    // Just... pick one I guess
                    .FirstOrDefault() ?? dependsOn.First().Key;

                reloadOrder.Add(next);
                dependsOn.Remove(next);
                foreach (var l in dependsOn.Values) l.Remove(next);
            }

            if (reloadOrder.Count > 0) {
                Debug.Log("VRCFury is force re-importing: " + string.Join(", ", reloadOrder));
            }

            foreach (var path in reloadOrder) {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            
            foreach (var sceneVrcf in objs.SelectMany(o => o.GetComponentsInSelfAndChildren<VRCFury>())) {
                for (var vrcf = sceneVrcf; vrcf != null; vrcf = PrefabUtility.GetCorrespondingObjectFromSource(vrcf)) {
                    var mods = GetModifications(vrcf);
                    if (mods.Count > 0) {
                        Debug.Log($"Reverting overrides on {vrcf}: {string.Join(", ", mods.Select(m => m.propertyPath))}");
                        PrefabUtility.RevertObjectOverride(vrcf, InteractionMode.AutomatedAction);
                        VRCFuryEditorUtils.MarkDirty(vrcf);
                    }
                }
            }
            
            Debug.Log("Prefab fix completed");
        }

        public static ICollection<PropertyModification> GetModifications(Object obj) {
            var parents = new HashSet<Object>();
            for (var i = obj; i != null; i = PrefabUtility.GetCorrespondingObjectFromSource(i)) {
                parents.Add(i);
            }
            var mods = PrefabUtility.GetPropertyModifications(obj);
            if (mods == null) return new PropertyModification[] { };
            return mods.Where(mod => parents.Contains(mod.target)).ToArray();
        }
    }
}
