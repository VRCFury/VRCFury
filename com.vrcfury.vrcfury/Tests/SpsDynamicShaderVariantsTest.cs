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
using VF.Builder.Haptics;

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsDynamicShaderVariantsTest : IPreprocessShaders {
        private static string testId;
        private static readonly List<(string shader, object[] expected, object[] actual)> results = new();
        public int callbackOrder => 0;

        public void OnProcessShader(
            Shader shader,
            ShaderSnippetData snippet,
            IList<ShaderCompilerData> variants
        ) {
            if (testId == null || !shader.name.EndsWith("/" + testId, StringComparison.Ordinal) || variants.Count == 0) return;
            var dynamicBranch = new LocalKeyword(shader, "TEST_BRANCH");
            var staticFeature = new LocalKeyword(shader, "TEST_FEATURE");

            // Model an earlier stripper that kept only the branch-on job for one static variant.
            var jobs = variants.ToList();
            if (jobs.Any(v => v.shaderKeywordSet.IsEnabled(dynamicBranch))) {
                jobs.RemoveAll(v => v.shaderKeywordSet.IsEnabled(staticFeature)
                    && !v.shaderKeywordSet.IsEnabled(dynamicBranch));
            }
            var isPatched = !shader.name.StartsWith("Hidden/Control/", StringComparison.Ordinal);
            object Describe(ShaderCompilerData job) {
                var names = job.shaderKeywordSet.GetShaderKeywords()
                    .Select(k => k.name)
                    .Where(n => !isPatched || n != "TEST_BRANCH")
                    .OrderBy(n => n, StringComparer.Ordinal);
                return (
                    job.buildTarget,
                    job.shaderCompilerPlatform,
                    job.graphicsTier,
                    job.shaderRequirements,
                    job.platformKeywordSet,
                    string.Join(" ", names)
                );
            }
            var expected = jobs.Select(Describe).ToArray();
            if (isPatched) expected = expected.Distinct().ToArray();
            new SpsDynamicShaderVariants().OnProcessShader(shader, snippet, jobs);
            results.Add((shader.name, expected, jobs.Select(Describe).ToArray()));
            variants.Clear(); // This test only needs the build callback.
        }

        [Test]
        public void KeepsOneJobForEachStaticVariantOfPatchedShaders() {
            testId = Guid.NewGuid().ToString("N");
            var folder = "Assets/SpsDynamicVariants_" + testId;
            var output = Path.GetFullPath("Temp/SpsDynamicVariants_" + testId);
            try {
                Directory.CreateDirectory(output);
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                var paths = new List<string>();
                foreach (var prefix in new[] {
                    "Hidden/SPSPatched/",
                    "Hidden/Locked/SPSPatched/",
                    "Hidden/Control/"
                }) {
                    var path = folder + "/Test" + paths.Count + ".shader";
                    var source = "Shader \"" + prefix + testId + "\" { SubShader { Pass {\n" + @"
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ TEST_FEATURE
#pragma dynamic_branch_local __ TEST_BRANCH
float4 vert(float4 pos : POSITION) : SV_POSITION { return pos; }
float4 frag() : SV_Target {
    #ifdef TEST_FEATURE
        return TEST_BRANCH ? 1 : 0.5;
    #else
        return TEST_BRANCH ? 0.3 : 0;
    #endif
}
ENDCG
} } }";
                    File.WriteAllText(path, source);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    paths.Add(path);
                }
                var bundle = new AssetBundleBuild {
                    assetBundleName = "variants",
                    assetNames = paths.ToArray()
                };
                var manifest = BuildPipeline.BuildAssetBundles(
                    output,
                    new[] { bundle },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    EditorUserBuildSettings.activeBuildTarget
                );
                Assert.That(manifest, Is.Not.Null);
                Assert.That(results.Select(r => r.shader).Distinct().Count(), Is.EqualTo(paths.Count));
                foreach (var (shader, expected, actual) in results) {
                    Assert.That(actual, Is.EquivalentTo(expected), shader);
                }
            } finally {
                testId = null;
                results.Clear();
                AssetDatabase.DeleteAsset(folder);
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }
    }
}
