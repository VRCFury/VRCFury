using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Inspector;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    internal static class VrcfAnimationDebugInfo {
        public static List<VisualElement> BuildDebugInfo(
            IList<AnimatorController> controllers,
            [CanBeNull] VFGameObject avatarObject,
            VFGameObject componentObject,
            Func<string, string> rewritePath = null,
            bool suggestPathRewrites = false
        ) {
            var bindings = new HashSet<EditorCurveBinding>();
            foreach (var c in controllers) {
                var controller = (VFController)c;
                foreach (var state in new AnimatorIterator.States().From(controller)) {
                    bindings.UnionWith(new AnimatorIterator.Clips().From(state)
                        .SelectMany(clip => clip.GetAllBindings())
                    );
                }
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
                bindings.UnionWith(new AnimatorIterator.Behaviours().From(controller)
                    .OfType<VRCAnimatorPlayAudio>()
                    .Select(audio => audio.SourcePath)
                    .Where(path => path != "")
                    .Select(path => EditorCurveBinding.FloatCurve(path, typeof(GameObject), "animatorPlayAudio"))
                );
#endif
            }

            return BuildDebugInfo(bindings, avatarObject, componentObject, rewritePath, true, suggestPathRewrites);
        }
        
        public static List<VisualElement> BuildDebugInfo(
            IEnumerable<EditorCurveBinding> bindings,
            [CanBeNull] VFGameObject avatarObject,
            VFGameObject componentObject,
            Func<string,string> rewritePath = null,
            bool isController = false,
            bool suggestPathRewrites = false
        ) {
            if (avatarObject == null) avatarObject = componentObject.root;
            var missingFromBase = new HashSet<string>();
            var missingFromAvatar = new HashSet<string>();

            AnimationRewriter nearestRewriter = ClipRewriter.CreateNearestMatchPathRewriter(
                animObject: componentObject,
                rootObject: avatarObject,
                rootBindingsApplyToAvatar: false,
                nullIfNotFound: true
            );

            var usedBindings = new HashSet<EditorCurveBinding>();
            foreach (var binding in bindings) {
                var path = binding.path;
                if (rewritePath != null) path = rewritePath(path);
                if (path == null) {
                    // binding was deleted by rules :)
                    continue;
                }
                if (path.ToLower().Contains("ignore")) {
                    continue;
                }
                if (componentObject.Find(path) != null) {
                    // Found as relative path. All good!
                    usedBindings.Add(binding);
                    continue;
                }

                var debugPath = binding.path;
                if (binding.path != path) debugPath += " -> " + path;
                if (avatarObject == componentObject) {
                    missingFromAvatar.Add(debugPath);
                } else {
                    var nearestPath = nearestRewriter?.RewritePath(path);
                    if (nearestPath == null) {
                        missingFromAvatar.Add(debugPath);
                    } else {
                        var nearestBinding = binding;
                        nearestBinding.path = nearestPath;
                        usedBindings.Add(nearestBinding);
                        missingFromBase.Add(debugPath);
                    }
                }
            }
            
            var warnings = new List<VisualElement>();
            
            var badMats = new VFMultimapSet<string,string>();
            foreach (var binding in usedBindings) {
                if (AvatarBindingStateService.TryParseMaterialProperty(binding, out var propertyName)) {
                    var obj = avatarObject.Find(binding.path);
                    if (obj == null) continue;
                    var renderer = obj.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    var bad = renderer.sharedMaterials
                        .NotNull()
                        .Distinct()
                        .Where(m => IsPoiyomiWithPropNonanimated(m, propertyName))
                        .Select(m => m.name)
                        .ToList();
                    foreach (var matName in bad) {
                        badMats.Put(propertyName, matName + " on " + obj.name);
                    }
                }
            }

            if (badMats.Any()) {
                var lines = new List<string>();
                foreach (var propertyName in badMats.GetKeys().OrderBy(a => a)) {
                    lines.Add(propertyName);
                    foreach (var mat in badMats.Get(propertyName).OrderBy(a => a)) {
                        lines.Add("  " + mat);
                    }
                }
                warnings.Add(VRCFuryEditorUtils.Warn(
                    $"You're animating these properties on materials using Poiyomi, but the materials don't have the property set as Animated. " +
                    $"Check the right click menu of the property on the material:\n" +
                    string.Join("\n", lines)
                ));
            }

            var thisName = isController ? "the controller" : "this clip";

            if (missingFromAvatar.Any()) {
                var msg = $"These paths are animated in {thisName}, but not found in your avatar! Thus, they won't do anything!";
                if (suggestPathRewrites) msg += " You may need to use 'Path Rewrite Rules' in the Advanced Settings to fix them if your avatar's objects are in a different location.";
                msg += "\n";
                msg += string.Join("\n", missingFromAvatar.OrderBy(path => path));
                warnings.Add(VRCFuryEditorUtils.Error(msg));
            }
            if (missingFromBase.Any() && suggestPathRewrites) {
                var msg = $"These paths are animated in the {thisName}, but not found as children of this object.";
                if (suggestPathRewrites) msg +=
                    " If you want this prop to be reusable, you should use 'Path Rewrite Rules' in the Advanced Settings to rewrite " +
                    "these paths so they work with how the objects are located within this object.";
                msg += "\n";
                msg += string.Join("\n", missingFromBase.OrderBy(path => path));
                warnings.Add(VRCFuryEditorUtils.Warn(msg));
            }

            return warnings;
        }

        private static bool IsPoiyomiWithPropNonanimated(Material m, string propertyName) {
            if (m.GetTag(propertyName + "Animated", false, "") != "") return false;

            if (IsPoiyomiWithProp(m.shader, propertyName)) return true;
            var origShaderName = m.GetTag("OriginalShader", false, "");
            if (IsPoiyomiWithProp(Shader.Find(origShaderName), propertyName)) return true;
            var origShaderGuid = m.GetTag("OriginalShaderGUID", false, "");
            if (IsPoiyomiWithProp(VRCFuryAssetDatabase.LoadAssetByGuid<Shader>(origShaderGuid), propertyName)) return true;
            return false;
        }

        private static bool IsPoiyomiWithProp(Shader shader, string propertyName) {
            return shader != null
                   && shader.name.ToLower().Contains("poiyomi")
                   && shader.GetPropertyType(propertyName) != null;
        }
    }
}
