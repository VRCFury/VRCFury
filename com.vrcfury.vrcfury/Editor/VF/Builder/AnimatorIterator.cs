using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using AnimatorStateExtensions = VF.Builder.AnimatorStateExtensions;

namespace VF.Builder {
    /**
     * Collects the resting value for every animated property in an animator, and puts them all into a clip.
     */
    public static class AnimatorIterator {
        public static void ForEachBehaviourRW(
            AnimatorStateMachine root,
            Func<StateMachineBehaviour, Func<Type, StateMachineBehaviour>, bool> action
        ) {
            foreach (var stateMachine in GetAllStateMachines(root)) {
                for (var i = 0; i < stateMachine.behaviours.Length; i++) {
                    var keep = action(stateMachine.behaviours[i], type => stateMachine.VAddStateMachineBehaviour(type));
                    if (!keep) {
                        var behaviours = stateMachine.behaviours.ToList();
                        behaviours.RemoveAt(i);
                        stateMachine.behaviours = behaviours.ToArray();
                        i--;
                    }
                }
            }
            foreach (var state in new States().From(root)) {
                for (var i = 0; i < state.behaviours.Length; i++) {
                    var keep = action(state.behaviours[i], type => state.VAddStateMachineBehaviour(type));
                    if (!keep) {
                        var behaviours = state.behaviours.ToList();
                        behaviours.RemoveAt(i);
                        state.behaviours = behaviours.ToArray();
                        i--;
                    }
                }
            }
        }

        public static void ReplaceClips(AnimatorController controller, Func<AnimationClip, AnimationClip> replace) {
            foreach (var state in new States().From(controller)) {
                var motions = new Stack<(Motion, Func<Motion,Motion>)>();
                motions.Push((state.motion, m => state.motion = m));
                while (motions.Count > 0) {
                    var (motion, setMotion) = motions.Pop();
                    if (motion == null) continue;
                    switch (motion) {
                        case AnimationClip clip:
                            setMotion(replace(clip));
                            break;
                        case BlendTree tree:
                            var children = tree.children;
                            for (var i = 0; i < children.Length; i++) {
                                var childNum = i;
                                var child = children[childNum];
                                motions.Push((child.motion, m => child.motion = m));
                            }
                            break;
                    }
                }
            }
        }

        public abstract class Iterator<T> {
            public virtual IImmutableSet<T> From(Motion root) {
                return ImmutableHashSet<T>.Empty;
            }
            public IImmutableSet<T> From(AnimatorState root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                return From(root.motion);
            }
            public virtual IImmutableSet<T> From(AnimatorStateMachine root) {
                return new States().From(root).SelectMany(From).ToImmutableHashSet();
            }

            public IImmutableSet<T> From(AnimatorControllerLayer root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                return From(root.stateMachine);
            }

            public IImmutableSet<T> From(AnimatorController root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                return root.layers.SelectMany(From).ToImmutableHashSet();
            }
        }

        private static IImmutableSet<T> GetRecursive<T>(T root, Func<T, IEnumerable<T>> getChildren) {
            var all = new HashSet<T>();
            var stack = new Stack<T>();
            stack.Push(root);
            while (stack.Count > 0) {
                var one = stack.Pop();
                if (one == null) continue;
                if (all.Contains(one)) continue;
                all.Add(one);
                getChildren(one).ToList().ForEach(stack.Push);
            }
            return all.ToImmutableHashSet();
        }
        
        private static IImmutableSet<AnimatorStateMachine> GetAllStateMachines(AnimatorStateMachine root) {
            return GetRecursive(root, sm => sm.stateMachines
                .Select(c => c.stateMachine)
            );
        }

        public class States : Iterator<AnimatorState> {
            public override IImmutableSet<AnimatorState> From(AnimatorStateMachine root) {
                return GetAllStateMachines(root)
                    .SelectMany(sm => sm.states)
                    .Select(c => c.state)
                    .Where(state => state != null)
                    .ToImmutableHashSet();
            }
        }
        
        public class Transitions : Iterator<AnimatorTransitionBase> {
            public override IImmutableSet<AnimatorTransitionBase> From(AnimatorStateMachine root) {
                var states = new States().From(root);
                return GetAllStateMachines(root)
                    .SelectMany(sm =>
                        sm.entryTransitions
                            .Concat<AnimatorTransitionBase>(sm.anyStateTransitions)
                            .Concat(sm.stateMachines.SelectMany(childSm => sm.GetStateMachineTransitions(childSm.stateMachine)))
                    )
                    .Concat(states.SelectMany(state => state.transitions))
                    .Where(transition => transition != null)
                    .ToImmutableHashSet();
            }
        }
        
        public class Conditions : Iterator<AnimatorCondition> {
            public override IImmutableSet<AnimatorCondition> From(AnimatorStateMachine root) {
                return new Transitions().From(root)
                    .SelectMany(t => t.conditions)
                    .ToImmutableHashSet();
            }
        }

        public class Motions : Iterator<Motion> {
            public override IImmutableSet<Motion> From(Motion root) {
                return GetRecursive(root, motion => {
                    if (motion is BlendTree tree) {
                        return tree.children.Select(child => child.motion);
                    }

                    return new Motion[] { };
                });
            }
        }

        public class Clips : Iterator<AnimationClip> {
            public override IImmutableSet<AnimationClip> From(Motion root) {
                return new Motions().From(root).OfType<AnimationClip>().ToImmutableHashSet();
            }
        }
        
        public class Trees : Iterator<BlendTree> {
            public override IImmutableSet<BlendTree> From(Motion root) {
                return new Motions().From(root).OfType<BlendTree>().ToImmutableHashSet();
            }
        }
        
        public class Behaviours : Iterator<StateMachineBehaviour> {
            public override IImmutableSet<StateMachineBehaviour> From(AnimatorStateMachine root) {
                var all = new HashSet<StateMachineBehaviour>();
                ForEachBehaviourRW(root, (b, add) => {
                    all.Add(b);
                    return true;
                });
                return all.ToImmutableHashSet();
            }
        }
    }
}
