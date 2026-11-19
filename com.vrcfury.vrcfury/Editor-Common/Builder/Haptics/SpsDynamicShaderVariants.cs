using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

namespace VF.Builder.Haptics {
    /**
     * Unity 2022 can schedule a separate compile job for every on/off combination of dynamic
     * shader keywords. For example, Poiyomi's three dynamic fog keywords produce eight jobs
     * for the same GPU program. That one program handles all eight combinations at runtime.
     *
     * Before Unity compiles each pass and stage of an SPS-patched shader, keep the first job
     * for each combination of static keywords and compiler settings. Unity tells us which
     * keywords are dynamic, so this works for any shader that SPS patches.
     */
    internal class SpsDynamicShaderVariants : IPreprocessShaders {
        public int callbackOrder => int.MaxValue;

        public void OnProcessShader(
            Shader shader,
            ShaderSnippetData snippet,
            IList<ShaderCompilerData> variants
        ) {
            if (variants.Count < 2) return;
            var name = shader.name;
            if (!name.StartsWith(SpsPatcher.ShaderNamePrefix, StringComparison.Ordinal)
                && !name.StartsWith(SpsPatcher.LockedShaderNamePrefix, StringComparison.Ordinal)) return;

            var dynamicKeywords = ShaderUtil.GetPassKeywords(shader, snippet.pass, snippet.shaderType)
                .Where(keyword => keyword.isDynamic)
                .Select(keyword => keyword.name)
                .ToHashSet();
            if (dynamicKeywords.Count == 0) return;

            var seen = new HashSet<(ShaderCompilerData, string)>();
            var kept = 0;
            for (var i = 0; i < variants.Count; i++) {
                var variant = variants[i];
                var staticKeywords = variant.shaderKeywordSet.GetShaderKeywords()
                    .Select(keyword => keyword.name)
                    .Where(keyword => !dynamicKeywords.Contains(keyword))
                    .OrderBy(name => name, StringComparer.Ordinal);
                var settings = variant;
                // Compare keywords by their static names and all other settings by value.
                // Only this comparison copy drops the keyword set. Unity compiles the original job.
                settings.shaderKeywordSet = default;
                if (!seen.Add((settings, string.Join(" ", staticKeywords)))) continue;
                if (kept != i) variants[kept] = variant;
                kept++;
            }
            while (variants.Count > kept) variants.RemoveAt(variants.Count - 1);
        }
    }
}
