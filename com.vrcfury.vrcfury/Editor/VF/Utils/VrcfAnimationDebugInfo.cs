using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Inspector;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Utils {
    internal static class VrcfAnimationDebugInfo {
        public static List<VisualElement> BuildDebugInfo(
            IEnumerable<AnimatorController> controllers,
            [CanBeNull] VFGameObject avatarObject,
            VFGameObject componentObject,
            Func<string, string> rewritePath = null,
            Action<string> addPathRewrite = null
        ) {
            var bindings = new HashSet<EditorCurveBinding>();
            foreach (var c in controllers) {
                var controller = new VFController(c);
                foreach (var state in new AnimatorIterator.States().From(controller)) {
                    bindings.UnionWith(new AnimatorIterator.Clips().From(state)
                        .SelectMany(clip => clip.GetAllBindings())
                    );
                }
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
                bindings.UnionWith(controller.layers
                    .SelectMany(c => c.allBehaviours)
                    .OfType<VRCAnimatorPlayAudio>()
                    .Select(audio => audio.SourcePath)
                    .Where(path => path != "")
                    .Select(path => EditorCurveBinding.FloatCurve(path, typeof(GameObject), "animatorPlayAudio"))
                );
#endif
            }

            return BuildDebugInfo(bindings, avatarObject, componentObject, rewritePath, true, addPathRewrite);
        }
        
        public static List<VisualElement> BuildDebugInfo(
            IEnumerable<EditorCurveBinding> bindings,
            [CanBeNull] VFGameObject avatarObject,
            VFGameObject componentObject,
            Func<string,string> rewritePath = null,
            bool isController = false,
            Action<string> addPathRewrite = null
        ) {
            if (avatarObject == null) avatarObject = componentObject.root;
            var nonRewriteSafeBindings = new HashSet<string>();
            var outsidePrefabBindings = new HashSet<string>();
            var missingBindings = new HashSet<string>();
            var autofixPrefixes = new HashSet<string>();

            var cr = new ClipRewritersService(avatarObject);
            AnimationRewriter nearestRewriter = cr.CreateNearestMatchPathRewriter(
                componentObject,
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
                if (IsProbablyIgnoredBinding(path)) {
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
                    missingBindings.Add(debugPath);
                } else {
                    var foundFullPath = nearestRewriter?.RewritePath(path);
                    if (foundFullPath == null) {
                        missingBindings.Add(debugPath);
                    } else {
                        var foundObject = avatarObject.Find(foundFullPath);
                        if (foundObject != null) {
                            var nearestBinding = binding;
                            nearestBinding.path = foundFullPath;
                            usedBindings.Add(nearestBinding);
                            if (foundObject == componentObject) {
                                nonRewriteSafeBindings.Add(debugPath);
                                autofixPrefixes.Add(componentObject.GetPath(avatarObject));
                            } else if (foundObject.IsChildOf(componentObject)) {
                                nonRewriteSafeBindings.Add(debugPath);
                                var partInsideComponent = "/" + foundObject.GetPath(componentObject);
                                if (path.EndsWith(partInsideComponent)) {
                                    autofixPrefixes.Add(path.Substring(0, path.Length - partInsideComponent.Length));
                                }
                            } else {
                                outsidePrefabBindings.Add(debugPath);
                            }
                        }
                    }
                }
            }
            
            var warnings = new List<VisualElement>();
            
            var inNonAnimatedPhysbones = avatarObject.GetComponentsInSelfAndChildren<VRCPhysBone>()
                .Where(physbone => !physbone.isAnimated)
                .Select(physbone => physbone.GetRootTransform().asVf())
                .SelectMany(root => root.GetSelfAndAllChildren())
                .ToImmutableHashSet();
            var badPhysboneTransforms = new HashSet<string>();
            foreach (var binding in usedBindings) {
                if (binding.type != typeof(Transform)) continue;
                var obj = avatarObject.Find(binding.path);
                if (obj == null) continue;
                if (!inNonAnimatedPhysbones.Contains(obj)) continue;
                badPhysboneTransforms.Add(binding.path);
            }
            if (badPhysboneTransforms.Any()) {
                warnings.Add(VRCFuryEditorUtils.Warn(
                    $"You're animating these transforms, but they are within physbones that are not marked as Is Animated.\n" +
                    badPhysboneTransforms.OrderBy(a => a).Join('\n')
                ));
            }

            var nonAnimatedPoi = new VFMultimapSet<string,string>();
            foreach (var binding in usedBindings) {
                if (AvatarBindingStateService.TryParseMaterialProperty(binding, out var propertyName)) {
                    var obj = avatarObject.Find(binding.path);
                    if (obj == null) continue;
                    var renderer = obj.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    var bad = renderer.sharedMaterials
                        .NotNull()
                        .Distinct()
                        .Where(m => PoiyomiUtils.IsPoiyomiWithPropNonanimated(m, propertyName))
                        .Select(m => m.name)
                        .ToList();
                    foreach (var matName in bad) {
                        nonAnimatedPoi.Put(propertyName, matName + " on " + obj.name);
                    }
                }
            }
            if (nonAnimatedPoi.Any()) {
                var lines = new List<string>();
                foreach (var propertyName in nonAnimatedPoi.GetKeys().OrderBy(a => a)) {
                    lines.Add(propertyName);
                    foreach (var mat in nonAnimatedPoi.Get(propertyName).OrderBy(a => a)) {
                        lines.Add("  " + mat);
                    }
                }
                warnings.Add(VRCFuryEditorUtils.Warn(
                    $"You're animating these properties on materials using Poiyomi, but the materials don't have the property set as Animated. " +
                    $"Check the right click menu of the property on the material:\n" +
                    lines.Join('\n')
                ));
            }

            var thisName = isController ? "the controller" : "this clip";

            if (missingBindings.Any()) {
                var msg = $"These paths are animated in {thisName}, but not found in your avatar! Thus, they won't do anything!";
                if (addPathRewrite != null) msg += " You may need to use 'Path Rewrite Rules' in the Advanced Settings to fix them if your avatar's objects are in a different location.";
                msg += "\n";
                msg += missingBindings.OrderBy(path => path).Join('\n');
                warnings.Add(VRCFuryEditorUtils.Error(msg));
            }
            if (nonRewriteSafeBindings.Any()) {
                var el = new VisualElement();
                el.Add(VRCFuryEditorUtils.WrappedLabel(
                    $"The animations provided are not rename-safe! If this object is moved or renamed, the animations will break."
                ));
                if (addPathRewrite != null && autofixPrefixes.Any()) {
                    el.Add(VRCFuryEditorUtils.WrappedLabel(
                        "\nClick Auto-Fix to add a Rewrite Path rule to this Full Controller which will make the animations rename-safe."
                    ));
                    el.Add(new Button(() => {
                        if (!DialogUtils.DisplayDialog(
                                "VRCFury",
                                "These Path Rewrite rules are being added to the Full Controller component:\n" +
                                autofixPrefixes.Select(prefix => $"'{prefix}' -> ''").Join('\n'),
                                "Ok",
                                "Cancel"
                        )) {
                            return;
                        }
                        foreach (var p in autofixPrefixes) {
                            addPathRewrite(p);
                        }
                    }) { text = "Auto-Fix" });
                }

                warnings.Add(VRCFuryEditorUtils.Warn(el));
            }
            if (outsidePrefabBindings.Any()) {
                var msg = $"This prefab is not self-contained! It animates things outside of this object.";
                msg += "\n";
                msg += outsidePrefabBindings.OrderBy(path => path).Join('\n');
                warnings.Add(VRCFuryEditorUtils.Warn(msg));
            }

            var overLimitConstraints = new HashSet<string>();
            foreach (var binding in usedBindings) {
                if (binding.IsOverLimitConstraint(out var slotNum)) {
                    overLimitConstraints.Add($"Source {slotNum} on {binding.path}");
                }
            }
            if (overLimitConstraints.Any()) {
                warnings.Add(VRCFuryEditorUtils.Warn(
                    "VRC Constraints can only have the first 16 source animated, but you are animating a constraint source above this limit!" +
                    " This will break these animations if this avatar is upgraded to VRC Constraints.\n" + overLimitConstraints.Join('\n')));
            }

            return warnings;
        }

        private static bool IsProbablyIgnoredBinding(string bindingPath) {
            if (bindingPath == "__vrcf_length") return true;
            if (bindingPath == "_buffer") return true;
            if (bindingPath.EndsWith("/Idle Camera")) return true; // dumb gogoloco thing that we don't want to show warnings for
            if (bindingPath.Contains("/")) return false;
            var normalized = new string(bindingPath.ToLower().Where(c => char.IsLetter(c)).ToArray());
            return normalized.Contains("invalid")
                   || normalized.Contains("ignore")
                   || normalized.Contains("av3")
                   || normalized.Contains("donothing");
        }
    }
}
