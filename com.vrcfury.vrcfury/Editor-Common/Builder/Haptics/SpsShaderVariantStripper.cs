using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Builder.Haptics {
    internal class SpsShaderVariantStripper : IPreprocessShaders {
        public int callbackOrder => 0;

        private static readonly string[] BaseKeywords = { "VERTEXLIGHT_ON" };
        private static readonly string[] AddKeywords = {
            "POINT",
            "DIRECTIONAL",
            "SPOT",
            "POINT_COOKIE",
            "DIRECTIONAL_COOKIE",
            "SHADOWS_DEPTH",
            "SHADOWS_CUBE",
            "SHADOWS_SOFT"
        };

        public void OnProcessShader(
            Shader shader,
            ShaderSnippetData snippet,
            IList<ShaderCompilerData> data
        ) {
            if (!shader.name.Contains("/SPSPatched/Poiyomi/") || snippet.shaderType != ShaderType.Vertex
                || (snippet.passType != PassType.ForwardBase && snippet.passType != PassType.ForwardAdd)) return;
            var pass = ShaderUtil.GetShaderData(shader)
                .GetSerializedSubshader((int)snippet.pass.SubshaderIndex)
                .GetPass((int)snippet.pass.PassIndex);
            // Fur and tessellation can consume lighting keywords in later geometry stages.
            if (pass.HasShaderStage(ShaderType.Geometry) || pass.HasShaderStage(ShaderType.Hull)
                || pass.HasShaderStage(ShaderType.Domain)) return;

            var remove = snippet.passType == PassType.ForwardBase ? BaseKeywords : AddKeywords;
            // Poiyomi normally uses these only in frag. This intentionally does not
            // support 11.x's optional vertex-stage "Ignore Directional Lights" setting.
            // The normalized programs rely on Unity's normal, non-strict variant fallback.
            var seen = new HashSet<(GraphicsTier, ShaderRequirements, BuildTarget, PlatformKeywordSet, string)>();
            for (var i = data.Count - 1; i >= 0; i--) {
                var entry = data[i];
                if (entry.shaderCompilerPlatform != ShaderCompilerPlatform.D3D) continue;
                foreach (var name in remove) entry.shaderKeywordSet.Disable(new ShaderKeyword(name));
                // Keep screen-space shadows, stereo, instancing, and every unrelated keyword.
                var keywords = string.Join(" ", entry.shaderKeywordSet.GetShaderKeywords().Select(k => k.name).OrderBy(n => n));
                var key = (
                    entry.graphicsTier,
                    entry.shaderRequirements,
                    entry.buildTarget,
                    entry.platformKeywordSet,
                    keywords
                );
                if (!seen.Add(key)) data.RemoveAt(i);
                else data[i] = entry;
            }
        }
    }
}
