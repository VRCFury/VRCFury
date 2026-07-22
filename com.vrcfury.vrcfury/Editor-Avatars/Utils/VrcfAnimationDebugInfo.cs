using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Hooks;
using VF.Inspector;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Utils {
    internal static class VrcfAnimationDebugInfo {
        private class ControllerDebugInfo {
            public HashSet<EditorCurveBinding> bindings;
            public bool usesWdOff;
        }

        private static readonly AssetChangeCache<ControllerDebugInfo> controllerCache =
            new AssetChangeCache<ControllerDebugInfo>(
                typeof(AnimatorController),
                typeof(AnimatorState),
                typeof(AnimatorStateMachine),
                typeof(Motion),
                typeof(StateMachineBehaviour)
            );

        public static List<VisualElement> BuildDebugInfo(
            IEnumerable<AnimatorController> controllers,
            VFGameObject componentObject,
            Func<string, string> rewritePath = null,
            Action<string> addPathRewrite = null
        ) {
            var usesWdOff = false;
            var bindings = new HashSet<VFBinding>();
            var avatarObject = componentObject.GetAvatarRoot();
            foreach (var c in controllers) {
                var debugInfo = controllerCache.Get(c, () => BuildControllerDebugInfo(c));

                usesWdOff |= debugInfo.usesWdOff;
                var context = new VFLoadContext {
                    OwnerObject = componentObject,
                    AnimatorObject = avatarObject,
                    RewritePath = rewritePath
                };
                foreach (var binding in debugInfo.bindings) {
                    var resolved = VFResolvedObject.Load(binding.path, context, binding.type);
                    if (!resolved.HasValue) continue;
                    bindings.Add(VFBinding.From(resolved.Value, binding));
                }
            }

            var warnings = BuildDebugInfo(bindings, componentObject, true, addPathRewrite);

            if (usesWdOff) {
                warnings.Add(VRCFuryEditorUtils.Warn(
                    "This controller uses WD off!" +
                    " If you want this prop to be reusable, you should use WD on." +
                    " VRCFury will automatically convert the WD on or off to match the client's avatar," +
                    " however if WD is converted from 'off' to 'on', the 'stickiness' of properties will be lost."
                ));
            }

            return warnings;
        }

        private static ControllerDebugInfo BuildControllerDebugInfo(AnimatorController source) {
            var bindings = new HashSet<EditorCurveBinding>();
            var usesWdOff = false;
            var layers = source.layers;
            for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++) {
                var layer = layers[layerIndex];
                var syncedLayerIndex = layer.syncedLayerIndex;
                if (syncedLayerIndex >= layers.Length) continue;
                var isSynced = syncedLayerIndex >= 0 && syncedLayerIndex != layerIndex;
                var sourceLayer = isSynced ? layers[syncedLayerIndex] : layer;

                var stateMachines = new Stack<AnimatorStateMachine>();
                var seenStateMachines = new HashSet<AnimatorStateMachine>();
                stateMachines.Push(sourceLayer.stateMachine);
                while (stateMachines.Count > 0) {
                    var stateMachine = stateMachines.Pop();
                    if (stateMachine == null || !seenStateMachines.Add(stateMachine)) continue;
                    foreach (var childStateMachine in stateMachine.stateMachines) {
                        stateMachines.Push(childStateMachine.stateMachine);
                    }
                    foreach (var childState in stateMachine.states) {
                        var state = childState.state;
                        if (state == null) continue;
                        usesWdOff |= !state.writeDefaultValues;

                        var behaviours = state.behaviours;
                        if (isSynced) {
                            behaviours = layer.GetOverrideBehaviours(state) ?? behaviours;
                        }

                        foreach (var behaviour in behaviours ?? Array.Empty<StateMachineBehaviour>()) {
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
                            if (!(behaviour is VRCAnimatorPlayAudio playAudio)
                                || string.IsNullOrEmpty(playAudio.SourcePath)) continue;
                            bindings.Add(EditorCurveBinding.FloatCurve(
                                playAudio.SourcePath,
                                typeof(GameObject),
                                "animatorPlayAudio"
                            ));
#endif
                        }
                    }
                }
            }

            var clips = new Stack<AnimationClip>(source.animationClips ?? Array.Empty<AnimationClip>());
            var seenClips = new HashSet<AnimationClip>();
            while (clips.Count > 0) {
                var clip = clips.Pop();
                if (clip == null || !seenClips.Add(clip)) continue;
                var additiveClip = AnimationUtility.GetAnimationClipSettings(clip).additiveReferencePoseClip;
                if (additiveClip != null) clips.Push(additiveClip);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip)
                             .Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip))) {
                    if (binding.path == null || binding.propertyName == null || binding.type == null) continue;
                    if (VFBinding.IsAnimatorBinding(binding)) continue;
                    bindings.Add(binding);
                }
            }

            return new ControllerDebugInfo {
                bindings = bindings,
                usesWdOff = usesWdOff
            };
        }
        
        public static List<VisualElement> BuildDebugInfo(
            IEnumerable<VFBinding> bindings,
            VFGameObject componentObject,
            bool isController = false,
            Action<string> addPathRewrite = null
        ) {
            var avatarObject = componentObject.GetAvatarRoot();
            var nonRewriteSafeBindings = new HashSet<string>();
            var outsidePrefabBindings = new HashSet<string>();
            var missingBindings = new HashSet<string>();
            var autofixPrefixes = new HashSet<string>();

            var usedBindings = new HashSet<VFBinding>();
            foreach (var binding in bindings) {
                if (binding.IsAnimatorBinding()) continue;

                var sourcePath = binding.GetStoredPath();
                var resolvedPath = binding.GetRewrittenPath();
                var debugPath = sourcePath + (sourcePath != resolvedPath ? " -> " + resolvedPath : "");
                if (binding.target == null) {
                    if (resolvedPath == null) {
                        // binding was deleted by rules :)
                        continue;
                    }
                    if (IsProbablyIgnoredBinding(resolvedPath)) {
                        // user doesn't care that this is missing :)
                        continue;
                    }
                    missingBindings.Add(debugPath);
                    continue;
                }

                usedBindings.Add(binding);

                if (!binding.target.IsSameOrChildOf(componentObject)) {
                    outsidePrefabBindings.Add(binding.PrettyString());
                    continue;
                }

                // Programmatically generated bindings target the object directly and have no unresolved path.
                if (resolvedPath == null) continue;

                var relativeTarget = resolvedPath == "" ? componentObject : componentObject.Find(resolvedPath);
                if (relativeTarget != binding.target) {
                    nonRewriteSafeBindings.Add(debugPath);
                    if (binding.target == componentObject) {
                        autofixPrefixes.Add(componentObject.GetPath(avatarObject));
                    } else {
                        var partInsideComponent = "/" + binding.target.GetPath(componentObject);
                        if (resolvedPath.EndsWith(partInsideComponent)) {
                            autofixPrefixes.Add(resolvedPath.Substring(0, resolvedPath.Length - partInsideComponent.Length));
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
                var obj = binding.target;
                if (obj == null) continue;
                if (!inNonAnimatedPhysbones.Contains(obj)) continue;
                badPhysboneTransforms.Add(obj.GetPath(avatarObject));
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
                    var obj = binding.target;
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
                var msg = $"Paths are animated in {thisName}, but not found in your avatar, thus, they won't do anything! If you are not the creator of this asset, this may be on purpose.";
                if (addPathRewrite != null) msg += " You may need to use 'Path Rewrite Rules' in the Advanced Settings to fix them if your avatar's objects are in a different location.";
                msg += "\n";
                msg += missingBindings.OrderBy(path => path).Join('\n');
                warnings.Add(VRCFuryEditorUtils.Warn(msg));
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
                    overLimitConstraints.Add($"Source {slotNum} on {binding.GetDebugPath(avatarObject)}");
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
