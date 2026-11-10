using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using VF.Exceptions;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder.Haptics {
    internal static class TpsUtils {
        private static readonly string TpsPenetratorEnabledName = "_TPSPenetratorEnabled";

        public static bool IsTps(Material mat) {
            if (mat == null) return false;
            var shader = mat.shader;
            if (shader == null) return false;
            return mat.GetPropertyType(TpsPenetratorEnabledName) != null &&
                   mat.TryGetFloatFast(TpsPenetratorEnabledName, out var enabled) &&
                   enabled > 0;
        }

        public static bool IsLocked(Material mat) {
            return mat != null && mat.shader && mat.shader.name.ToLower().Contains("locked");
        }

        public static Quaternion GetTpsRotation(Material mat) {
            const string forwardProperty = "_TPS_PenetratorForward";
            if (mat.GetPropertyType(forwardProperty) != null) {
                mat.TryGetVectorFast(forwardProperty, out var c);
                return Quaternion.LookRotation(new Vector3(c.x, c.y, c.z));
            }
            return Quaternion.identity;
        }

        public static float? GetAnimatedEnabled(Renderer renderer) {
            foreach (var material in renderer.sharedMaterials) {
                if (IsTps(material) && material.TryGetFloatFast("_TPS_AnimatedToggle", out var enabled)) return enabled;
            }
            return null;
        }

        public static Texture2D GetMask(Material material, Mesh mesh) {
            if (mesh == null || mesh.vertexCount == 0) return null;
            var weights = GetVertexMask(material, mesh);
            if (weights == null) return null;
            var uniform = weights.All(weight => weight == weights[0]);
            var size = uniform ? 1 : 1024;
            var pixels = new Color[size * size];
            if (uniform) {
                pixels[0] = Color.Lerp(Color.white, Color.black, weights[0]);
            } else {
                var uvs = mesh.uv;
                if (uvs.Length != weights.Length)
                    throw new VRCFBuilderException("Cannot recover a TPS texture mask because the plug mesh has no UVs");
                var points = uvs.Select(uv => uv * size).ToArray();
                float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
                foreach (var submesh in Enumerable.Range(0, mesh.subMeshCount)) {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    var triangles = mesh.GetTriangles(submesh);
                    for (var index = 0; index < triangles.Length; index += 3) {
                        var ia = triangles[index]; var ib = triangles[index + 1]; var ic = triangles[index + 2];
                        var a = points[ia]; var b = points[ib]; var c = points[ic];
                        var area = Cross(b - a, c - a);
                        if (Mathf.Abs(area) < 0.00001f) continue;
                        var min = Vector2.Min(a, Vector2.Min(b, c));
                        var max = Vector2.Max(a, Vector2.Max(b, c));
                        for (var y = Mathf.Max(0, Mathf.FloorToInt(min.y)); y <= Mathf.Min(size - 1, Mathf.CeilToInt(max.y)); y++) {
                            for (var x = Mathf.Max(0, Mathf.FloorToInt(min.x)); x <= Mathf.Min(size - 1, Mathf.CeilToInt(max.x)); x++) {
                                var point = new Vector2(x, y);
                                var wb = Cross(point - a, c - a) / area;
                                var wc = Cross(b - a, point - a) / area;
                                var wa = 1 - wb - wc;
                                if (wa < 0 || wb < 0 || wc < 0) continue;
                                var excluded = 1 - (wa * weights[ia] + wb * weights[ib] + wc * weights[ic]);
                                pixels[y * size + x] = Color.Lerp(Color.black, Color.white, excluded);
                            }
                        }
                    }
                }
                // Preserve the vertex samples used by SPS. Overlapping UVs favor exclusion.
                var written = new bool[pixels.Length];
                for (var vertex = 0; vertex < points.Length; vertex++) {
                    var point = points[vertex];
                    for (var dy = 0; dy < 2; dy++) for (var dx = 0; dx < 2; dx++) {
                        var x = Mathf.Clamp(Mathf.FloorToInt(point.x) + dx, 0, size - 1);
                        var y = Mathf.Clamp(Mathf.FloorToInt(point.y) + dy, 0, size - 1);
                        var index = y * size + x;
                        var excluded = written[index] ? Mathf.Max(pixels[index].r, 1 - weights[vertex]) : 1 - weights[vertex];
                        pixels[index] = Color.Lerp(Color.black, Color.white, excluded);
                        written[index] = true;
                    }
                }
            }
            var texture = VrcfObjectFactory.CreateTexture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.name = "SPS Mask";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static float[] GetVertexMask(Material material, Mesh mesh) {
            if (!IsTps(material) || !material.TryGetTextureFast("_TPS_BakedMesh", out var source)
                || !(source is Texture2D texture)) return null;
            var readable = texture;
            try {
                if (!readable.isReadable) {
                    readable = texture.Clone();
                    readable.ForceReadable();
                }
                var output = new float[mesh.vertexCount];
                var baked = material.IsKeywordEnabled("TPS_IsSkinnedMesh")
                    || (material.TryGetFloatFast("_TPS_IsSkinnedMeshRenderer", out var isSkinned) && isSkinned > 0);
                if (baked) {
                    // TPS stores six IEEE floats per vertex, encoded as RGBA bytes in rows of 8190 texels.
                    if (texture.width < 8190 || (mesh.vertexCount * 6 - 1) / 8190 >= texture.height)
                        throw new VRCFBuilderException("TPS baked vertex texture is too small for the plug mesh");
                    var pixels = readable.GetPixels32();
                    bool IsZero(int index) {
                        var pixel = pixels[(index / 8190) * texture.width + index % 8190];
                        // Both +0 and -0 are zero; all other bit patterns represent an active normal.
                        return pixel.r == 0 && pixel.g == 0 && pixel.b == 0 && (pixel.a & 127) == 0;
                    }
                    for (var vertex = 0; vertex < output.Length; vertex++) {
                        var normal = vertex * 6 + 3;
                        output[vertex] = IsZero(normal) && IsZero(normal + 1) && IsZero(normal + 2) ? 0 : 1;
                    }
                } else {
                    var uvs = mesh.uv;
                    for (var vertex = 0; vertex < output.Length; vertex++) {
                        var uv = uvs.GetOrDefault(vertex, Vector2.zero);
                        var sample = readable.filterMode == FilterMode.Point
                            ? readable.GetPixel(Mathf.FloorToInt(uv.x * readable.width), Mathf.FloorToInt(uv.y * readable.height))
                            // GetPixelBilinear's origin is the first pixel's center; shader UVs start at its edge.
                            : readable.GetPixelBilinear(uv.x - 0.5f / readable.width, uv.y - 0.5f / readable.height);
                        var weight = sample.r;
                        if (QualitySettings.activeColorSpace == ColorSpace.Linear && GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat))
                            weight = Mathf.GammaToLinearSpace(weight);
                        output[vertex] = Mathf.Clamp01(weight);
                    }
                }
                return output;
            } finally {
                if (readable != texture) Object.DestroyImmediate(readable);
            }
        }

        public static bool HasDpsOrTpsMaterial(Renderer r) {
            return r.sharedMaterials.Any(mat => DpsUtils.IsDps(mat) || IsTps(mat));
        }
    }
}
