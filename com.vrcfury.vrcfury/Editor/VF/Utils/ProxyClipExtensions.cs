using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    public static class ProxyClipExtensions {
        private static string ProxyClipMagicString = "__vrchat_proxy_clip";

        public static void WriteProxyBinding(this AnimationClip clip, AnimationClip original) {
            if (original == null) return;
            var path = AssetDatabase.GetAssetPath(original);
            if (path == null) return;
            var filename = Path.GetFileName(path);
            if (!filename.StartsWith("proxy_")) return;

            int value;
            var muscleTypes = original.GetMuscleBindingTypes();
            if (muscleTypes.Contains(EditorCurveBindingExtensions.MuscleBindingType.Other)) {
                value = 1;
            } else if (muscleTypes.Count > 0) {
                value = 2;
            } else {
                return;
            }
            clip.Rewrite(AnimationRewriter.DeleteAllBindings());
            clip.SetCurve(EditorCurveBinding.DiscreteCurve(
                ProxyClipMagicString,
                typeof(GameObject),
                path
            ), new FloatOrObjectCurve(AnimationCurve.Constant(0,0,value)));
        }

        public static List<(AnimationClip,bool)> CollapseProxyBindings(this AnimationClip clip, bool removeProxyBindings = false) {
            var collectedProxies = new List<(AnimationClip, bool)>();

            var newBindings = new List<(EditorCurveBinding, FloatOrObjectCurve)>();
            foreach (var (binding,curve) in clip.GetFloatCurves()) {
                if (binding.path != ProxyClipMagicString) continue;
                var proxyClipPath = binding.propertyName;
                var proxyClip = AssetDatabase.LoadMainAssetAtPath(proxyClipPath) as AnimationClip;
                if (proxyClip == null) throw new Exception($"Failed to find proxy clip: {proxyClipPath}");
                var firstVal = curve.keys[0].value;
                collectedProxies.Add((proxyClip, firstVal == 1));
                if (removeProxyBindings) {
                    newBindings.Add((binding, null));
                }
            }
            clip.SetCurves(newBindings);

            return collectedProxies;
        }

        public static bool IsProxyBinding(this EditorCurveBinding binding) {
            return binding.path == ProxyClipMagicString;
        }
    }
}
