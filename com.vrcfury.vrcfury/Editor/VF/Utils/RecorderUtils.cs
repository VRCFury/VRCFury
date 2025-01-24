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
        private static Action restore = null;
        
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type animStateType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditorInternal.AnimationWindowState");
            public static readonly PropertyInfo selectionField = animStateType?.GetProperty("selection");
            public static readonly PropertyInfo gameObjectField = selectionField?.PropertyType.GetProperty("gameObject");
            public static readonly PropertyInfo animationClipField = animStateType?.GetProperty("activeAnimationClip");
#if ! UNITY_6000_0_OR_NEWER
            public static readonly MethodInfo startRecording = animStateType?.GetMethod("StartRecording");
#endif
            public static readonly PropertyInfo isRecordingProperty = animStateType?.GetProperty("recording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly Type AnimationWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.AnimationWindow");
            public static readonly PropertyInfo AnimationWindowState = AnimationWindow?.GetProperty("state", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;

            void Cleanup() {
                if (restore == null) return;
                var r = restore;
                restore = null;
                r();
            }

            EditorApplication.update += () => {
                if (restore != null && !IsRecording()) Cleanup();
            };

            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        private static bool IsRecording() {
            if (!ReflectionHelper.IsReady<Reflection>()) return false;
            var animationWindow = EditorWindowFinder.GetWindows(Reflection.AnimationWindow).FirstOrDefault();
            if (animationWindow == null) return false;
            var animState = Reflection.AnimationWindowState.GetValue(animationWindow);
            return (bool)Reflection.isRecordingProperty.GetValue(animState);
        }

        public static void Record(AnimationClip clip, VFGameObject baseObj, bool rewriteClip = true) {
            if (!ReflectionHelper.IsReady<Reflection>()) {
                DialogUtils.DisplayDialog("VRCFury Animation Recorder",
                    "VRCFury failed to initialize the recorder. Maybe this version of unity is not supported yet?", "Ok");
                return;
            }
            if (IsRecording()) {
                DialogUtils.DisplayDialog("VRCFury Animation Recorder", "An animation is already being recorded",
                    "Ok");
                return;
            }
            
            // Open / focus the animation tab
            var animationWindow = EditorWindowFinder.GetWindows(Reflection.AnimationWindow).FirstOrDefault();
            if (animationWindow == null) {
                DialogUtils.DisplayDialog("VRCFury Animation Recorder", "Animation tab needs to be open",
                    "Ok");
                return;
            }

            animationWindow.Focus();
            var animState = Reflection.AnimationWindowState.GetValue(animationWindow);
            
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
            foreach (var child in avatarObject.GetSelfAndAllChildren()) {
                if (expandedIds.Contains(child.GetInstanceID())) {
                    var expandedInClone = clone.Find(child.GetPath(avatarObject));
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

            var selection = Reflection.selectionField.GetValue(animState);
            Reflection.gameObjectField.SetValue(selection, (GameObject)clone);
            Reflection.animationClipField.SetValue(animState, clip);
#if UNITY_6000_0_OR_NEWER
            Reflection.isRecordingProperty.SetValue(animState, true);
#else
            Reflection.startRecording.Invoke(animState, new object[] { });
#endif

            if (avatarObject == baseObj) rewriteClip = false;
            if (rewriteClip) {
                clip.Rewrite(AnimationRewriter.Combine(
                    ClipRewriter.CreateNearestMatchPathRewriter(
                        animObject: baseObj,
                        rootObject: avatarObject
                    ),
                    ClipRewriter.AnimatorBindingsAlwaysTargetRoot()
                ));
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
                    clip.Rewrite(AnimationRewriter.Combine(
                        ClipRewriter.CreateNearestMatchPathRewriter(
                            animObject: baseObj,
                            rootObject: avatarObject,
                            invert: true
                        ),
                        ClipRewriter.AnimatorBindingsAlwaysTargetRoot()
                    ));
                    clip.FinalizeAsset(false);
                }
            };
        }
    }
}
