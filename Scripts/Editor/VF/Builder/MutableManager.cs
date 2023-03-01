using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Inspector;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public class MutableManager {
        private string tmpDir;

        private static readonly Type[] typesToMakeMutable = {
            typeof(AnimatorController),

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
        
        private static readonly Type[] typesToNeverRevert = {
            typeof(AnimatorController),
            typeof(AnimatorStateMachine),
            typeof(AnimatorState),
            typeof(AnimatorTransitionBase)
        };

        public MutableManager(string tmpDir) {
            this.tmpDir = tmpDir;
        }

        private static void Iterate(SerializedObject obj, Action<SerializedProperty> act) {
            var prop = obj.GetIterator();
            do {
                act(prop);
            } while (prop.Next(true));
        }

        private static void Rewrite(Object obj, Dictionary<Object, Object> rewrites) {
            var serialized = new SerializedObject(obj);
            var changed = false;
            Iterate(serialized, prop => {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) return;
                var oldValue = prop.objectReferenceValue;
                if (oldValue == null) return;
                if (!rewrites.TryGetValue(oldValue, out var newValue)) return;
                if (oldValue == newValue) return;
                prop.objectReferenceValue = newValue;
                changed = true;
            });
            if (changed) {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                VRCFuryEditorUtils.MarkDirty(obj);
            }
        }

        public T CopyRecursive<T>(T obj, string saveFilename = null, Object saveParent = null) where T : Object {
            var originalToMutable = new Dictionary<Object, Object>();

            T rootCopy = null;

            ForEachChild(obj, original => {
                if (originalToMutable.ContainsKey(original)) return false;
                if (obj != original) {
                    if (!IsType(original, typesToMakeMutable)) return false;
                    if (original is AnimationClip && AssetDatabase.GetAssetPath(original).Contains("/proxy_"))
                        return false;
                }

                var copy = SafeInstantiate(original);
                if (obj == original) rootCopy = copy as T;
                if (saveParent == null) {
                    VRCFuryAssetDatabase.SaveAsset(copy, tmpDir, saveFilename);
                    saveParent = copy;
                } else {
                    if (IsType(copy, hiddenTypes)) {
                        copy.hideFlags |= HideFlags.HideInHierarchy;
                    } else {
                        copy.name = $"{obj.name}/{original.name}";
                    }
                    AssetDatabase.AddObjectToAsset(copy, saveParent);
                }

                originalToMutable[original] = copy;
                return true;
            });
            
            foreach (var mutable in originalToMutable.Values) {
                Rewrite(mutable, originalToMutable);
            }

            return rootCopy;
        }

        // It's like Object.Instantiate, except it actually works with
        // AnimatorControllers, AnimatorStateMachines, AnimatorStates,
        // and other things that unity usually logs errors from when using
        // Object.Instantiate
        public static T SafeInstantiate<T>(T original) where T : Object {
            T copy;
            if (original is ScriptableObject) {
                copy = ScriptableObject.CreateInstance(original.GetType()) as T;
            } else {
                copy = Activator.CreateInstance(original.GetType()) as T;
            }
            if (copy == null) {
                throw new VRCFBuilderException("Failed to create copy of " + copy);
            }
            EditorUtility.CopySerialized(original, copy);
            return copy;
        }
        
        private void ForEachChild(Object obj, Func<Object,bool> visit) {
            var visited = new HashSet<Object>();
            var stack = new Stack<Object>();
            stack.Push(obj);
            while (stack.Count > 0) {
                var o = stack.Pop();
                if (visited.Contains(o)) continue;
                visited.Add(o);
                var enter = visit(o);
                if (!enter) continue;
                Iterate(new SerializedObject(o), prop => {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference) {
                        if (prop.objectReferenceValue != null) {
                            stack.Push(prop.objectReferenceValue);
                        }
                    }
                });
            }
        }
        
        // Make a fresh clone, because CopySerialized does make /some/ changes
        // like wiping out editor cache values, so they might be slightly different
        // for no good reason unless we do this.
        private static SerializedObject GetCleanSo(Object obj) {
            return new SerializedObject(SafeInstantiate(obj));
        }
        public static bool IsModified(Object a, Object b, Func<Object,Object> bToAChildMapper = null) {
            var aProp = GetCleanSo(a).GetIterator();
            var bProp = GetCleanSo(b).GetIterator();
            while (true) {
                bool match;
                if (aProp.propertyType != bProp.propertyType) return true;
                if (aProp.propertyPath != bProp.propertyPath) return true;

                var type = aProp.propertyType;
                if (bToAChildMapper != null
                    && type == SerializedPropertyType.ObjectReference
                    && aProp.objectReferenceValue != null
                    && bProp.objectReferenceValue != null
                    && bToAChildMapper(bProp.objectReferenceValue) == aProp.objectReferenceValue
                ) {
                    match = true;
                } else if (type == SerializedPropertyType.Generic) {
                    match = true;
                } else {
                    match = SerializedProperty.DataEquals(aProp, bProp);
                }

                if (!match) {
                    Debug.Log("Failed on " + aProp.propertyPath);
                    return true;
                }
                    
                var enterChildren = type == SerializedPropertyType.Generic;
                var aDone = !aProp.Next(enterChildren);
                var bDone = !bProp.Next(enterChildren);
                if (aDone || bDone) return aDone != bDone;
            }
        }

        /*
        public void RevertUnchanged() {
            var reverseLookup = originalToMutable.ToDictionary(x => x.Value, x => x.Key);
            
            bool IsModified2(Object copy) {
                if (!reverseLookup.TryGetValue(copy, out var original)) {
                    return true;
                }
                return IsModified(original, copy, copyChild =>
                    reverseLookup.TryGetValue(copyChild, out var originalChild) ? originalChild : copyChild);
            }

            var mutables = originalToMutable.Values
                .Where(m => !IsType(m, typesToNeverRevert))
                .ToArray();
            var unmodifiedMutables = mutables
                .Where(m => !IsModified2(m))
                .ToImmutableHashSet();
            
            var cachedChildren = new Dictionary<Object, HashSet<Object>>(); 
            HashSet<Object> GetChildren(Object obj) {
                if (cachedChildren.TryGetValue(obj, out var cached)) return cached;
                var children = new HashSet<Object>();
                cachedChildren[obj] = children;
                // TODO: When this was written ForEachChild didn't include the root. NOW IT DOES.
                ForEachChild(obj, child => {
                    if (mutables.Contains(child)) {
                        children.Add(child);
                        children.UnionWith(GetChildren(child));
                    }
                    return false;
                });
                return children;
            }

            var mutablesToRevert = unmodifiedMutables
                .Where(m => GetChildren(m).All(child => unmodifiedMutables.Contains(child)))
                .ToArray();

            var revertMap = mutablesToRevert
                .ToDictionary(m => m, m => reverseLookup[m]);

            Debug.Log("Reverting");
            RewriteAll(revertMap);
            foreach (var mutable in mutablesToRevert) {
                AssetDatabase.RemoveObjectFromAsset(mutable);
                Object.DestroyImmediate(mutable);
            }
            Debug.Log($"Reverted {mutablesToRevert.Length} of {mutables.Length} assets");
        }
        */

        private bool IsType(Object obj, Type[] types) =>
            types.Any(type => type.IsInstanceOfType(obj));
    }
}
