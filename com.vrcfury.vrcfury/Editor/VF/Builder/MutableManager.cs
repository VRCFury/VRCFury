using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Inspector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder {
    internal static class MutableManager {

        public static void ForEachChildObjectReference(Object obj, Action<string,Object,Action<Object>> each) {
            if (obj is Texture || obj is Mesh) {
                // These can't have any object children anyways, so we can just skip them for performance
                return;
            }

            var changed = false;
            var serialized = new SerializedObject(obj);
            var prop = serialized.GetIterator();
            while (true) {
                if (prop.propertyType == SerializedPropertyType.ObjectReference) {
                    var value = GetObjectReferenceValueSafe(prop);
                    if (value != null) {
                        void Set(Object v) {
                            if (value != v) {
                                value = v;
                                changed = true;
                                prop.objectReferenceValue = v;
                            }
                        }
                        each(prop.propertyPath, value, Set);
                    }
                }

                var enter = prop.propertyType == SerializedPropertyType.Generic
                            || prop.propertyType == SerializedPropertyType.ManagedReference;

                // Optimization so we don't have to iterate over the giant float arrays in dance AnimationClips
                if (obj is AnimationClip
                    && prop.propertyPath.EndsWith(".Array")
                    && !prop.propertyPath.ToLower().Contains("pptr")) {
                    enter = false;
                }

                if (!prop.Next(enter)) break;
            }

            if (changed) {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                VRCFuryEditorUtils.MarkDirty(obj);
            }
        }

        public static void RewriteInternals(Object obj, Dictionary<Object, Object> rewrites) {
            ForEachChildObjectReference(obj, (path, oldValue, set) => {
                var newValue = RewriteObject(oldValue, rewrites);
                if (oldValue != newValue) set(newValue);
            });
        }

        private static T RewriteObject<T>(T obj, Dictionary<Object, Object> rewrites) where T : Object {
            if (obj == null) return null;
            if (rewrites.TryGetValue(obj, out var newValue)) return newValue as T;
            return obj;
        }

        /** For some reason, unity occasionally breaks and return non-Objects from objectReferenceValue somehow. */
        private static Object GetObjectReferenceValueSafe(SerializedProperty prop) {
            if (prop.objectReferenceValue == null) return null;
            if (!(prop.objectReferenceValue is object systemObj)) return null;
            if (!(systemObj is Object unityObj)) return null;
            return unityObj;
        }

        public static T CopyRecursive<T>(T obj, Type[] typesToMakeMutable, string addPrefix = "") where T : Object {
            var originalToMutable = new Dictionary<Object, Object>();
            var mutableToOriginal = new Dictionary<Object, Object>();

            T rootCopy = null;

            // Make mutable copies of everything recursively
            ForEachChild(obj, original => {
                if (originalToMutable.ContainsKey(original)) return false;
                if (obj != original) {
                    if (!IsType(original, typesToMakeMutable)) return false;
                } 

                var copy = VrcfObjectCloner.Clone(original);
                if (obj == original) rootCopy = copy as T;

                if (copy is AnimatorState || copy is AnimatorStateMachine) {
                    // don't add prefix
                } else {
                    copy.name = $"{addPrefix}{original.name}";
                }

                originalToMutable[original] = copy;
                mutableToOriginal[copy] = original;
                return true;
            });

            // Connect the new copies to each other
            foreach (var mutable in originalToMutable.Values) {
                RewriteInternals(mutable, originalToMutable);
            }
            
            // If this isn't here, default states and state machine transitions involving child machines can disappear
            // because unity throws them away if they were invalid (because the child wasn't rewritten yet) when they were rewritten above
            foreach (var (original,mutable) in originalToMutable.Select(x => (x.Key, x.Value))) {
                if (original is AnimatorStateMachine originalSm && mutable is AnimatorStateMachine mutableSm) {
                    mutableSm.defaultState = RewriteObject(originalSm.defaultState, originalToMutable);
                    foreach (var sm in mutableSm.stateMachines.Select(child => child.stateMachine)) {
                        var originalTransitions =
                            originalSm.GetStateMachineTransitions(RewriteObject(sm, mutableToOriginal));
                        var rewrittenTransitions = originalTransitions
                            .Select(a => RewriteObject(a, originalToMutable))
                            .ToArray();
                        mutableSm.SetStateMachineTransitions(sm, rewrittenTransitions);
                    }
                    VRCFuryEditorUtils.MarkDirty(mutable);
                }
            }

            return rootCopy;
        }

        public static void ForEachChild(Object obj, Func<Object,bool> visit) {
            if (obj == null) return;
            var visited = new HashSet<Object>();
            var stack = new Stack<Object>();
            stack.Push(obj);
            while (stack.Count > 0) {
                var o = stack.Pop();
                if (visited.Contains(o)) continue;
                visited.Add(o);
                var enter = visit(o);
                if (!enter) continue;

                ForEachChildObjectReference(o, (path, child,set) => {
                    stack.Push(child);
                });
            }
        }

        private static bool IsType(Object obj, Type[] types) =>
            types.Any(type => type.IsInstanceOfType(obj));
    }
}
