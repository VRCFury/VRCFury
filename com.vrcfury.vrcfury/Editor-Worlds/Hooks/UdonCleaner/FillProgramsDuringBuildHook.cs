using System;
using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Utils;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace VF.Hooks.UdonCleaner {
    /**
     * The serialized programs still need to get into the real build somehow. Normally the VRCSDK would do this,
     * but all attempts to set serializedProgramAsset are captured by our transpiler hooks, so it's up to us
     * to fill them in. Luckily, we can do it... more non-destructively than the VRCSDK did. No need to modify
     * the original prefabs like u graph does, no need to make saved copies of the prefab like u# does,
     * we can just instantiate them into the temp scene, mess with them there, and unity will automatically
     * clean everything up after the build.
     */
    internal class FillProgramsDuringBuildHook : IProcessSceneWithReport {
        public int callbackOrder => -1;

        public void OnProcessScene(Scene scene, BuildReport report) {
            var stack = new Stack<GameObject>();
            var prefabInstances = new Dictionary<GameObject, GameObject>();

            var prefabHolder = new Lazy<GameObject>(() => {
                var h = new GameObject("__PrefabHolder");
                h.SetActive(false);
                SceneManager.MoveGameObjectToScene(h, scene);
                return h;
            });

            foreach (var obj in scene.GetRootGameObjects()) {
                stack.Push(obj);
            }

            bool ReplacePrefabsWithInst(SerializedObject so) {
                var prop = so.GetIterator();
                var changed = false;
                while (prop.Next(true)) {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (!(prop.objectReferenceValue is GameObject go)) continue;
                    if (go.scene == scene) continue;
                    if (!prefabInstances.TryGetValue(go, out var inst)) {
                        inst = Object.Instantiate(go, prefabHolder.Value.transform, false);
                        inst.name = go.name;
                        prefabInstances[go] = inst;
                        stack.Push(inst);
                    }
                    changed = true;
                    prop.objectReferenceValue = inst;
                }
                return changed;
            }

            while (stack.TryPop(out var obj)) {
                foreach (var ub in obj.GetComponentsInChildren<UdonBehaviour>(true)) {
                    var so = new SerializedObject(ub);
                    so.FindProperty("programSource").objectReferenceValue = StoreUdonSharpProgramsInTempFolderHook.programSource_get(ub);
                    so.FindProperty("serializedProgramAsset").objectReferenceValue = StoreUdonSharpProgramsInTempFolderHook.serializedProgramAsset_get(ub);
                    if (SyncMethodManagerHook.IsActive()) so.FindProperty("_syncMethod").enumValueIndex = (int)ub.SyncMethod;
                    ReplacePrefabsWithInst(so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var ub in obj.GetComponentsInChildren<UdonSharpBehaviour>(true)) {
                    var so = new SerializedObject(ub);
                    if (ReplacePrefabsWithInst(so)) {
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }
    }
}

