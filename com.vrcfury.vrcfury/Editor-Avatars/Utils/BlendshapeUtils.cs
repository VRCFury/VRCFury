using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Utils {
    internal static class BlendshapeUtils {
        public static void RenameBlendshapeCurves(
            IEnumerable<ControllerManager> controllers,
            SkinnedMeshRenderer skin,
            IDictionary<string, string> renames
        ) {
            if (renames.Count == 0) return;
            foreach (var controller in controllers) {
                foreach (var clip in controller.GetClips()) {
                    var changedBindings = new List<(VFBinding binding, VFBinding newBinding, AnimationCurve curve)>();
                    foreach (var binding in clip.GetFloatBindings()) {
                        if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                        if (!binding.Targets(skin.owner())) continue;
                        if (!binding.propertyName.StartsWith("blendShape.")) continue;
                        var oldName = binding.propertyName.Substring(11);
                        if (!renames.TryGetValue(oldName, out var newName)) continue;
                        changedBindings.Add((
                            binding,
                            binding.WithPropertyName("blendShape." + newName),
                            clip.GetFloatCurve(binding)
                        ));
                    }
                    foreach (var (binding, _, _) in changedBindings)
                        clip.SetCurve(binding, null);
                    foreach (var (_, newBinding, curve) in changedBindings)
                        clip.SetCurve(newBinding, curve);
                }
            }
        }

        public static void RenameBlendshapes(SkinnedMeshRenderer skin, IDictionary<string, string> renames) {
            if (renames.Count == 0) return;
            var originalMesh = skin.GetMesh();
            if (originalMesh == null) return;
            var blendshapeCount = originalMesh.blendShapeCount;
            var blendshapeNames = Enumerable.Range(0, blendshapeCount)
                .Select(index => originalMesh.GetBlendShapeName(index))
                .ToArray();
            var weights = Enumerable.Range(0, blendshapeCount)
                .Select(skin.GetBlendShapeWeight)
                .ToArray();
            var frames = Enumerable.Range(0, blendshapeCount)
                .Select(index => new BlendshapeFrames(originalMesh, index))
                .ToArray();

            var mesh = skin.GetMutableMesh("Needed to rename blendshapes for face tracking", forceClone: true);
            mesh.ClearBlendShapes();
            for (var index = 0; index < blendshapeCount; index++) {
                var name = renames.TryGetValue(blendshapeNames[index], out var renamed) ? renamed : blendshapeNames[index];
                frames[index].SaveTo(mesh, name);
                skin.SetBlendShapeWeight(index, weights[index]);
            }
            mesh.Dirty();
        }

        private class BlendshapeFrames {
            private readonly List<(float weight, Vector3[] vertices, Vector3[] normals, Vector3[] tangents)> frames = new();

            public BlendshapeFrames(Mesh mesh, int index) {
                for (var frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++) {
                    var vertices = new Vector3[mesh.vertexCount];
                    var normals = new Vector3[mesh.vertexCount];
                    var tangents = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(index, frame, vertices, normals, tangents);
                    frames.Add((mesh.GetBlendShapeFrameWeight(index, frame), vertices, normals, tangents));
                }
            }

            public void SaveTo(Mesh mesh, string name) {
                foreach (var (weight, vertices, normals, tangents) in frames)
                    mesh.AddBlendShapeFrame(name, weight, vertices, normals, tangents);
            }
        }

    }
}
