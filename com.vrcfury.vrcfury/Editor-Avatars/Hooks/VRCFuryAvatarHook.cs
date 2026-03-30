using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Actions;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
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

        public static string GetAnimatedPath(this VFGameObject obj) {
            var avatarObject = obj.GetAvatarRoot();
            return obj.GetPath(avatarObject);
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

        [InitializeOnLoadMethod]
        private static void Init() {
            SpsConfigurer.getIsActuallyUploading = IsActuallyUploadingHook.Get;

            VRCFuryHapticPlugEditor.getHapticsEnabled = HapticsToggleMenuItem.Get;

            VRCFuryHapticSocketEditor.getAvatarViewPos = obj => {
                var avatar = obj.GetAvatarRoot().GetComponent<VRCAvatarDescriptor>();
                if (avatar == null) return Vector3.zero;
                return avatar.ViewPosition;
            };

            VRCFuryHapticSocketEditor.getClosestBone = ClosestBoneUtils.GetClosestHumanoidBone;

            VFGameObject.getUploadRoots = obj => {
                return new[] { obj.GetAvatarRoot() };
            };

            DialogUtils.debugLineGetter = () => VrcfDebugLine.GetOutputString();

            VRCFuryComponentEditor.getDebugLine = component => {
                var avatarObject = component.GetAvatarRoot();
                return VrcfDebugLine.GetOutputString(avatarObject);
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

            VRCFuryActionSetDrawer.renderDebugInfo = (gameObject, actionSet) => {
                var debugInfo = new VisualElement();

                var avatarObject = gameObject.GetAvatarRoot();

                var injector = new VRCFuryInjector();
                injector.ImportOne(typeof(ActionClipService));
                injector.ImportOne(typeof(ClipFactoryService));
                injector.ImportScan(typeof(ActionBuilder));
                injector.Set("avatarObject", avatarObject);
                injector.Set("componentObject", new Func<VFGameObject>(() => avatarObject));
                var mainBuilder = injector.GetService<ActionClipService>();
                var test = mainBuilder.LoadStateAdv("test", actionSet, gameObject);
                var bindings = new AnimatorIterator.Clips().From(test.onClip)
                    .SelectMany(clip => clip.GetAllBindings())
                    .ToImmutableHashSet();
                var warnings =
                    VrcfAnimationDebugInfo.BuildDebugInfo(bindings, avatarObject);

                foreach (var warning in warnings) {
                    debugInfo.Add(warning);
                }
                return debugInfo;
            };

            VRCFuryComponentEditor.renderWarnings = (owner, warnings) => {
                var descriptors = owner.GetComponentsInSelfAndParents<VRCAvatarDescriptor>()
                    .SelectMany(descriptor => descriptor.owner().GetComponentsInSelfAndChildren<VRCAvatarDescriptor>())
                    .ToImmutableHashSet();
                var editingPrefab = UnityCompatUtils.IsEditingPrefab();
                if (!editingPrefab && !descriptors.Any()) {
                    var animators = owner.GetComponentsInSelfAndParents<Animator>();
                    if (animators.Any()) {
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
                        " This may cause issues in this inspector or during your avatar build.\n\n" + descriptors.Select(d => d.owner().GetPath()).Join('\n')));
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
