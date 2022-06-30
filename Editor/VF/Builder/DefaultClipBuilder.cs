using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Builder {
    /**
     * Collects the resting value for every animated property in an animator, and puts them all into a clip.
     */
    public static class DefaultClipBuilder {
        public static void ForEachState(AnimatorControllerLayer layer, Action<AnimatorState> action) {
            var stateMachines = new Stack<AnimatorStateMachine>();
            stateMachines.Push(layer.stateMachine);

            while (stateMachines.Count > 0) {
                var stateMachine = stateMachines.Pop();
                foreach (var sub in stateMachine.stateMachines)
                    stateMachines.Push(sub.stateMachine);
                foreach (var state in stateMachine.states)
                    action(state.state);
            }
        }

        public static void ForEachClip(AnimatorControllerLayer layer, Action<AnimationClip> action) {
            ForEachState(layer, state => {
                var motions = new Stack<Motion>();
                motions.Push(state.motion);
                while (motions.Count > 0) {
                    var motion = motions.Pop();
                    if (motion == null) continue;
                    switch (motion) {
                        case AnimationClip clip:
                            action(clip);
                            break;
                        case BlendTree tree:
                            foreach (var child in tree.children) {
                                motions.Push(child.motion);
                            }
                            break;
                    }
                }
            });
        }
        
        public static void CollectDefaults(AnimatorControllerLayer layer, AnimationClip defaultClip, GameObject baseObject) {
            ForEachClip(layer, clip => {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                    var exists = AnimationUtility.GetFloatValue(baseObject, binding, out var value);
                    if (exists) {
                        AnimationUtility.SetEditorCurve(defaultClip, binding, ClipBuilder.OneFrame(value));
                    } else if (binding.path != "_ignored") {
                        Debug.LogWarning("Missing default value for '" + binding.path + "' in " + layer.name);
                    }
                }
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                    var exists = AnimationUtility.GetObjectReferenceValue(baseObject, binding, out var value);
                    if (exists) {
                        AnimationUtility.SetObjectReferenceCurve(defaultClip, binding, ClipBuilder.OneFrame(value));
                    } else if (binding.path != "_ignored") {
                        Debug.LogWarning("Missing default value for '" + binding.path + "' in " + layer.name);
                    }
                }
            });
        }
    }
}
