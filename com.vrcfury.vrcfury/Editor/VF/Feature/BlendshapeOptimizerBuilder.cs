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
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {
    [FeatureTitle("Blendshape Optimizer")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class BlendshapeOptimizerBuilder : FeatureBuilder<BlendshapeOptimizer> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AnimatorHolderService animators;

        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will automatically bake all non-animated blendshapes into the mesh," +
                " saving VRAM for free!"
            ));
            return content;
        }

        [FeatureBuilderAction(FeatureOrder.BlendshapeOptimizer)]
        public void Apply() {
            var keepMmdShapes = globals.allFeaturesInRun.Any(f => f is MmdCompatibility);

            var logOutput = "";
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                var mesh = skin.GetMesh();
                if (mesh == null) continue;
                var blendshapeCount = mesh.blendShapeCount;
                if (blendshapeCount == 0) continue;
                var path = skin.owner().GetPath(avatarObject);

                // will print with ┬─ at the start, for nicer viewing in the console
                logOutput += $"\n\u252c\u2500 Optimizing {path}\n";

                var animatedBlendshapes = CollectAnimatedBlendshapesForMesh(skin);

                bool ShouldKeepName(string name) {
                    if (animatedBlendshapes.Contains(name)) return true;
                    if (keepMmdShapes && MmdUtils.IsMaybeMmdBlendshape(name) && path == "Body") return true;
                    return false;
                }

                var blendshapeIdsToKeep = Enumerable.Range(0, blendshapeCount)
                    .Where(id => ShouldKeepName(mesh.GetBlendShapeName(id)))
                    .ToImmutableHashSet();

                if (blendshapeIdsToKeep.Count == blendshapeCount) {
                    continue;
                }

                var savedWeights = Enumerable.Range(0, blendshapeCount)
                    .Select(skin.GetBlendShapeWeight).ToArray();

                var savedBlendshapes = Enumerable.Range(0, blendshapeCount)
                    .Select(id => new SavedBlendshape(mesh, id))
                    .ToArray();

                mesh = skin.GetMutableMesh("Needed to remove blendshapes for blendshape optimizer");
                mesh.ClearBlendShapes();

                for (var id = 0; id < blendshapeCount; id++) {
                    var savedBlendshape = savedBlendshapes[id];
                    var keep = blendshapeIdsToKeep.Contains(id);

                    string logOutputDetail;
                    if (keep) {
                        savedBlendshape.SaveTo(mesh, out logOutputDetail);
                    } else {
                        savedBlendshape.BakeTo(mesh, savedWeights[id], out logOutputDetail);
                    }
                    // add ├ and └ for nicer looking log output
                    logOutput += (id != blendshapeCount-1 ? "\u251c" : "\u2514") + logOutputDetail;
                }
                VRCFuryEditorUtils.MarkDirty(mesh);

                var newId = 0;
                for (var id = 0; id < blendshapeCount; id++) {
                    var keep = blendshapeIdsToKeep.Contains(id);
                    if (keep) {
                        skin.SetBlendShapeWeight(newId, savedWeights[id]);
                        if (avatar.customEyeLookSettings.eyelidsSkinnedMesh == skin) {
                            for (var i = 0; i < avatar.customEyeLookSettings.eyelidsBlendshapes.Length; i++) {
                                if (avatar.customEyeLookSettings.eyelidsBlendshapes[i] == id) {
                                    avatar.customEyeLookSettings.eyelidsBlendshapes[i] = newId;
                                    VRCFuryEditorUtils.MarkDirty(avatar);
                                }
                            }
                        }
                        newId++;
                    }
                }
            }
            Debug.Log($"Blendshape Optimizer Actions:\n{logOutput}");
        }

        private class SavedBlendshape {
            private readonly string name;
            private readonly List<Tuple<float, Vector3[], Vector3[], Vector3[]>> frames
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

            public void SaveTo(Mesh mesh, out string logOutputDetail) {
                logOutputDetail = $"Keeping BlendShape \"{name}\"\n";
                foreach (var (w, v, n, t) in frames) {
                    mesh.AddBlendShapeFrame(name, w, v, n, t);
                }
            }

            public void BakeTo(Mesh mesh, float weight100, out string logOutputDetail) {
                logOutputDetail = $"Baking BlendShape \"{name}\" into mesh at weight {weight100}, as weight is not animated\n";
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

        private ICollection<(EditorCurveBinding, AnimationCurve)> GetBindings(VFGameObject obj, VFController controller) {
            var prefix = obj.GetPath(avatarObject);

            var clipsInController = new AnimatorIterator.Clips().From(controller);

            return clipsInController
                .SelectMany(clip => clip.GetFloatCurves())
                .Select(pair => {
                    var (binding, curve) = pair;
                    binding.path = ClipRewritersService.Join(prefix, binding.path, allowAdvancedOperators: false);
                    return (binding, curve);
                })
                .ToList();
        }

        private ICollection<string> CollectAnimatedBlendshapesForMesh(SkinnedMeshRenderer skin) {
            var animatedBindings = controllers.GetAllUsedControllers()
                .SelectMany(controller => GetBindings(avatarObject, controller))
                .Concat(animators.GetSubControllers().SelectMany(pair =>
                    GetBindings(pair.owner, pair.controller)
                ))
                .ToList();

            var skinPath = skin.owner().GetPath(avatarObject);
            
            var animatedBlendshapes = new HashSet<string>();
            foreach (var tuple in animatedBindings) {
                var (binding, curve) = tuple;
                if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                if (!binding.propertyName.StartsWith("blendShape.")) continue;
                if (binding.path != skinPath) continue;
                var blendshape = binding.propertyName.Substring(11);
                var animatesToNondefaultValue = false;
                if (skin.HasBlendshape(blendshape)) {
                    var skinDefaultValue = skin.GetBlendShapeWeight(blendshape);
                    foreach (var frameValue in curve.keys.Select(key => key.value)) {
                        if (!Mathf.Approximately(frameValue, skinDefaultValue)) {
                            animatesToNondefaultValue = true;
                        }
                    }
                }

                if (animatesToNondefaultValue) {
                    animatedBlendshapes.Add(blendshape);
                }
            }

            if (avatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes) {
                if (skin == avatar.customEyeLookSettings.eyelidsSkinnedMesh) {
                    foreach (var b in avatar.customEyeLookSettings.eyelidsBlendshapes) {
                        var name = skin.GetBlendshapeName(b);
                        if (name != null) {
                            animatedBlendshapes.Add(name);
                        }
                    }
                }
            }

            if (skin == avatar.VisemeSkinnedMesh) {
                if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape) {
                    animatedBlendshapes.Add(avatar.MouthOpenBlendShapeName);
                }

                if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape) {
                    foreach (var b in avatar.VisemeBlendShapes) {
                        animatedBlendshapes.Add(b);
                    }
                }
            }

            return animatedBlendshapes;
        }
    }
}
