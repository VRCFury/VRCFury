using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Component;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal static class RecorderUtils {

        private static readonly Type animStateType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditorInternal.AnimationWindowState");
        private static readonly PropertyInfo selectionField = animStateType?.GetProperty("selection");
        private static readonly PropertyInfo gameObjectField = selectionField?.PropertyType.GetProperty("gameObject");
        private static readonly PropertyInfo animationClipField = animStateType?.GetProperty("activeAnimationClip");
        private static readonly MethodInfo startRecording = animStateType?.GetMethod("StartRecording");
        private static bool initialized = false;

        private static Action restore = null;

        [InitializeOnLoadMethod]
        private static void Init() {
            if (animStateType == null || selectionField == null || gameObjectField == null || animationClipField == null || startRecording == null) {
                Debug.LogError("VRCFury failed to find Unity recording methods");
                return;
            }

            initialized = true;

            void Cleanup() {
                if (restore == null) return;
                var r = restore;
                restore = null;
                r();
            }

            EditorApplication.update += () => {
                if (!AnimationWindowUtils.IsRecording()) Cleanup();
            };

            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        public static void Record(AnimationClip clip, VFGameObject baseObj, bool rewriteClip = true) {
            if (!initialized) {
                EditorUtility.DisplayDialog("VRCFury Animation Recorder",
                    "VRCFury failed to initialize the recorder. Maybe this version of unity is not supported yet?", "Ok");
                return;
            }
            if (AnimationWindowUtils.IsRecording()) {
                EditorUtility.DisplayDialog("VRCFury Animation Recorder", "An animation is already being recorded",
                    "Ok");
            }
            
            var avatarObject = baseObj.GetComponentInSelfOrParent<VRCAvatarDescriptor>().NullSafe()?.owner();
            if (avatarObject == null) {
                avatarObject = baseObj.GetComponentInSelfOrParent<Animator>().NullSafe()?.owner();
            }
            if (avatarObject == null) {
                avatarObject = baseObj.root;
            }

            var wasActive = avatarObject.active;
            avatarObject.active = false;

            var clone = avatarObject.Clone();
            clone.active = true;
            clone.name = avatarObject.name + " (VRCFury Recording Copy)";
            if (clone.scene != avatarObject.scene) {
                SceneManager.MoveGameObjectToScene(clone, avatarObject.scene);
            }

            var expandedIds = CollapseUtils.GetExpandedIds();
            var wasExpanded = expandedIds.Contains(avatarObject.GetInstanceID());
            CollapseUtils.SetExpanded(avatarObject, false);
            foreach (var transform in avatarObject.GetComponentsInSelfAndChildren<Transform>()) {
                if (expandedIds.Contains(transform.gameObject.GetInstanceID())) {
                    var expandedInClone = clone.Find(transform.owner().GetPath(avatarObject));
                    if (expandedInClone != null) CollapseUtils.SetExpanded(expandedInClone, true);
                }
            }
            
            var prefix = baseObj.GetPath(avatarObject);
            var baseObjInClone = clone.Find(prefix);
            Selection.activeGameObject = baseObjInClone;

            foreach (var an in clone.GetComponentsInSelfAndChildren<Animator>()) {
                Object.DestroyImmediate(an);
            }
            foreach (var a in clone.GetComponentsInSelfAndChildren<Animation>()) {
                Object.DestroyImmediate(a);
            }
            foreach (var a in clone.GetComponentsInSelfAndChildren<VRCFuryComponent>()) {
                Object.DestroyImmediate(a);
            }
            var animator = clone.AddComponent<Animator>();
            var controller = new AnimatorController();
            controller.AddLayer("Temp Controller For Recording");
            var layer = controller.layers.Last();
            var state = layer.stateMachine.AddState("Main");
            state.motion = clip;
            animator.runtimeAnimatorController = controller;

            var animState = Resources.FindObjectsOfTypeAll(animStateType)[0];
            var selection = selectionField.GetValue(animState);
            gameObjectField.SetValue(selection, (GameObject)clone);
            animationClipField.SetValue(animState, clip);
            startRecording.Invoke(animState, new object[] { });

            if (avatarObject == baseObj) rewriteClip = false;
            if (rewriteClip) {
                clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                    if (binding.type == typeof(Animator)) {
                    } else if (binding.path == "") {
                        binding.path = prefix;
                    } else if (baseObj.Find(binding.path) != null) {
                        binding.path = prefix + "/" + binding.path;
                    } 
                    return binding;
                }));
                clip.FinalizeAsset(false);
            }

            restore = () => {
                if (clone != null) clone.Destroy();
                if (baseObj != null) Selection.activeGameObject = baseObj;
                if (avatarObject != null) {
                    if (wasActive) avatarObject.active = true;
                    if (wasExpanded) CollapseUtils.SetExpanded(avatarObject, true);
                }
                if (rewriteClip && clip != null) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.type == typeof(Animator)) {
                        } else if (binding.path == prefix) {
                            binding.path = "";
                        } else if (binding.path.StartsWith(prefix + "/")) {
                            binding.path = binding.path.Substring(prefix.Length + 1);
                        } 
                        return binding;
                    }));
                    clip.FinalizeAsset(false);
                }
            };
        }
    }
}
