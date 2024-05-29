using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Inspector;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Builder {
    internal static class MutableManager {

        private static readonly Type[] typesToMakeMutable = {
            // This has to be here because animator override controllers
            // can have other controllers as children
            typeof(RuntimeAnimatorController),

            // Animator Controller internals
            typeof(AnimatorStateMachine),
            typeof(AnimatorState),
            typeof(AnimatorTransitionBase),
            typeof(StateMachineBehaviour),
            typeof(AvatarMask),

            // Clips and blend trees
            typeof(Motion),

            typeof(VRCExpressionParameters),
            typeof(VRCExpressionsMenu),
        };
        
        private static readonly Type[] hiddenTypes = {
            typeof(AnimatorStateMachine),
            typeof(AnimatorState),
            typeof(AnimatorTransitionBase),
            typeof(StateMachineBehaviour),
        };

        private static void Iterate(SerializedObject obj, Action<SerializedProperty> act) {
            var prop = obj.GetIterator();
            do {
                act(prop);
            } while (prop.Next(true));
        }
        
        private static T RewriteObject<T>(T obj, Dictionary<Object, Object> rewrites) where T : Object {
            if (obj == null) return null;
            if (rewrites.TryGetValue(obj, out var newValue)) return newValue as T;
            return obj;
        }

        public static void RewriteInternals(Object obj, Dictionary<Object, Object> rewrites) {
            var serialized = new SerializedObject(obj);
            var changed = false;
            Iterate(serialized, prop => {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) return;
                var oldValue = GetObjectReferenceValueSafe(prop);
                var newValue = RewriteObject(oldValue, rewrites);
                if (oldValue == newValue) return;
                prop.objectReferenceValue = newValue;
                changed = true;
            });
            if (changed) {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                VRCFuryEditorUtils.MarkDirty(obj);
            }
        }

        /** For some reason, unity occasionally breaks and return non-Objects from objectReferenceValue somehow. */
        private static Object GetObjectReferenceValueSafe(SerializedProperty prop) {
            if (prop.objectReferenceValue == null) return null;
            if (!(prop.objectReferenceValue is object systemObj)) return null;
            if (!(systemObj is Object unityObj)) return null;
            return unityObj;
        }

        public class CopyResults<T> {
            public Dictionary<Object, Object> originalToCopy;
            public Dictionary<Object, Object> copyToOriginal;
            public T output;
        }

        public static T CopyRecursive<T>(T obj, bool addPrefix = true) where T : Object {
            return CopyRecursiveAdv(obj, addPrefix).output;
        }

        public static CopyResults<T> CopyRecursiveAdv<T>(T obj, bool addPrefix = true) where T : Object {
            var originalToMutable = new Dictionary<Object, Object>();
            var mutableToOriginal = new Dictionary<Object, Object>();

            T rootCopy = null;

            // Make mutable copies of everything recursively
            ForEachChild(obj, original => {
                if (originalToMutable.ContainsKey(original)) return false;
                if (obj != original) {
                    if (!IsType(original, typesToMakeMutable)) return false;
                }

                var copy = MakeMutable(original, true);
                if (obj == original) rootCopy = copy as T;

                if (IsType(copy, hiddenTypes)) {
                    copy.hideFlags |= HideFlags.HideInHierarchy;
                } else if (addPrefix) {
                    copy.name = $"{obj.name}/{original.name}";
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

            return new CopyResults<T>() {
                originalToCopy = originalToMutable,
                copyToOriginal = mutableToOriginal,
                output = rootCopy,
            };
        }

        // It's like Object.Instantiate, except it actually works with
        // AnimatorControllers, AnimatorStateMachines, AnimatorStates,
        // and other things that unity usually logs errors from when using
        // Object.Instantiate
        private static T SafeInstantiate<T>(T original) where T : Object {
            if (original is Material || original is Mesh) {
                var c = Object.Instantiate(original);
                VrcfObjectFactory.Register(c);
                return c;
            }

            if (original is AnimationClip clip) {
                return clip.Clone() as T;
            }

            var copy = VrcfObjectFactory.Create(original.GetType()) as T;
            EditorUtility.CopySerialized(original, copy);
            return copy;
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
                
                // AnimationClips are really big, so we can just iterate the possible object children
                if (o is AnimationClip clip) {
                    foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                        var curve = AnimationUtility.GetObjectReferenceCurve(clip, b);
                        foreach (var frame in curve) {
                            if (frame.value != null) stack.Push(frame.value);
                        }
                    }
                } else {
                    Iterate(new SerializedObject(o), prop => {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference) {
                            var objectReferenceValue = GetObjectReferenceValueSafe(prop);
                            if (objectReferenceValue != null) {
                                stack.Push(objectReferenceValue);
                            }
                        }
                    });
                }
            }
        }

        private static bool IsType(Object obj, Type[] types) =>
            types.Any(type => type.IsInstanceOfType(obj));

        public static T MakeMutable<T>(T original, bool forceCopy = false) where T : Object {
            if (!forceCopy && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(original))) {
                // Already mutable
                return original;
            }

            if (original is Material originalMat) {
                MaterialLocker.Lock(originalMat);
            }

            var copy = SafeInstantiate(original);
            copy.name = original.name;
            if (copy is Material copyMat) {
                // Ensure the material is flattened (if it's a material variant)
                // This way, things like SPS can change the shader
#if UNITY_2022_1_OR_NEWER
                copyMat.parent = null;
#endif
                
                // Keep the thry suffix so if it's locked later, the renamed properties still use the same suffixes
                if (string.IsNullOrWhiteSpace(copyMat.GetTag("thry_rename_suffix", false))) {
                    copyMat.SetOverrideTag("thry_rename_suffix", Regex.Replace(original.name, "[^a-zA-Z0-9_]", ""));
                }
            }
            return copy;
        }
    }
}
