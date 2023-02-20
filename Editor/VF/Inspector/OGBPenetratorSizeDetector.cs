using System;
using System.Linq;
using UnityEngine;

namespace VF.Inspector {
    static class OGBPenetratorSizeDetector {
        public static Tuple<float, float, Vector3> GetAutoSize(GameObject obj, bool directOnly = false) {
            foreach (var skin in obj.GetComponents<SkinnedMeshRenderer>()) {
                var auto = GetAutoSize(skin, true);
                if (auto != null) return auto;
            }
            foreach (var renderer in obj.GetComponents<MeshRenderer>()) {
                var auto = GetAutoSize(renderer, true);
                if (auto != null) return auto;
            }
            if (directOnly) return null;
            foreach (var skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var auto = GetAutoSize(skin, true);
                if (auto != null) return auto;
            }
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>(true)) {
                var auto = GetAutoSize(renderer, true);
                if (auto != null) return auto;
            }
            foreach (var skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var auto = GetAutoSize(skin, false);
                if (auto != null) return auto;
            }
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>(true)) {
                var auto = GetAutoSize(renderer, false);
                if (auto != null) return auto;
            }
            return null;
        }

        private static Tuple<float, float, Vector3> GetAutoSize(MeshRenderer renderer, bool useMaterials) {
            var forward = Vector3.forward;
            if (useMaterials) {
                var m = renderer.sharedMaterials.Select(MaterialIsDps).FirstOrDefault(c => c != null);
                if (m == null) return null;
                forward = m.Item1;
            }
            
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (!meshFilter || !meshFilter.sharedMesh) return null;
            return GetAutoSize(renderer.gameObject, meshFilter.sharedMesh, forward);
        }

        private static Tuple<float, float, Vector3> GetAutoSize(SkinnedMeshRenderer skin, bool useMaterials) {
            var forward = Vector3.forward;
            if (useMaterials) {
                var m = skin.sharedMaterials.Select(MaterialIsDps).FirstOrDefault(c => c != null);
                if (m == null) return null;
                forward = m.Item1;
            }
            
            // If the skinned mesh doesn't have any bones attached, it's treated like a regular mesh and BakeMesh applies no transforms
            // So we have to skip calling BakeMesh, because otherwise we'd apply the inverse scale inappropriately and it would be too small.
            bool actuallySkinned = skin.bones.Any(b => b != null);
            Mesh mesh;
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

            if (!mesh) return null;
            return GetAutoSize(skin.gameObject, mesh, forward);
        }

        private static Tuple<float, float, Vector3> GetAutoSize(GameObject obj, Mesh mesh, Vector3 forward) {
            forward = forward.normalized;
            var worldScale = obj.transform.lossyScale.x;
            var length = mesh.vertices
                .Select(v => Vector3.Dot(v, forward))
                .DefaultIfEmpty(0)
                .Max() * worldScale;
            var verticesInFront = mesh.vertices
                .Where(v => Vector3.Dot(v, forward) > 0);
            var verticesInFrontCount = verticesInFront.Count();
            float radius = verticesInFront
                .Select(v => Vector3.Cross(v, forward).magnitude)
                .OrderBy(m => m)
                .Where((m, i) => i <= verticesInFrontCount*0.75)
                .DefaultIfEmpty(0)
                .Max() * worldScale;

            if (length <= 0 || radius <= 0) return null;

            return Tuple.Create(length, radius, forward);
        }
        
        public static Tuple<Vector3> MaterialIsDps(Material mat) {
            if (mat == null) return null;
            if (!mat.shader) return null;
            if (mat.shader.name == "Raliv/Penetrator") return Tuple.Create(Vector3.forward); // Raliv
            if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return Tuple.Create(Vector3.forward); // XSToon w/ Raliv
            if (mat.HasProperty("_PenetratorEnabled") && mat.GetFloat("_PenetratorEnabled") > 0) return Tuple.Create(Vector3.forward); // Poiyomi 7 w/ Raliv
            if (mat.shader.name.Contains("DPS") && mat.HasProperty("_ReCurvature")) return Tuple.Create(Vector3.forward); // UnityChanToonShader w/ Raliv
            if (mat.HasProperty("_TPSPenetratorEnabled") && mat.GetFloat("_TPSPenetratorEnabled") > 0) {
                // Poiyomi 8 w/ TPS
                var forward = Vector3.forward;
                if (mat.HasProperty("_TPS_PenetratorForward")) {
                    var c = mat.GetVector("_TPS_PenetratorForward");
                    forward = new Vector3(c.x, c.y, c.z).normalized;
                }
                return Tuple.Create(forward);
            }
            return null;
        }
    }
}