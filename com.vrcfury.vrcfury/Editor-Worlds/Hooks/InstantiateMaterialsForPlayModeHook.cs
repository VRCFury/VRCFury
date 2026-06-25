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

namespace VF.Hooks {
    /**
     * When in play mode, we instantiate a clone of any materials that are used by udon,
     * to prevent that udon from modifying the original files in the project.
     * During a real upload, it's fine to just upload the originals, since they'll be
     * reset in game any time you rejoin the world.
     */
    internal class InstantiateMaterialsForPlayModeHook : IProcessSceneWithReport {
        public int callbackOrder => 200000;

        public void OnProcessScene(Scene scene, BuildReport report) {
            if (!Application.isPlaying) return;
            //if (Application.isPlaying) return;

            var materialInstances = new Dictionary<Material, Material>();
            var customRenderTextureInstances = new Dictionary<CustomRenderTexture, CustomRenderTexture>();
            var loggedLargeProperties = new HashSet<string>();

            Material GetMaterialInstance(Material mat, bool allowClone) {
                if (!materialInstances.TryGetValue(mat, out var inst)) {
                    if (!allowClone) return null;
                    inst = mat.Clone("Original was used by udon");
                    VRCFuryAssetDatabase.SaveAsset(inst, TmpFilePackage.GetPath()+"/Builds", inst.name + " (Play Mode)");
                    materialInstances[mat] = inst;
                }
                return inst;
            }

            CustomRenderTexture GetCustomRenderTextureInstance(CustomRenderTexture rt, bool allowClone) {
                if (!customRenderTextureInstances.TryGetValue(rt, out var inst)) {
                    if (!allowClone) return null;
                    inst = UnityEngine.Object.Instantiate(rt);
                    inst.name = rt.name;
                    VrcfObjectFactory.Register(inst, copyWorkLogFrom: rt);
                    inst.WorkLog("Original was used by udon");
                    if (rt.material != null) {
                        inst.material = GetMaterialInstance(rt.material, true);
                    }
                    if (rt.initializationMaterial != null) {
                        inst.initializationMaterial = GetMaterialInstance(rt.initializationMaterial, true);
                    }
                    VRCFuryAssetDatabase.SaveAsset(inst, TmpFilePackage.GetPath()+"/Builds", inst.name + " (Play Mode)");
                    customRenderTextureInstances[rt] = inst;
                }
                return inst;
            }

            bool ReplaceMaterials(SerializedObject so, bool allowClone) {
                var changed = false;
                foreach (var prop in so.IterateFast()) {
                    // BEGIN TEMP LARGE PROPERTY WALK DIAGNOSTICS
                    // Remove this block after diagnosing slow play-mode material instantiation.
                    if (prop.isArray
                        && prop.propertyType != SerializedPropertyType.String
                        && prop.arraySize >= 1000) {
                        var firstElement = prop.GetArrayElementAtIndex(0);
                        var walksIntoArray = firstElement.propertyType == SerializedPropertyType.ObjectReference
                                             || firstElement.propertyType == SerializedPropertyType.ExposedReference
                                             || firstElement.propertyType == SerializedPropertyType.Generic
                                             || firstElement.propertyType == SerializedPropertyType.ManagedReference;
                        if (walksIntoArray) {
                            var target = so.targetObject;
                            var key = $"{target.GetInstanceID()}:{prop.propertyPath}";
                            if (loggedLargeProperties.Add(key)) {
                                Debug.Log(
                                    $"[VRCFury] InstantiateMaterialsForPlayModeHook is walking large property " +
                                    $"{target.GetType().FullName}.{prop.propertyPath} with {prop.arraySize} children on {target.name}",
                                    target);
                            }
                        }
                    }
                    // END TEMP LARGE PROPERTY WALK DIAGNOSTICS

                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (prop.objectReferenceValue is Material mat) {
                        var inst = GetMaterialInstance(mat, allowClone);
                        if (inst == null) continue;
                        changed = true;
                        prop.objectReferenceValue = inst;
                    } else if (prop.objectReferenceValue is CustomRenderTexture rt) {
                        var inst = GetCustomRenderTextureInstance(rt, allowClone);
                        if (inst == null) continue;
                        changed = true;
                        prop.objectReferenceValue = inst;
                    }
                }

                return changed;
            }

            VRCFuryAssetDatabase.WithAssetEditing(() => {
                foreach (var ub in scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren<UdonBehaviour>())) {
                    var so = new SerializedObject(ub);
                    if (ReplaceMaterials(so, true)) {
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                foreach (var ub in scene.Roots()
                             .SelectMany(root => root.GetComponentsInSelfAndChildren<UdonSharpBehaviour>())) {
                    var so = new SerializedObject(ub);
                    if (ReplaceMaterials(so, true)) {
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                foreach (var c in scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren())) {
                    if (c.GetType().Name == "ftLightmapsStorage") continue;
                    var so = new SerializedObject(c);
                    if (ReplaceMaterials(so, false)) {
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            });
        }
    }
}
