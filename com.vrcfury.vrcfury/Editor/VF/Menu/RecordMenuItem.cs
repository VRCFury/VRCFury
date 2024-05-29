using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Menu {
     internal class RecordMenuItem {
//         private static VFGameObject lastSelectedAvatar;
//         private static Action onRecordingEnd;
//
//         private static readonly Type AnimationWindow =
//             typeof(AnimationWindow);
//         private static readonly MethodInfo EditGameObject =
//             AnimationWindow.GetMethod("EditGameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
//         private static readonly PropertyInfo recording =
//             AnimationWindow.GetProperty("recording");
//
//         [InitializeOnLoadMethod]
//         private static void Init() {
//             if (EditGameObject == null || recording == null) return;
//
//             Selection.selectionChanged += () => {
//                 var avatar = MenuUtils.GetSelectedAvatar();
//                 if (avatar != null) lastSelectedAvatar = avatar;
//             };
//
//             EditorApplication.update += () => {
//                 if (onRecordingEnd == null) return;
//                 var isRecording = Resources.FindObjectsOfTypeAll(AnimationWindow)
//                     .Any(window => (bool)recording.GetValue(window));
//                 if (!isRecording) onRecordingEnd();
//             };
//
//             AssemblyReloadEvents.beforeAssemblyReload += OnRecordingEnd;
//         }
//
//         [MenuItem("Assets/Record clip using avatar (VRCFury)")]
//         private static void RunAnimTest() {
//             var clip = Selection.activeObject as AnimationClip;
//             if (clip == null) return;
//
//             var baseObject = lastSelectedAvatar;
//             if (baseObject == null) return;
//
//             Selection.SetActiveObjectWithContext(baseObject, baseObject);
//             Record(clip, baseObject);
//         }
//
//         private static void OnRecordingEnd() {
//             onRecordingEnd?.Invoke();
//             onRecordingEnd = null;
//         }
//
//         public static void Record(AnimationClip clip, GameObject baseObject) {
//             OnRecordingEnd();
//
//             var controller = VrcfObjectFactory.Create<AnimatorController>();
//             controller.AddLayer("Temp Controller For Recording");
//             var layer = controller.layers.Last();
//             VrcfObjectFactory.Register(layer.stateMachine);
//             var state = layer.stateMachine.AddState("Main");
//             VrcfObjectFactory.Register(state);
//             state.motion = clip;
//
//             Action onRecordingStart = null;
//             var animator = baseObject.GetComponent<Animator>();
//             if (animator != null) {
//                 var bakController = animator.runtimeAnimatorController;
//                 var bakAvatar = animator.avatar;
//                 onRecordingEnd = () => {
//                     animator.runtimeAnimatorController = bakController;
//                     animator.avatar = bakAvatar;
//                 };
//             } else {
//                 animator = baseObject.AddComponent<Animator>();
//                 animator.enabled = false;
//                 animator.hideFlags = HideFlags.DontSave;
//                 onRecordingEnd = () => Object.DestroyImmediate(animator);
//             }
//             animator.runtimeAnimatorController = controller;
//             animator.avatar = null;
//
//             try {
//                 var animationWindow = Resources.FindObjectsOfTypeAll(AnimationWindow)[0];
//                 EditGameObject.Invoke(animationWindow, new object[] { (GameObject)baseObject });
//                 recording.SetValue(animationWindow, true);
//             } finally {
//                 onRecordingStart?.Invoke();
//             }
//
//             /*
//             Reset();
//
//             var clone = originalAvatar.Clone();
//             clone.name = "VRCFury Recording Copy";
//             clone.active = true;
//             if (clone.scene != originalAvatar.scene) {
//                 SceneManager.MoveGameObjectToScene(clone, originalAvatar.scene);
//             }
//             Selection.SetActiveObjectWithContext(clone, clone);
//             
//             foreach (var an in clone.GetComponentsInSelfAndChildren<Animator>()) {
//                 Object.DestroyImmediate(an);
//             }
//             foreach (var a in clone.GetComponentsInSelfAndChildren<Animation>()) {
//                 Object.DestroyImmediate(a);
//             }
//             foreach (var a in clone.GetComponentsInSelfAndChildren<VRCAvatarDescriptor>()) {
//                 Object.DestroyImmediate(a);
//             }
//             var animator = clone.AddComponent<Animator>();
//             var controller = VrcfObjectFactory.Create<AnimatorController>();
//             animator.runtimeAnimatorController = controller;
//             controller.AddLayer("Temp Controller For Recording");
//             VrcfObjectFactory.Register(layer.stateMachine);
//             var layer = controller.layers.Last();
//             var state = layer.stateMachine.AddState("Main");
//             VrcfObjectFactory.Register(state);
//             state.motion = selectedAnimation;
//             
//             var animationWindow = Resources.FindObjectsOfTypeAll(AnimationWindow)[0];
//             EditGameObject.Invoke(animationWindow, new object[] { (GameObject)clone });
//             recording.SetValue(animationWindow, true);
//
//             reset += () => {
//                 
//             };
//             
//             Debug.Log("Hello world");
//             */
//         }
     }
}
