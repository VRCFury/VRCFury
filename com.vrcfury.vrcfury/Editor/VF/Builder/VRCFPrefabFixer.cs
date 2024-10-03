using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Inspector;
using VF.Model;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder {
    internal static class VRCFPrefabFixer {
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
            //Debug.Log("Running VRCFury prefab fix pass on " + objs.Select(o => o.GetPath()).Join(", "));

            var dependsOn = new Dictionary<string, HashSet<string>>();
            HashSet<string> GetDependsOn(string childPath) {
                if (!dependsOn.ContainsKey(childPath)) dependsOn[childPath] = new HashSet<string>();
                return dependsOn[childPath];
            }
            foreach (var sceneVrcf in objs.SelectMany(o => o.GetComponentsInSelfAndChildren<VRCFury>())) {
                string childPath = null;
                for (var vrcf = sceneVrcf; vrcf != null; vrcf = GetCorrespondingObjectFromSource(vrcf)) {
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

            if (reloadOrder.Count > 0) {
                Debug.Log("VRCFury is force re-importing: " + reloadOrder.Join(", "));

                VRCFuryAssetDatabase.WithAssetEditing(() => {
                    foreach (var path in reloadOrder) {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    }
                });

                Debug.Log("Done");
            }

            foreach (var sceneVrcf in objs.SelectMany(o => o.GetComponentsInSelfAndChildren<VRCFury>())) {
                for (var vrcf = sceneVrcf; vrcf != null; vrcf = GetCorrespondingObjectFromSource(vrcf)) {
                    var mods = GetModifications(vrcf);
                    if (mods.Count > 0) {
                        Debug.Log($"Reverting overrides on {vrcf}: {mods.Select(m => m.propertyPath).Join(", ")}");
                        PrefabUtility.RevertObjectOverride(vrcf, InteractionMode.AutomatedAction);
                        VRCFuryEditorUtils.MarkDirty(vrcf);
                        Debug.Log($"Done");
                    }
                }
            }
        }

        public static ICollection<PropertyModification> GetModifications(Object obj) {
            var parents = new HashSet<Object>();
            for (var i = obj; i != null; i = GetCorrespondingObjectFromSource(i)) {
                parents.Add(i);
            }
            var mods = PrefabUtility.GetPropertyModifications(obj);
            if (mods == null) return new PropertyModification[] { };
            return mods.Where(mod => parents.Contains(mod.target)).ToArray();
        }

        private static T GetCorrespondingObjectFromSource<T>(T obj) where T : Object {
            // For some reason, this method in unity occasionally throws a random "Specified cast is not valid" exception
            try {
                return PrefabUtility.GetCorrespondingObjectFromSource(obj);
            } catch (Exception) {
                return null;
            }
        }
    }
}
