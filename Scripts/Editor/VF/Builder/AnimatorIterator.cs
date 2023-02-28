using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Builder {
    /**
     * Collects the resting value for every animated property in an animator, and puts them all into a clip.
     */
    public static class AnimatorIterator {
        public static void ForEachStateMachine(AnimatorStateMachine root, Action<AnimatorStateMachine> action) {
            var stateMachines = new Stack<AnimatorStateMachine>();
            stateMachines.Push(root);

            while (stateMachines.Count > 0) {
                var stateMachine = stateMachines.Pop();
                foreach (var sub in stateMachine.stateMachines)
                    stateMachines.Push(sub.stateMachine);
                action(stateMachine);
            }
        }
        
        public static void ForEachState(AnimatorStateMachine root, Action<AnimatorState> action) {
            ForEachStateMachine(root, stateMachine => {
                foreach (var state in stateMachine.states)
                    action(state.state);
            });
        }

        public static void ForEachBehaviour(AnimatorStateMachine root, Action<StateMachineBehaviour> action) {
            ForEachBehaviourRW(root, (b, add) => {
                action(b);
                return true;
            });
        }
        public static void ForEachBehaviourRW(
            AnimatorStateMachine root,
            Func<StateMachineBehaviour, Func<Type, StateMachineBehaviour>, bool> action
        ) {
            ForEachStateMachine(root, stateMachine => {
                for (var i = 0; i < stateMachine.behaviours.Length; i++) {
                    var keep = action(stateMachine.behaviours[i], type => VRCFAnimatorUtils.AddStateMachineBehaviour(stateMachine, type));
                    if (!keep) {
                        var behaviours = stateMachine.behaviours.ToList();
                        behaviours.RemoveAt(i);
                        stateMachine.behaviours = behaviours.ToArray();
                        i--;
                    }
                }
            });
            ForEachState(root, state => {
                for (var i = 0; i < state.behaviours.Length; i++) {
                    var keep = action(state.behaviours[i], type => VRCFAnimatorUtils.AddStateMachineBehaviour(state, type));
                    if (!keep) {
                        var behaviours = state.behaviours.ToList();
                        behaviours.RemoveAt(i);
                        state.behaviours = behaviours.ToArray();
                        i--;
                    }
                }
            });
        }

        public static void ForEachTransition(AnimatorStateMachine root, Action<AnimatorTransitionBase> action) {
            ForEachStateMachine(root, sm => {
                foreach (var childSm in sm.stateMachines) {
                    foreach (var t in sm.GetStateMachineTransitions(childSm.stateMachine)) action(t);
                }
                foreach (var t in sm.entryTransitions) action(t);
                foreach (var t in sm.anyStateTransitions) action(t);
                foreach (var child in sm.states) {
                    foreach (var t in child.state.transitions) action(t);
                }
            });
        }
        
        public static void ForEachClip(AnimatorState state, Action<AnimationClip> action) {
            ForEachClip(state.motion, action);
        }
        
        public static void ForEachClip(Motion root, Action<AnimationClip> action) {
            action = NoDupesWrapper(action);

            var motions = new Stack<Motion>();
            motions.Push(root);
            while (motions.Count > 0) {
                var motion = motions.Pop();
                if (motion == null) continue;
                switch (motion) {
                    case AnimationClip clip:
                        action(clip);
                        break;
                    case BlendTree tree:
                        var children = tree.children;
                        for (var i = 0; i < children.Length; i++) {
                            var childNum = i;
                            var child = children[childNum];
                            motions.Push(child.motion);
                        }
                        break;
                }
            }
        }

        public static void ForEachClip(AnimatorStateMachine root, Action<AnimationClip> action) {
            action = NoDupesWrapper(action);

            ForEachState(root, state => {
                ForEachClip(state, action);
            });
        }
        
        public static void ForEachBlendTree(AnimatorState state, Action<BlendTree> action) {
            action = NoDupesWrapper(action);

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
        
        public static void ForEachBlendTree(AnimatorStateMachine root, Action<BlendTree> action) {
            action = NoDupesWrapper(action);
            ForEachState(root, state => {
                ForEachBlendTree(state, action);
            });
        }

        private static Action<T> NoDupesWrapper<T>(Action<T> action) {
            HashSet<T> visited = new HashSet<T>();
            return entry => {
                if (visited.Contains(entry)) return;
                visited.Add(entry);
                action(entry);
            };
        }
    }
}
