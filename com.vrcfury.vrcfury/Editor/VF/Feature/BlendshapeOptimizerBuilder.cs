using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {
    public class BlendshapeOptimizerBuilder : FeatureBuilder<BlendshapeOptimizer> {
        public override string GetEditorTitle() {
            return "Blendshape Optimizer";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will automatically bake all non-animated blendshapes into the mesh," +
                " saving VRAM for free!"
            ));
            
            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            };
            content.Add(adv);
            
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("keepMmdShapes"), "Keep MMD Blendshapes"));
            
            return content;
        }

        public override bool AvailableOnProps() {
            return false;
        }

        [FeatureBuilderAction(FeatureOrder.BlendshapeOptimizer)]
        public void Apply() {

            foreach (var mesh in GetAllSkinMeshes()) {
                var blendshapeCount = mesh.blendShapeCount;
                if (blendshapeCount == 0) continue;

                var animatedBlendshapes = CollectAnimatedBlendshapesForMesh(mesh);

                bool ShouldKeepName(string name) {
                    if (animatedBlendshapes.Contains(name)) return true;
                    if (model.keepMmdShapes && MmdUtils.IsMaybeMmdBlendshape(name)) return true;
                    return false;
                }

                var blendshapeIdsToKeep = Enumerable.Range(0, blendshapeCount)
                    .Where(id => ShouldKeepName(mesh.GetBlendShapeName(id)))
                    .ToImmutableHashSet();

                if (blendshapeIdsToKeep.Count == blendshapeCount) {
                    continue;
                }

                var skinsForMesh = CollectSkinsUsingMesh(mesh);

                var savedWeights = skinsForMesh
                    .Select(skin => {
                        var weights = Enumerable.Range(0, blendshapeCount)
                            .Select(skin.GetBlendShapeWeight).ToArray();
                        return (skin, weights);
                    })
                    .ToArray();

                var savedBlendshapes = Enumerable.Range(0, blendshapeCount)
                    .Select(id => new SavedBlendshape(mesh, id))
                    .ToArray();

                var meshCopy = mutableManager.MakeMutable(mesh);
                meshCopy.ClearBlendShapes();
                
                for (var id = 0; id < blendshapeCount; id++) {
                    var savedBlendshape = savedBlendshapes[id];
                    var keep = blendshapeIdsToKeep.Contains(id);

                    if (keep) {
                        savedBlendshape.SaveTo(meshCopy);
                    } else {
                        var (_, firstSkinWeights) = savedWeights[0];
                        savedBlendshape.BakeTo(meshCopy, firstSkinWeights[id]);
                    }
                }
                VRCFuryEditorUtils.MarkDirty(meshCopy);

                var avatars = avatarObject.GetComponentsInSelfAndChildren<VRCAvatarDescriptor>();
                foreach (var (skin, weights) in savedWeights) {
                    skin.sharedMesh = meshCopy;
                    var newId = 0;
                    for (var id = 0; id < blendshapeCount; id++) {
                        var keep = blendshapeIdsToKeep.Contains(id);
                        if (keep) {
                            skin.SetBlendShapeWeight(newId, weights[id]);

                            foreach (var avatar in avatars) {
                                if (avatar.customEyeLookSettings.eyelidsSkinnedMesh == skin) {
                                    for (var i = 0; i < avatar.customEyeLookSettings.eyelidsBlendshapes.Length; i++) {
                                        if (avatar.customEyeLookSettings.eyelidsBlendshapes[i] == id) {
                                            avatar.customEyeLookSettings.eyelidsBlendshapes[i] = newId;
                                            VRCFuryEditorUtils.MarkDirty(avatar);
                                        }
                                    }
                                }
                            }
                            newId++;
                        }
                    }
                    VRCFuryEditorUtils.MarkDirty(skin);
                }
            }
        }

        private class SavedBlendshape {
            private string name;
            private List<Tuple<float, Vector3[], Vector3[], Vector3[]>> frames
                = new List<Tuple<float, Vector3[], Vector3[], Vector3[]>>();
            public SavedBlendshape(Mesh mesh, int id) {
                name = mesh.GetBlendShapeName(id);
                for (var i = 0; i < mesh.GetBlendShapeFrameCount(id); i++) {
                    var weight = mesh.GetBlendShapeFrameWeight(id, i);
                    var v = new Vector3[mesh.vertexCount];
                    var n = new Vector3[mesh.vertexCount];
                    var t = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(id, i, v, n, t);
                    frames.Add(Tuple.Create(weight, v, n, t));
                }
            }

            public void SaveTo(Mesh mesh) {
                foreach (var (w, v, n, t) in frames) {
                    mesh.AddBlendShapeFrame(name, w, v, n, t);
                }
            }

            public void BakeTo(Mesh mesh, float weight100) {
                // TODO: Is this how multiple frames work?
                var lastFrame = frames[frames.Count - 1];
                if (frames.Count == 0 || weight100 == 0) {
                    return;
                } else if (frames.Count == 1 || weight100 < 0 || weight100 >= lastFrame.Item1) {
                    var (_, dv, dn, dt) = lastFrame;
                    BakeTo(mesh, dv, dn, dt, weight100);
                } else {
                    var beforeFrame = Enumerable
                        .Range(0, frames.Count)
                        .First(frame => frame == frames.Count || weight100 <= frames.Count);
                    if (beforeFrame == 0) {
                        var (fw, fv, fn, ft) = frames[0];
                        BakeTo(mesh, fv, fn, ft, weight100 / fw);
                    } else {
                        var (fw1, fv1, fn1, ft1) = frames[beforeFrame-1];
                        var (fw2, fv2, fn2, ft2) = frames[beforeFrame];
                        var fraction = (weight100 - fw1) / (fw2 - fw1);
                        var dv = Enumerable.Zip(fv1, fv2, (a, b) => a + (b - a) * fraction).ToArray();
                        var dn = Enumerable.Zip(fn1, fn2, (a, b) => a + (b - a) * fraction).ToArray();
                        var dt = Enumerable.Zip(ft1, ft2, (a, b) => a + (b - a) * fraction).ToArray();
                        BakeTo(mesh, dv, dn, dt);
                    }
                }
            }

            private static void BakeTo(Mesh mesh, Vector3[] dv, Vector3[] dn, Vector3[] dt, float weight100 = 100) {
                var verts = mesh.vertices;
                var normals = mesh.normals;
                var tangents = mesh.tangents;
                for (var i = 0; i < verts.Length && i < dv.Length; i++) {
                    verts[i] += dv[i] * (weight100 / 100);
                }
                for (var i = 0; i < normals.Length && i < dn.Length; i++) {
                    normals[i] += dn[i] * (weight100 / 100);
                }
                for (var i = 0; i < tangents.Length && i < dt.Length; i++) {
                    var d = dt[i] * (weight100 / 100);
                    tangents[i] += new Vector4(d.x, d.y, d.z, 0);
                }
                mesh.vertices = verts;
                mesh.normals = normals;
                mesh.tangents = tangents;
            }
        }

        private ICollection<Mesh> GetAllSkinMeshes() {
            return GetAllSkins()
                .Select(skin => skin.sharedMesh)
                .Where(mesh => mesh != null)
                .ToImmutableHashSet();
        }

        private ICollection<SkinnedMeshRenderer> GetAllSkins() {
            return avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>();
        }

        private ICollection<SkinnedMeshRenderer> CollectSkinsUsingMesh(Mesh mesh) {
            return GetAllSkins()
                .Where(skin => skin.sharedMesh == mesh)
                .ToImmutableHashSet();
        }

        private ICollection<(EditorCurveBinding, AnimationCurve)> GetBindings(GameObject obj, AnimatorController controller) {
            var prefix = AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);

            var clipsInController = new AnimatorIterator.Clips().From(controller);

            return clipsInController.SelectMany(clip => {
                var clipBindings = AnimationUtility.GetCurveBindings(clip);
                return clipBindings.Select(b => {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    b.path = ClipRewriter.Join(prefix, b.path, allowAdvancedOperators: false);
                    return (b, curve);
                });
            }).ToList();
        }

        private ICollection<string> CollectAnimatedBlendshapesForMesh(Mesh mesh) {
            var animatedBindings = manager.GetAllUsedControllersRaw()
                .Select(tuple => tuple.Item2)
                .SelectMany(controller => GetBindings(avatarObject, controller))
                .Concat(avatarObject.GetComponentsInSelfAndChildren<Animator>()
                    .SelectMany(animator => GetBindings(animator.gameObject, animator.runtimeAnimatorController as AnimatorController)))
                .ToList();

            var skins = CollectSkinsUsingMesh(mesh);

            var skinPaths = skins
                .Select(skin => clipBuilder.GetPath(skin.transform))
                .ToImmutableHashSet();
            
            var blendshapeNames = new List<string>();
            for (var i = 0; i < mesh.blendShapeCount; i++) {
                blendshapeNames.Add(mesh.GetBlendShapeName(i));
            }
            
            var animatedBlendshapes = new HashSet<string>();
            foreach (var tuple in animatedBindings) {
                var (binding, curve) = tuple;
                if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                if (!binding.propertyName.StartsWith("blendShape.")) continue;
                if (!skinPaths.Contains(binding.path)) continue;
                var blendshape = binding.propertyName.Substring(11);
                var blendshapeId = mesh.GetBlendShapeIndex(blendshape);
                var animatesToNondefaultValue = false;
                if (blendshapeId >= 0) {
                    var skinDefaultValues = skins
                        .Select(skin => skin.GetBlendShapeWeight(blendshapeId))
                        .ToArray();
                    foreach (var frameValue in curve.keys.Select(key => key.value)) {
                        foreach (var skinDefaultValue in skinDefaultValues) {
                            if (!Mathf.Approximately(frameValue, skinDefaultValue)) {
                                animatesToNondefaultValue = true;
                            }
                        }
                    }
                }

                if (animatesToNondefaultValue) {
                    animatedBlendshapes.Add(blendshape);
                }
            }

            foreach (var avatar in avatarObject.GetComponentsInSelfAndChildren<VRCAvatarDescriptor>()) {
                if (avatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes) {
                    if (skins.Contains(avatar.customEyeLookSettings.eyelidsSkinnedMesh)) {
                        foreach (var b in avatar.customEyeLookSettings.eyelidsBlendshapes) {
                            if (b >= 0 && b < blendshapeNames.Count) {
                                animatedBlendshapes.Add(blendshapeNames[b]);
                            }
                        }
                    }
                }

                if (skins.Contains(avatar.VisemeSkinnedMesh)) {
                    if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape) {
                        animatedBlendshapes.Add(avatar.MouthOpenBlendShapeName);
                    }

                    if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape) {
                        foreach (var b in avatar.VisemeBlendShapes) {
                            animatedBlendshapes.Add(b);
                        }
                    }
                }
            }

            for (var i = 0; i < mesh.blendShapeCount; i++) {
                var weightsUsedForBlendshape = skins
                    .Select(skin => skin.GetBlendShapeWeight(i))
                    .ToImmutableHashSet();
                if (weightsUsedForBlendshape.Count > 1) {
                    animatedBlendshapes.Add(blendshapeNames[i]);
                }
            }

            return animatedBlendshapes;
        }
    }
}
