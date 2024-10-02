using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.ScriptableObjects;

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
    }
}
