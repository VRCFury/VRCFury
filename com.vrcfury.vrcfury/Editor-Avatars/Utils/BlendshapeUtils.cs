using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Utils {
    internal static class BlendshapeUtils {
        public static void RenameBlendshapeCurves(
            IEnumerable<VFAvatarController> controllers,
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
            var weights = Enumerable.Range(0, blendshapeCount)
                .Select(skin.GetBlendShapeWeight)
                .ToArray();

            var mesh = skin.GetMutableMesh("Renamed blendshapes for face tracking", forceClone: true);
            mesh.ClearBlendShapes();
            var frames = new BlendshapeFrameBuffer(mesh.vertexCount);
            for (var index = 0; index < blendshapeCount; index++) {
                var originalName = originalMesh.GetBlendShapeName(index);
                var name = renames.TryGetValue(originalName, out var renamed) ? renamed : originalName;
                frames.CopyShape(originalMesh, index, mesh, name);
                skin.SetBlendShapeWeight(index, weights[index]);
            }
            mesh.Dirty();
        }

        internal class BlendshapeFrameBuffer {
            private readonly Vector3[] positions;
            private readonly Vector3[] normals;
            private readonly Vector3[] tangents;

            public BlendshapeFrameBuffer(int vertexCount) {
                positions = new Vector3[vertexCount];
                normals = new Vector3[vertexCount];
                tangents = new Vector3[vertexCount];
            }

            public void CopyShape(Mesh source, int index, Mesh destination, string name) {
                var count = source.GetBlendShapeFrameCount(index);
                for (var frame = 0; frame < count; frame++) {
                    source.GetBlendShapeFrameVertices(index, frame, positions, normals, tangents);
                    destination.AddBlendShapeFrame(
                        name,
                        source.GetBlendShapeFrameWeight(index, frame),
                        positions,
                        normals,
                        tangents
                    );
                }
            }
        }

    }
}
