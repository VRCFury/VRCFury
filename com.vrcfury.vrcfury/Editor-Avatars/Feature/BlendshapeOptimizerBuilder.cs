using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
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
        // Position tolerance is a fraction of the mesh's longest dimension.
        private const float TinyPositionFraction = 0.0001f;
        private const float TinyNormalDelta = 0.0001f;

        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AnimatorHolderService animators;

        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature saves VRAM by baking non-animated blendshapes into the mesh" +
                " and removing blendshapes whose position and normal changes are all tiny."
            ));
            return content;
        }

        [FeatureBuilderAction(FeatureOrder.BlendshapeOptimizer)]
        public void Apply() {
            var keepMmdShapes = globals.allFeaturesInRun.Any(f => f is MmdCompatibility);
            var blendshapeCurves = new Lazy<ICollection<(VFBinding, AnimationCurve)>>(() =>
                controllers.GetAllUsedControllers()
                    .SelectMany(GetBlendshapeCurves)
                    .Concat(animators.GetSubControllers().SelectMany(pair => GetBlendshapeCurves(pair.controller)))
                    .ToList()
            );

            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                var mesh = skin.GetMesh();
                if (mesh == null) continue;
                var blendshapeCount = mesh.blendShapeCount;
                if (blendshapeCount == 0) continue;
                var path = skin.owner().GetPath(avatarObject);

                var animatedBlendshapes = CollectAnimatedBlendshapesForMesh(skin, blendshapeCurves);
                var legacyVisemes = avatar.VisemeSkinnedMesh == skin
                    && avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                    ? avatar.VisemeBlendShapes ?? Array.Empty<string>()
                    : Array.Empty<string>();
                animatedBlendshapes.UnionWith(legacyVisemes);

                bool ShouldKeepName(string name) {
                    if (animatedBlendshapes.Contains(name)) return true;
                    if (keepMmdShapes && MmdUtils.IsMaybeMmdBlendshape(name) && path == "Body") return true;
                    return false;
                }

                var animatedBlendshapeIds = Enumerable.Range(0, blendshapeCount)
                    .Where(id => ShouldKeepName(mesh.GetBlendShapeName(id)))
                    .ToImmutableHashSet();

                var frames = new FrameBuffer(mesh.vertexCount);
                var tinyShapes = new Dictionary<int, string>();
                if (animatedBlendshapeIds.Count > 0) {
                    var meshSize = GetMeshSize(mesh);
                    var tolerance = meshSize * TinyPositionFraction;
                    var toleranceSq = tolerance * tolerance;
                    foreach (var id in animatedBlendshapeIds) {
                        if (!frames.HasOnlyTinyChanges(mesh, id, toleranceSq, out var peak)) continue;
                        var fraction = meshSize > 0 ? peak.position / meshSize : 0;
                        tinyShapes[id] = FormattableString.Invariant($"peak position {peak.position:G4} mesh units ({fraction:G4} of mesh size), normal {peak.normal:G4}");
                    }
                }
                if (animatedBlendshapeIds.Count == blendshapeCount && tinyShapes.Count == 0) continue;

                var savedWeights = Enumerable.Range(0, blendshapeCount).Select(skin.GetBlendShapeWeight).ToArray();
                var source = mesh;
                mesh = skin.GetMutableMesh(
                    $"Removed {blendshapeCount - animatedBlendshapeIds.Count} unanimated blendshapes and pruned {tinyShapes.Count} blendshapes with negligible deltas",
                    forceClone: true
                );
                mesh.ClearBlendShapes();
                var shapesToBake = new List<int>();
                Vector3[] emptyDeltas = null;
                for (var id = 0; id < blendshapeCount; id++) {
                    var name = source.GetBlendShapeName(id);
                    if (tinyShapes.TryGetValue(id, out var deltas)) {
                        if (legacyVisemes.Contains(name)) {
                            // An empty frame keeps the slot valid. VRChat disables legacy lip sync if any viseme is missing.
                            emptyDeltas ??= new Vector3[mesh.vertexCount];
                            mesh.AddBlendShapeFrame(name, 100, emptyDeltas, null, null);
                            mesh.WorkLog($"Cleared deltas for legacy viseme \"{name}\" ({deltas})");
                        } else {
                            mesh.WorkLog($"Removed \"{name}\" ({deltas})");
                        }
                    } else if (animatedBlendshapeIds.Contains(id)) {
                        frames.CopyShape(source, id, mesh);
                    } else {
                        // Shapes with negative frame weights can contribute at renderer weight zero.
                        if (savedWeights[id] != 0 || source.GetBlendShapeFrameWeight(id, 0) <= 0) shapesToBake.Add(id);
                        mesh.WorkLog($"Removed unanimated \"{name}\" at weight {savedWeights[id]}");
                    }
                }
                if (shapesToBake.Count > 0) BakeShapes(source, mesh, frames, shapesToBake, savedWeights);
                mesh.Dirty();

                for (var id = 0; id < mesh.blendShapeCount; id++) {
                    var originalId = source.GetBlendShapeIndex(mesh.GetBlendShapeName(id));
                    skin.SetBlendShapeWeight(id, savedWeights[originalId]);
                }
                if (avatar.customEyeLookSettings.eyelidsSkinnedMesh == skin) {
                    var eyelids = avatar.customEyeLookSettings.eyelidsBlendshapes;
                    for (var i = 0; i < eyelids.Length; i++) {
                        var id = eyelids[i];
                        if (id < 0 || id >= blendshapeCount) continue;
                        eyelids[i] = mesh.GetBlendShapeIndex(source.GetBlendShapeName(id));
                        if (eyelids[i] != id) avatar.Dirty();
                    }
                }
            }
        }

        private static float GetMeshSize(Mesh mesh) {
            var vertices = mesh.vertices;
            if (vertices.Length == 0) return 0;
            var min = vertices[0];
            var max = min;
            foreach (var vertex in vertices) {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }
            var size = max - min;
            return Mathf.Max(size.x, size.y, size.z);
        }

        private class FrameBuffer {
            private readonly Vector3[] positions;
            private readonly Vector3[] normals;
            private readonly Vector3[] tangents;

            public FrameBuffer(int vertexCount) {
                positions = new Vector3[vertexCount];
                normals = new Vector3[vertexCount];
                tangents = new Vector3[vertexCount];
            }

            public bool HasOnlyTinyChanges(
                Mesh mesh,
                int id,
                float positionToleranceSquared,
                out (float position, float normal) peak
            ) {
                peak = default;
                const float normalToleranceSquared = TinyNormalDelta * TinyNormalDelta;
                for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(id); frame++) {
                    mesh.GetBlendShapeFrameVertices(id, frame, positions, normals, null);
                    for (var i = 0; i < positions.Length; i++) {
                        var p = positions[i].sqrMagnitude;
                        var n = normals[i].sqrMagnitude;
                        if (!(p <= positionToleranceSquared && n <= normalToleranceSquared)) return false;
                        peak.position = Mathf.Max(peak.position, p);
                        peak.normal = Mathf.Max(peak.normal, n);
                    }
                }
                peak = (Mathf.Sqrt(peak.position), Mathf.Sqrt(peak.normal));
                return true;
            }

            public void CopyShape(Mesh source, int id, Mesh destination) {
                var name = source.GetBlendShapeName(id);
                for (var frame = 0; frame < source.GetBlendShapeFrameCount(id); frame++) {
                    source.GetBlendShapeFrameVertices(id, frame, positions, normals, tangents);
                    destination.AddBlendShapeFrame(
                        name,
                        source.GetBlendShapeFrameWeight(id, frame),
                        positions,
                        normals,
                        tangents
                    );
                }
            }
        }

        private static void BakeShapes(Mesh source, Mesh destination, FrameBuffer frames, List<int> ids, float[] weights) {
            // Sample only the discarded shapes, in mesh local space without bones or renderer transforms.
            var temporary = EditorUtility.CreateGameObjectWithHideFlags(
                "Bake blendshapes",
                HideFlags.HideAndDontSave,
                typeof(SkinnedMeshRenderer)
            );
            var samplingMesh = new Mesh();
            var baked = new Mesh();
            try {
                samplingMesh.vertices = source.vertices;
                samplingMesh.normals = source.normals;
                samplingMesh.tangents = source.tangents;
                foreach (var id in ids) frames.CopyShape(source, id, samplingMesh);
                var renderer = temporary.GetComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = samplingMesh;
                for (var i = 0; i < ids.Count; i++) renderer.SetBlendShapeWeight(i, weights[ids[i]]);
                renderer.BakeMesh(baked);
                destination.vertices = baked.vertices;
                destination.normals = baked.normals;
                destination.tangents = baked.tangents;
            } finally {
                UnityEngine.Object.DestroyImmediate(temporary);
                UnityEngine.Object.DestroyImmediate(samplingMesh);
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        private IEnumerable<(VFBinding, AnimationCurve)> GetBlendshapeCurves(VFController controller) {
            return new AnimatorIterator.Clips().From(controller)
                .SelectMany(clip => clip.GetFloatCurves())
                .Where(pair => pair.Item1.type == typeof(SkinnedMeshRenderer))
                .Where(pair => pair.Item1.propertyName.StartsWith("blendShape."));
        }

        private HashSet<string> CollectAnimatedBlendshapesForMesh(
            SkinnedMeshRenderer skin,
            Lazy<ICollection<(VFBinding, AnimationCurve)>> animatedBindings
        ) {
            var animatedBlendshapes = new HashSet<string>();
            foreach (var tuple in animatedBindings.Value) {
                var (binding, curve) = tuple;
                if (!binding.Targets(skin.owner())) continue;
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

            if (skin == avatar.VisemeSkinnedMesh && avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape) {
                animatedBlendshapes.Add(avatar.MouthOpenBlendShapeName);
            }

#if VRCSDK_HAS_FACE_TRACKING
            foreach (var blendshape in GetFaceTrackingBlendshapeNames(skin)) {
                animatedBlendshapes.Add(blendshape);
            }
#endif

            return animatedBlendshapes;
        }

#if VRCSDK_HAS_FACE_TRACKING
        private IEnumerable<string> GetFaceTrackingBlendshapeNames(SkinnedMeshRenderer skin) {
            if (!avatar.enableFaceTracking
                || avatar.faceTrackingMeshes == null
                || !avatar.faceTrackingMeshes.Contains(skin)) yield break;

            foreach (var set in (avatar.faceTrackingBlendshapesSetList
                                 ?? Enumerable.Empty<VRCAvatarDescriptor.FaceTrackingBlendshapesSet>())
                         .Where(set => set != null)) {
                foreach (var blendshape in GetFaceTrackingBlendshapeNames(
                             set.faceTrackingBlendshapeNames
                         )) {
                    yield return blendshape;
                }
                foreach (var blendshape in GetFaceTrackingBlendshapeNames(
                             set.visemeBlendshapeNames
                         )) {
                    yield return blendshape;
                }
            }

            foreach (var behavior in (avatar.faceTrackingParameterBehaviors
                                      ?? Enumerable.Empty<VRCAvatarDescriptor.FaceTrackingParameterBehavior>())
                         .Where(behavior => behavior != null)) {
                if (!FaceTrackingUtils.TargetsBlendshape(behavior)) continue;
                if (!string.IsNullOrEmpty(behavior.targetBlendshapeName)) yield return behavior.targetBlendshapeName;
            }
        }

        private static IEnumerable<string> GetFaceTrackingBlendshapeNames(
            string[] blendshapeNames
        ) {
            if (blendshapeNames == null) yield break;

            for (var i = 0; i < blendshapeNames.Length; i++) {
                if (!string.IsNullOrEmpty(blendshapeNames[i])) yield return blendshapeNames[i];
            }
        }
#endif
    }
}
