using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Actions;
using VF.Builder;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Hooks {
    /**
     * Wires up VRCFury-common for avatar work
     */
    internal static class VRCFuryAvatarHook {
        private static bool AllowRootFeatures(VFGameObject gameObject, [CanBeNull] VFGameObject avatarObject) {
            if (gameObject == avatarObject) {
                return true;
            }

            VFGameObject checkRoot;
            if (avatarObject == null) {
                checkRoot = gameObject.root;
            } else {
                checkRoot = gameObject.GetSelfAndAllParents()
                    .First(o => o.parent == avatarObject);
            }

            if (checkRoot == null) {
                return false;
            }

            return checkRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()
                .All(c => c is VRCFuryComponent || c is Transform);
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            DialogUtils.debugLineGetter = () => VrcfDebugLine.GetOutputString();

            VRCFuryComponentEditor.getDebugLine = component => {
                var avatarObject = VRCAvatarUtils.GuessAvatarObject(component);
                return VrcfDebugLine.GetOutputString(avatarObject);
            };

            FeatureFinder.onInjectEditor = (gameObject, builderType, injector) => {
                var avatarObject = VRCAvatarUtils.GuessAvatarObject(gameObject) ?? gameObject.root;
                var allowRootFeatures = AllowRootFeatures(gameObject, avatarObject);
                if (builderType.GetCustomAttribute<FeatureRootOnlyAttribute>() != null && !allowRootFeatures) {
                    throw new RenderFeatureEditorException(
                        "To avoid abuse by prefab creators, this component can only be placed on the root object" +
                        " containing the avatar descriptor, OR a child object containing ONLY vrcfury components."
                    );
                }
                injector.Set("avatarObject", avatarObject);
            };

            FeatureFinder.onGetBuilder = (gameObject, builderType, title) => {
                var avatarObject = VRCAvatarUtils.GuessAvatarObject(gameObject) ?? gameObject.root;
                var allowRootFeatures = AllowRootFeatures(gameObject, avatarObject);
                if (builderType.GetCustomAttribute<FeatureRootOnlyAttribute>() != null && !allowRootFeatures) {
                    throw new Exception($"This VRCFury component ({title}) is only allowed on the root object of the avatar, but was found in {gameObject.GetPath(avatarObject)}.");
                }
            };

            VRCFuryActionSetDrawer.renderDebugInfo = (gameObject, actionSet) => {
                var debugInfo = new VisualElement();

                var avatarObject = VRCAvatarUtils.GuessAvatarObject(gameObject);
                if (avatarObject == null) return debugInfo;

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
                    VrcfAnimationDebugInfo.BuildDebugInfo(bindings, avatarObject, avatarObject);

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

            VFGameObject.onPreDestroy = obj => {
                var b = VRCAvatarUtils.GuessAvatarObject(obj) ?? obj.root;
                foreach (var c in b.GetComponentsInSelfAndChildren<VRCPhysBoneBase>()) {
                    if (c.GetRootTransform().IsChildOf(obj))
                        Object.DestroyImmediate(c);
                }
                foreach (var c in b.GetComponentsInSelfAndChildren<VRCPhysBoneColliderBase>()) {
                    if (c.GetRootTransform().IsChildOf(obj))
                        Object.DestroyImmediate(c);
                }
                foreach (var c in b.GetComponentsInSelfAndChildren<ContactBase>()) {
                    if (c.GetRootTransform().IsChildOf(obj))
                        Object.DestroyImmediate(c);
                }
                foreach (var c in obj.GetConstraints(includeChildren: true)) {
                    c.Destroy();
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
