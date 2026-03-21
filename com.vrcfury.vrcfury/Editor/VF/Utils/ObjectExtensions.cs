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
        private static readonly VFMultimapList<Object, string> workLog
            = new VFMultimapList<Object, string>();

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.update += () => {
                workLog.Clear();
            };
        }

        public static T GetCloneSource<T>(this T clone) where T : Object {
            var original = VrcfObjectCloner.GetOriginal(clone);
            if (original == null) throw new Exception("Failed to find original of a clone");
            return original;
        }

        public static T Clone<T>(this T original, string reason = null, string addPrefix = "", bool recursive = true) where T : Object {

            if (recursive) {
                if (original is Motion) {
                    return MutableManager.CopyRecursive(original, reason, new[] { typeof(Motion) });
                }
                if (original is VRCExpressionsMenu) {
                    return MutableManager.CopyRecursive(original, reason, new[] { typeof(VRCExpressionsMenu) });
                }
                if (original is RuntimeAnimatorController || original is AnimatorStateMachine) {
                    return MutableManager.CopyRecursive(original, reason, new[] {
                        typeof(RuntimeAnimatorController),
                        typeof(AnimatorStateMachine),
                        typeof(AnimatorState),
                        typeof(AnimatorTransitionBase),
                        typeof(StateMachineBehaviour),
                        typeof(AvatarMask),
                        typeof(Motion),
                    }, addPrefix);
                }
            }

            var clone = VrcfObjectCloner.Clone(original);
            if (reason != null) {
                clone.WorkLog(reason);
            }
            return clone;
        }

        public static void MarkClonedFrom(this Object to, Object from) {
            if (from == null || to == null || from == to) return;
            var originalWorkLog = workLog.Get(from);
            if (originalWorkLog.Count > 0) {
                foreach (var item in workLog.Get(from)) {
                    workLog.Put(to, item);
                }
            } else {
                workLog.Put(to, $"Imported from {from.GetPathAndName()}");
            }
        }

        public static void WorkLog(this Object obj, string item) {
            if (obj == null || string.IsNullOrEmpty(item)) return;
            if (!VrcfObjectFactory.DidCreate(obj)) {
                throw new Exception(
                    "Attempted to add a work log item to an object that was not created by VRCFury: " +
                    obj.name
                );
            }
            workLog.Put(obj, item);
        }

        public static IList<string> GetWorkLog(this Object obj) {
            return workLog.Get(obj);
        }

        public static string GetPathAndName(this Object obj) {
            if (obj == null) return "(null)";
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) {
                path = "(generated)";
            }
            if (AssetDatabase.IsMainAsset(obj)) {
                return path;
            }
            return $"{path} ({obj.name})";
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
    }
}
