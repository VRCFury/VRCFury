using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal static class ObjectExtensions {
        public static readonly VFMultimapList<UnityEngine.Object, string> cloneReasons
            = new VFMultimapList<UnityEngine.Object, string>();

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.update += () => cloneReasons.Clear();
        }

        public static T GetCloneSource<T>(this T clone) where T : UnityEngine.Object {
            var original = VrcfObjectCloner.GetOriginal(clone);
            if (original == null) throw new Exception("Failed to find original of a clone");
            return original;
        }

        public static T Clone<T>(this T original, string reason = null, string addPrefix = "") where T : UnityEngine.Object {
            T clone;
            if (original is Motion) {
                clone = MutableManager.CopyRecursive(original, new[] { typeof(Motion) });
            } else if (original is VRCExpressionsMenu) {
                clone = MutableManager.CopyRecursive(original, new[] { typeof(VRCExpressionsMenu) });
            } else if (original is RuntimeAnimatorController || original is AnimatorStateMachine) {
                clone = MutableManager.CopyRecursive(original, new[] {
                    typeof(RuntimeAnimatorController),
                    typeof(AnimatorStateMachine),
                    typeof(AnimatorState),
                    typeof(AnimatorTransitionBase),
                    typeof(StateMachineBehaviour),
                    typeof(AvatarMask),
                    typeof(Motion),
                }, addPrefix);
            } else {
                clone = VrcfObjectCloner.Clone(original);
            }

            if (clone != original) {
                foreach (var r in cloneReasons.Get(original)) {
                    cloneReasons.Put(clone, r);
                }
            }
            if (reason != null) {
                cloneReasons.Put(clone, reason);
            }
            return clone;
        }

        /**
         * Converts destroyed Objects ("fake-null") into "real" null values which can use null-coalescing
         */
        [CanBeNull]
        public static T NullSafe<T>([CanBeNull] this T obj) where T : UnityEngine.Object {
            return obj == null ? null : obj;
        }
        [CanBeNull]
        public static VFGameObject NullSafe([CanBeNull] this VFGameObject obj) {
            return obj == null ? null : obj;
        }

        public static T[] FindObjectsByType<T>() where T : Object {
#if UNITY_2022_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        public static Object[] FindObjectsByType(Type type) {
#if UNITY_2022_1_OR_NEWER
            return Object.FindObjectsByType(type, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType(type);
#endif
        }

        [InitializeOnLoadMethod]
        public static void IsMissingTest() {
            Object obj = null;
            if (obj.GetNoneType() != SerializedPropertyExtensions.NoneType.Unset) {
                Debug.LogError("Failed IsMissing test 1");
            }
            obj = new AnimationClip();
            if (obj.GetNoneType() != SerializedPropertyExtensions.NoneType.Present) {
                Debug.LogError("Failed IsMissing test 2");
            }
            Object.DestroyImmediate(obj);
            if (obj.GetNoneType() != SerializedPropertyExtensions.NoneType.Missing) {
                Debug.LogError("Failed IsMissing test 4");
            }
        }

        public static SerializedPropertyExtensions.NoneType GetNoneType([CanBeNull] this Object obj) {
            if (obj != null) return SerializedPropertyExtensions.NoneType.Present;
            var wrapper = ScriptableObject.CreateInstance<DummyObjectWrapper>();
            try {
                wrapper.obj = obj;
                using (var so = new SerializedObject(wrapper)) {
                    return so.FindProperty("obj").GetNoneType();
                }
            } finally {
                Object.DestroyImmediate(wrapper);
            }
        }

        [Serializable]
        private class DummyObjectWrapper : ScriptableObject {
            public Object obj;
        }
    }
}
