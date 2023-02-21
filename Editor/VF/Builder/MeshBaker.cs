using System.Linq;
using UnityEngine;

namespace VF.Builder {
    public static class MeshBaker {
        /**
         * Returns a baked mesh, where the vertices are in local space in relation to the renderer's transform.
         * This is true even for skinned meshes (where in other places, the offsets are commonly in relation to the root bone)
         */
        public static BakedMesh BakeMesh(Renderer renderer, Transform origin = null) {
            Mesh mesh;
            if (renderer is SkinnedMeshRenderer skin) {
                // If the skinned mesh doesn't have any bones attached, it's treated like a regular mesh and BakeMesh applies no transforms
                // So we have to skip calling BakeMesh, because otherwise we'd apply the inverse scale inappropriately and it would be too small.
                var actuallySkinned = skin.bones.Any(b => b != null);
                if (actuallySkinned) {
                    var temporaryMesh = new Mesh();
                    skin.BakeMesh(temporaryMesh);
                    var verts = temporaryMesh.vertices;
                    var scale = skin.transform.lossyScale;
                    var inverseScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                    for (var i = 0; i < verts.Length; i++) {
                        verts[i].Scale(inverseScale);
                    }
                    temporaryMesh.vertices = verts;
                    mesh = temporaryMesh;
                } else {
                    mesh = skin.sharedMesh;
                }
            } else {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                mesh = meshFilter ? meshFilter.sharedMesh : null;
            }

            if (!mesh) return null;

            Vector3[] vertices;
            if (origin) {
                vertices = mesh.vertices
                    .Select(v => origin.InverseTransformPoint(renderer.transform.TransformPoint(v))).ToArray();
            } else {
                vertices = mesh.vertices;
            }

            return new BakedMesh() {
                vertices = vertices,
                normals = mesh.normals
            };
        }

        public class BakedMesh {
            public Vector3[] vertices;
            public Vector3[] normals;
        }
    }
}
