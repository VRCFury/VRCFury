using System;
using System.Linq;
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
        private static Action restore;

        public static void Record(AnimationClip clip, VFGameObject baseObj) {
            if (clip == null) {
                EditorUtility.DisplayDialog("VRCFury Animation Recorder",
                    "An animation clip file must be set before recording", "Ok");
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
            clone.name = "VRCFury Recording Copy";
            if (clone.scene != avatarObject.scene) {
                SceneManager.MoveGameObjectToScene(clone, avatarObject.scene);
            }

            var path = baseObj.GetPath(avatarObject);
            var baseObjInClone = clone.Find(path);
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
            animator.runtimeAnimatorController = controller;
            controller.AddLayer("Temp Controller For Recording");
            var layer = controller.layers.Last();
            var state = layer.stateMachine.AddState("Main");
            state.motion = clip;

            var animStateType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditorInternal.AnimationWindowState");
            var animState = Resources.FindObjectsOfTypeAll(animStateType)[0];
            var selectionField = animStateType.GetProperty("selection");
            var selection = selectionField.GetValue(animState);
            var gameObjectField = selection.GetType().GetProperty("gameObject");
            gameObjectField.SetValue(selection, (GameObject)clone);
            var animationClipField = animStateType.GetProperty("activeAnimationClip");
            animationClipField.SetValue(animState, clip);
            var startRecording = animStateType.GetMethod("StartRecording");
            startRecording.Invoke(animState, new object[] { });

            EditorApplication.update -= Update;
            EditorApplication.update += Update;

            restore = () => {
                if (clone != null) clone.Destroy();
                if (baseObj != null) Selection.activeGameObject = baseObj;
                if (wasActive) avatarObject.active = true;
            };
        }

        private static void Update() {
            if (AnimationWindowUtils.IsRecording()) return;
            EditorApplication.update -= Update;
            restore();
        }
    }
}
