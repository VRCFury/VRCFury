using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils.Controller;
using AnimatorStateExtensions = VF.Builder.AnimatorStateExtensions;
using Object = UnityEngine.Object;

namespace VF.Builder {
    /**
     * Collects the resting value for every animated property in an animator, and puts them all into a clip.
     */
    public static class AnimatorIterator {
        public static void ForEachBehaviourRW(
            VFLayer layer,
            Func<StateMachineBehaviour, Func<Type, StateMachineBehaviour>, bool> action
        ) {
            foreach (var stateMachine in GetAllStateMachines(layer)) {
                foreach (var behaviour in stateMachine.behaviours.ToArray()) {
                    var keep = action(behaviour, type => stateMachine.VAddStateMachineBehaviour(type));
                    if (!keep) stateMachine.behaviours = stateMachine.behaviours.Where(b => b != behaviour).ToArray();
                }
            }
            foreach (var state in new States().From(layer)) {
                foreach (var behaviour in state.behaviours.ToArray()) {
                    var keep = action(behaviour, type => state.VAddStateMachineBehaviour(type));
                    if (!keep) state.behaviours = state.behaviours.Where(b => b != behaviour).ToArray();
                }
            }
        }

        public static void ForEachTransitionRW(
            VFLayer root,
            Func<AnimatorTransitionBase, IList<AnimatorTransitionBase>> action
        ) {
            foreach (var sm in GetAllStateMachines(root)) {
                ForEachTransitionRW(sm.entryTransitions, a => sm.entryTransitions = a, action);
                ForEachTransitionRW(sm.anyStateTransitions, a => sm.anyStateTransitions = a, action);
                foreach (var childSm in sm.stateMachines) {
                    ForEachTransitionRW(sm.GetStateMachineTransitions(childSm.stateMachine), a => sm.SetStateMachineTransitions(childSm.stateMachine, a), action);
                }
            }
            foreach (var state in new States().From(root)) {
                ForEachTransitionRW(state.transitions, a => state.transitions = a, action);
            }
        }

        private static void ForEachTransitionRW<T>(
            T[] input,
            Action<T[]> setter,
            Func<AnimatorTransitionBase, IList<AnimatorTransitionBase>> action
        ) where T : AnimatorTransitionBase {
            var changed = false;
            var output = input.SelectMany(oneTransition => {
                var result = action(oneTransition);
                changed |= result.Count != 1 || result[0] != oneTransition;
                return result;
            }).OfType<T>().ToArray();
            if (changed) setter(output);
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
            public virtual IImmutableSet<T> From(VFLayer root) {
                return new States().From(root).SelectMany(From).ToImmutableHashSet();
            }
            
            public IImmutableSet<T> From(IEnumerable<VFLayer> layers) {
                return layers.SelectMany(From).ToImmutableHashSet();
            }

            public IImmutableSet<T> From(VFController root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                return From(root.GetLayers());
            }
        }

        private static IImmutableSet<T> GetRecursive<T>(T root, Func<T, IEnumerable<T>> getChildren) where T : Object {
            var all = new HashSet<T>();
            var stack = new Stack<T>();
            stack.Push(root);
            while (stack.Count > 0) {
                var one = stack.Pop();
                if (one == null) continue;
                if (all.Contains(one)) continue;
                all.Add(one);
                foreach (var child in getChildren(one)) {
                    if (child != null && !(child is T)) {
                        throw new Exception(
                            $"{root.name} contains a child object that is not of type {typeof(T).Name}." +
                            $" This should be impossible, and is usually a sign of cache memory corruption within unity. Try reimporting or renaming the file" +
                            $" containing this resource. ({AssetDatabase.GetAssetPath(root)})");
                    }
                    stack.Push(child);
                }
            }
            return all.ToImmutableHashSet();
        }
        
        public static IImmutableSet<AnimatorStateMachine> GetAllStateMachines(AnimatorStateMachine root) {
            return GetRecursive(root, sm => sm.stateMachines
                .Select(c => c.stateMachine)
            );
        }

        public class States : Iterator<AnimatorState> {
            public override IImmutableSet<AnimatorState> From(VFLayer root) {
                return GetAllStateMachines(root)
                    .SelectMany(sm => sm.states)
                    .Select(c => c.state)
                    .Where(state => state != null)
                    .ToImmutableHashSet();
            }
        }
        
        public class Transitions : Iterator<AnimatorTransitionBase> {
            public override IImmutableSet<AnimatorTransitionBase> From(VFLayer root) {
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
            public override IImmutableSet<AnimatorCondition> From(VFLayer root) {
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
            public override IImmutableSet<StateMachineBehaviour> From(VFLayer root) {
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
