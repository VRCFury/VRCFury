using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsShaderVariantStripperTest : IPreprocessShaders {
        private static readonly Dictionary<string, string[][]> captured = new Dictionary<string, string[][]>();
        private static string testId;
        public int callbackOrder => int.MaxValue;

        public void OnProcessShader(
            Shader shader,
            ShaderSnippetData snippet,
            IList<ShaderCompilerData> data
        ) {
            if (testId == null || !shader.name.EndsWith(testId)) return;
            captured[shader.name + "/" + snippet.pass.PassIndex + "/" + snippet.shaderType] = data
                .Select(entry => entry.shaderKeywordSet.GetShaderKeywords().Select(k => k.name).OrderBy(n => n).ToArray())
                .Distinct(new KeywordComparer())
                .ToArray();
        }

        private class KeywordComparer : IEqualityComparer<string[]> {
            public bool Equals(string[] a, string[] b) => a.SequenceEqual(b);
            public int GetHashCode(string[] obj) => string.Join(" ", obj).GetHashCode();
        }

        [Test]
        [Platform("Win")]
        public void StripsOnlyOrdinaryPatchedPoiyomiVertexLightingVariants() {
            testId = Guid.NewGuid().ToString("N");
            var folder = "Assets/SpsVariantTest_" + testId;
            var output = Path.Combine(Path.GetTempPath(), "SpsVariantTest_" + testId);
            var names = new[] {
                "Hidden/SPSPatched/Poiyomi/" + testId,
                "Hidden/Locked/SPSPatched/Poiyomi/" + testId,
                ".poiyomi/Test/" + testId,
                "Hidden/SPSPatched/Other/" + testId
            };
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            Directory.CreateDirectory(output);
            try {
                var assets = new List<string>();
                foreach (var name in names) {
                    var path = folder + "/" + assets.Count + ".shader";
                    var passes = Pass("ForwardBase", "#pragma multi_compile_fwdbase", false)
                        + Pass("ForwardAdd", "#pragma multi_compile_fwdadd_fullshadows", false)
                        + Pass("ShadowCaster", "#pragma multi_compile_shadowcaster", false)
                        + Pass("ForwardAdd", "#pragma multi_compile_fwdadd_fullshadows", true);
                    File.WriteAllText(path, "Shader \"" + name + "\" { SubShader { " + passes + " } }");
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    assets.Add(path);
                }
                var manifest = BuildPipeline.BuildAssetBundles(
                    output,
                    new[] {
                        new AssetBundleBuild {
                            assetBundleName = "variants",
                            assetNames = assets.ToArray()
                        }
                    },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    BuildTarget.StandaloneWindows64
                );
                Assert.That(manifest, Is.Not.Null);
                foreach (var name in names.Take(2)) {
                    var baseVertex = captured[name + "/0/Vertex"];
                    var addVertex = captured[name + "/1/Vertex"];
                    Assert.That(baseVertex.SelectMany(k => k), Does.Not.Contain("VERTEXLIGHT_ON"));
                    foreach (var keyword in new[] {
                        "POINT",
                        "DIRECTIONAL",
                        "SPOT",
                        "POINT_COOKIE",
                        "DIRECTIONAL_COOKIE",
                        "SHADOWS_DEPTH",
                        "SHADOWS_CUBE",
                        "SHADOWS_SOFT"
                    }) {
                        Assert.That(addVertex.SelectMany(k => k), Does.Not.Contain(keyword));
                    }
                    Assert.That(addVertex.Any(k => k.Contains("SHADOWS_SCREEN")), Is.True);
                    Assert.That(addVertex.Any(k => k.Contains("SPS_TEST_UNRELATED")), Is.True);
                    Assert.That(addVertex.Any(k => !k.Contains("SPS_TEST_UNRELATED")), Is.True);
                    // Fragment programs, ShadowCaster, and the geometry pass remain identical.
                    foreach (var stage in new[] {
                        "0/Fragment",
                        "1/Fragment",
                        "2/Vertex",
                        "2/Fragment",
                        "3/Vertex",
                        "3/Geometry",
                        "3/Fragment"
                    }) {
                        Assert.That(
                            captured[name + "/" + stage].Select(k => string.Join(" ", k)),
                            Is.EquivalentTo(captured[names[2] + "/" + stage].Select(k => string.Join(" ", k))),
                            stage
                        );
                    }
                    Assert.That(
                        addVertex.Length,
                        Is.LessThan(captured[names[2] + "/1/Vertex"].Length)
                    );
                }
                foreach (var stage in new[] { "0/Vertex", "1/Vertex" }) {
                    Assert.That(
                        captured[names[3] + "/" + stage].Select(k => string.Join(" ", k)),
                        Is.EquivalentTo(captured[names[2] + "/" + stage].Select(k => string.Join(" ", k)))
                    );
                }
            } finally {
                testId = null;
                captured.Clear();
                AssetDatabase.DeleteAsset(folder);
                Directory.Delete(output, true);
            }
        }

        private static string Pass(string lightMode, string pragma, bool geometry) {
            return @"
Pass {
    Name ""RepeatedName""
    Tags { ""LightMode""=""" + lightMode + @""" }
    CGPROGRAM
    #pragma target 4.5
    #pragma vertex vert
    #pragma fragment frag
    #pragma multi_compile _ SPS_TEST_UNRELATED
    #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED
    " + pragma + (geometry ? "\n#pragma geometry geom\n" : "") + @"
    #include ""UnityCG.cginc""
    struct Output { float4 pos : SV_POSITION; };
    Output vert(appdata_base input) {
        Output output;
        output.pos = UnityObjectToClipPos(input.vertex);
        return output;
    }
    [maxvertexcount(3)]
    void geom(triangle Output input[3], inout TriangleStream<Output> stream) {
        for (int i = 0; i < 3; i++) stream.Append(input[i]);
    }
    float4 frag(Output input) : SV_Target { return 1; }
    ENDCG
}
";
        }
    }
}
