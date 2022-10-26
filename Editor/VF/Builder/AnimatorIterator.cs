using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Builder {
    /**
     * Collects the resting value for every animated property in an animator, and puts them all into a clip.
     */
    public static class AnimatorIterator {
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
        
        public static void ForEachClip(AnimatorState state, Action<AnimationClip> action) {
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
        }

        public static void ForEachClip(AnimatorControllerLayer layer, Action<AnimationClip> action) {
            ForEachState(layer, state => {
                ForEachClip(state, action);
            });
        }
        
        public static void ForEachBlendTree(AnimatorState state, Action<BlendTree> action) {
            var motions = new Stack<Motion>();
            motions.Push(state.motion);
            while (motions.Count > 0) {
                var motion = motions.Pop();
                if (motion == null) continue;
                switch (motion) {
                    case BlendTree tree:
                        action.Invoke(tree);
                        foreach (var child in tree.children) {
                            motions.Push(child.motion);
                        }
                        break;
                }
            }
        }
        
        public static void ForEachBlendTree(AnimatorControllerLayer layer, Action<BlendTree> action) {
            ForEachState(layer, state => {
                ForEachBlendTree(state, action);
            });
        }
    }
}
