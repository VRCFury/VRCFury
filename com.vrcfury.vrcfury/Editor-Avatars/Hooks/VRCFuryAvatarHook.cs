using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Actions;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Hooks.VrcsdkFixes;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Hooks {
    /**
     * Wires up VRCFury-common for avatar work
     */
    internal static class VRCFuryAvatarHook {
        // Per-frame caches shared across all VRCFury inspectors on the same avatar.
        // Clears every editor frame so results stay correct while avoiding O(components × avatar size) work.
        private static readonly Dictionary<VFGameObject, string> debugLinePerFrame
            = new Dictionary<VFGameObject, string>();
        private static readonly Dictionary<VFGameObject, DescriptorCacheEntry> descriptorCachePerFrame
            = new Dictionary<VFGameObject, DescriptorCacheEntry>();

        private class DescriptorCacheEntry {
            public ImmutableHashSet<VRCAvatarDescriptor> descriptors;
            public bool hasAnimators;
        }

        public static VFGameObject GetAvatarRoot(this VFGameObject obj) {
            if (obj == null) return null;
            var avatars = obj.GetComponentsInSelfAndParents<VRCAvatarDescriptor>();
            if (avatars.Length > 0) return avatars.Last().owner();
            var animators = obj.GetComponentsInSelfAndParents<Animator>();
            if (animators.Length > 0) return animators.Last().owner();
            return obj.root;
        }

        public static VFGameObject GetAvatarRoot(this UnityEngine.Component c) {
            return c.owner().GetAvatarRoot();
        }

        private static bool AllowRootFeatures(VFGameObject gameObject) {
            var avatarRoot = gameObject.GetAvatarRoot();
            if (gameObject == avatarRoot) {
                return true;
            }

            return gameObject.GetSelfAndAllParents()
                .First(o => o.parent == avatarRoot)
                .GetComponentsInSelfAndChildren<UnityEngine.Component>()
                .All(c => c is VRCFuryComponent || c is Transform);
        }

        [VFInit]
        private static void Init() {
            // Clear shared inspector caches every frame (same pattern as VRCFObjectPathCache / VRCFArmatureCache)
            Scheduler.Schedule(() => {
                debugLinePerFrame.Clear();
                descriptorCachePerFrame.Clear();
            }, 0);

            VRCFuryHapticPlugEditor.getHapticsEnabled = HapticsToggleMenuItem.Get;

            VFGameObject.getUploadRoots = obj => {
                return new[] { obj.GetAvatarRoot() };
            };

            DialogUtils.debugLineGetter = () => VrcfDebugLine.GetOutputString();

            // Version line: compute once per avatar per frame, reuse for every component inspector
            VRCFuryComponentEditor.getDebugLine = component => {
                var avatarObject = component.GetAvatarRoot();
                if (avatarObject == null) {
                    return VrcfDebugLine.GetOutputString(null);
                }
                return debugLinePerFrame.GetOrCreate(avatarObject, () =>
                    VrcfDebugLine.GetOutputString(avatarObject));
            };

            FeatureFinder.onInjectEditor = (gameObject, builderType, injector) => {
                var allowRootFeatures = AllowRootFeatures(gameObject);
                if (builderType.GetCustomAttribute<FeatureRootOnlyAttribute>() != null && !allowRootFeatures) {
                    throw new RenderFeatureEditorException(
                        "To avoid abuse by prefab creators, this component can only be placed on the root object" +
                        " containing the avatar descriptor, OR a child object containing ONLY vrcfury components."
                    );
                }
                injector.Set("avatarObject", gameObject.GetAvatarRoot());
            };

            FeatureFinder.onGetBuilder = (gameObject, builderType, title) => {
                var avatarObject = gameObject.GetAvatarRoot();
                var allowRootFeatures = AllowRootFeatures(gameObject);
                if (builderType.GetCustomAttribute<FeatureRootOnlyAttribute>() != null && !allowRootFeatures) {
                    throw new Exception($"This VRCFury component ({title}) is only allowed on the root object of the avatar, but was found in {gameObject.GetPath(avatarObject)}.");
                }
            };

            // Action-set debug box (main lag source): reuse the existing per-frame path/armature caches
            // instead of scanning the entire avatar hierarchy once per action set.
            VRCFuryActionSetDrawer.renderDebugInfo = (gameObject, actionSet) => {
                var debugInfo = new VisualElement();

                var avatarObject = gameObject.GetAvatarRoot();

                var injector = new VRCFuryInjector();
                injector.ImportOne(typeof(ActionClipService));
                injector.ImportOne(typeof(ClipFactoryService));
                // Inject the shared per-frame caches (already captured) instead of creating + Capture() per action set
                injector.Set(VRCFObjectPathCache.GetPerFrame(avatarObject));
                injector.Set(VRCFArmatureCache.GetPerFrame(avatarObject));
                injector.ImportScan(typeof(ActionBuilder));
                injector.Set("avatarObject", avatarObject);
                injector.Set("componentObject", new Func<VFGameObject>(() => avatarObject));
                var mainBuilder = injector.GetService<ActionClipService>();
                var test = mainBuilder.LoadStateAdv("test", actionSet, gameObject, debugMode: true);
                var bindings = new AnimatorIterator.Clips().From(test.onClip)
                    .SelectMany(clip => clip.GetAllBindings())
                    .ToImmutableHashSet();
                var clips = new HashSet<AnimationClip>();
                UnitySerializationUtils.Iterate(actionSet, visit => {
                    if (visit.value is AnimationClip clip && clip != null) {
                        clips.Add(clip);
                    }
                    return UnitySerializationUtils.IterateResult.Continue;
                });
                var warnings =
                    VrcfAnimationDebugInfo.BuildDebugInfo(clips, bindings, gameObject);

                foreach (var warning in warnings) {
                    debugInfo.Add(warning);
                }
                return debugInfo;
            };

            // Descriptor / "missing avatar descriptor" warning: scan once per avatar per frame
            VRCFuryComponentEditor.renderWarnings = (owner, warnings) => {
                var avatarRoot = owner.GetAvatarRoot() ?? owner;
                var entry = descriptorCachePerFrame.GetOrCreate(avatarRoot, () => {
                    // Prefer scanning from avatar root so the result is independent of which component
                    // requested the cache first.
                    var searchRoot = avatarRoot != null ? avatarRoot : owner;
                    var descriptors = searchRoot.GetComponentsInSelfAndParents<VRCAvatarDescriptor>()
                        .SelectMany(descriptor => descriptor.owner().GetComponentsInSelfAndChildren<VRCAvatarDescriptor>())
                        .ToImmutableHashSet();
                    var hasAnimators = searchRoot.GetComponentsInSelfAndParents<Animator>().Any();
                    return new DescriptorCacheEntry {
                        descriptors = descriptors,
                        hasAnimators = hasAnimators
                    };
                });

                var descriptors = entry.descriptors;
                var editingPrefab = UnityCompatUtils.IsEditingPrefab();
                if (!editingPrefab && !descriptors.Any()) {
                    if (entry.hasAnimators) {
                        warnings.Add(VRCFuryEditorUtils.Error(
                            "Your avatar does not have a VRC Avatar Descriptor, and thus this component will not do anything! " +
                            "Make sure that your avatar can actually be uploaded using the VRCSDK before attempting to add VRCFury things to it."));
                    } else {
                        warnings.Add(VRCFuryEditorUtils.Error(
                            "This VRCFury component is not placed on an avatar, and thus will not do anything! " +
                            "If you intended to include this in your avatar, make sure you've placed it within your avatar's " +
                            "object, and not just alongside it in the scene."));
                    }
                }

                if (descriptors.Count > 1) {
                    warnings.Add(VRCFuryEditorUtils.Error(
                        "There are multiple avatar descriptors in this hierarchy. Each avatar should only have one avatar descriptor on the avatar root." +
                        " This may cause issues in this inspector or during your avatar build.\n\n" + descriptors.Select(d => d.owner().GetDebugPath()).Join('\n')));
                }
            };

            ObjectExtensions.getExtraRecursiveTypes = original => {
                if (original is VRCExpressionsMenu) {
                    return new[] { typeof(VRCExpressionsMenu) };
                }
                return null;
            };
        }
    }
}
