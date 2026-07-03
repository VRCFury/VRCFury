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

            var sceneReferenceEdges = new Dictionary<UnityEngine.Object, List<(string propertyPath, UnityEngine.Object referenced)>>();
            var materialTextureEdges = new Dictionary<Material, List<(string propertyName, CustomRenderTexture referenced)>>();
            var crtReferenceEdges = new Dictionary<CustomRenderTexture, List<(string propertyPath, UnityEngine.Object referenced)>>();
            var clones = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var cloneRequired = new HashSet<UnityEngine.Object>();
            var loggedLargeProperties = new HashSet<string>();

            List<T> GetOrCreateList<TKey, T>(Dictionary<TKey, List<T>> map, TKey key) {
                if (!map.TryGetValue(key, out var list)) {
                    map[key] = list = new List<T>();
                }
                return list;
            }

            void ScanSerializedReferences(SerializedObject so, Action<string, UnityEngine.Object> onReference) {
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
                        onReference(prop.propertyPath, mat);
                    } else if (prop.objectReferenceValue is CustomRenderTexture rt) {
                        onReference(prop.propertyPath, rt);
                    }
                }
            }

            List<(string propertyName, CustomRenderTexture referenced)> GetMaterialTextureEdges(Material mat) {
                if (materialTextureEdges.TryGetValue(mat, out var edges)) return edges;

                edges = GetOrCreateList(materialTextureEdges, mat);
                foreach (var propertyName in mat.GetTexturePropertyNames()) {
#if UNITY_2022_1_OR_NEWER
                    if (!mat.HasTexture(propertyName)) continue;
#endif
                    var referenced = mat.GetTexture(propertyName) as CustomRenderTexture;
                    if (referenced == null) continue;
                    edges.Add((propertyName, referenced));
                }
                return edges;
            }

            List<(string propertyPath, UnityEngine.Object referenced)> GetCrtReferenceEdges(CustomRenderTexture rt) {
                if (crtReferenceEdges.TryGetValue(rt, out var edges)) return edges;

                edges = GetOrCreateList(crtReferenceEdges, rt);
                var so = new SerializedObject(rt);
                ScanSerializedReferences(so, (propertyPath, referenced) => {
                    edges.Add((propertyPath, referenced));
                });
                return edges;
            }

            foreach (var c in scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren())) {
                if (c.GetType().Name == "ftLightmapsStorage") continue;
                var isUdon = c is UdonBehaviour || c is UdonSharpBehaviour;
                var so = new SerializedObject(c);
                var edges = GetOrCreateList(sceneReferenceEdges, c);
                ScanSerializedReferences(so, (propertyPath, referenced) => {
                    edges.Add((propertyPath, referenced));
                    if (isUdon) {
                        cloneRequired.Add(referenced);
                    }
                });
            }

            var assetsToScan = new Queue<UnityEngine.Object>(cloneRequired);
            while (assetsToScan.Count > 0) {
                switch (assetsToScan.Dequeue()) {
                    case Material mat:
                        foreach (var (_, referenced) in GetMaterialTextureEdges(mat)) {
                            if (cloneRequired.Add(referenced)) {
                                assetsToScan.Enqueue(referenced);
                            }
                        }
                        break;

                    case CustomRenderTexture rt:
                        foreach (var (_, referenced) in GetCrtReferenceEdges(rt)) {
                            if (cloneRequired.Add(referenced)) {
                                assetsToScan.Enqueue(referenced);
                            }
                        }
                        break;
                }
            }

            UnityEngine.Object GetObjectClone(UnityEngine.Object obj) {
                if (!cloneRequired.Contains(obj)) return null;
                return obj switch {
                    Material mat => GetMaterialClone(mat),
                    CustomRenderTexture rt => GetCustomRenderTextureClone(rt),
                    _ => null
                };
            }

            Material GetMaterialClone(Material mat) {
                if (clones.TryGetValue(mat, out var existing)) return (Material)existing;

                // Cache the clone before rewriting references so material <-> CRT cycles terminate.
                var inst = mat.Clone("Original was used by udon");
                clones[mat] = inst;
                foreach (var (propertyName, referenced) in GetMaterialTextureEdges(mat)) {
                    var referencedInst = GetObjectClone(referenced) as CustomRenderTexture;
                    if (referencedInst == null) continue;
                    inst.SetTexture(propertyName, referencedInst);
                }
                VRCFuryAssetDatabase.SaveAsset(inst, TmpFilePackage.GetPath()+"/Builds", inst.name + " (Play Mode)");
                return inst;
            }

            CustomRenderTexture GetCustomRenderTextureClone(CustomRenderTexture rt) {
                if (clones.TryGetValue(rt, out var existing)) return (CustomRenderTexture)existing;

                // Cache the clone before rewriting references so material <-> CRT cycles terminate.
                var inst = UnityEngine.Object.Instantiate(rt);
                clones[rt] = inst;
                inst.name = rt.name;
                VrcfObjectFactory.Register(inst, copyWorkLogFrom: rt);
                inst.WorkLog("Original was used by udon");

                var so = new SerializedObject(inst);
                var changed = false;
                foreach (var (propertyPath, referenced) in GetCrtReferenceEdges(rt)) {
                    var referencedInst = GetObjectClone(referenced);
                    if (referencedInst == null) continue;
                    var prop = so.FindProperty(propertyPath);
                    if (prop == null || prop.objectReferenceValue == referencedInst) continue;
                    prop.objectReferenceValue = referencedInst;
                    changed = true;
                }
                if (changed) {
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                VRCFuryAssetDatabase.SaveAsset(inst, TmpFilePackage.GetPath()+"/Builds", inst.name + " (Play Mode)");
                return inst;
            }

            VRCFuryAssetDatabase.WithAssetEditing(() => {
                foreach (var (owner, edges) in sceneReferenceEdges) {
                    var so = new SerializedObject(owner);
                    var changed = false;
                    foreach (var edge in edges) {
                        var inst = GetObjectClone(edge.referenced);
                        if (inst == null) continue;
                        var prop = so.FindProperty(edge.propertyPath);
                        if (prop == null || prop.objectReferenceValue == inst) continue;
                        prop.objectReferenceValue = inst;
                        changed = true;
                    }
                    if (changed) {
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            });
        }
    }
}
