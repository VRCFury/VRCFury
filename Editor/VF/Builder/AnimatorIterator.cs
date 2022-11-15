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
        
        public static void ForEachClip(AnimatorState state, Action<AnimationClip, Action<Motion>> action) {
            var motions = new Stack<Tuple<Motion, Action<Motion>>>();
            motions.Push(Tuple.Create(state.motion, (Action<Motion>)(m => state.motion = m)));
            while (motions.Count > 0) {
                var motion = motions.Pop();
                if (motion == null) continue;
                switch (motion.Item1) {
                    case AnimationClip clip:
                        action(clip, motion.Item2);
                        break;
                    case BlendTree tree:
                        var children = tree.children;
                        for (var i = 0; i < children.Length; i++) {
                            var childNum = i;
                            var child = children[childNum];
                            motions.Push(Tuple.Create(child.motion, (Action<Motion>)(m => {
                                child.motion = m;
                                children[childNum] = child;
                                tree.children = children;
                            })));
                        }
                        break;
                }
            }
        }

        public static void ForEachClip(AnimatorControllerLayer layer, Action<AnimationClip, Action<Motion>> action) {
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
