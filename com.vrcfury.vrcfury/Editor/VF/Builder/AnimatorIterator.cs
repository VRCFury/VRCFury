using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;
using VF.Utils.Controller;
using Object = UnityEngine.Object;

namespace VF.Builder {
    /**
     * Collects the resting value for every animated property in an animator, and puts them all into a clip.
     */
    internal static class AnimatorIterator {
        private static ISet<VFBehaviourContainer> GetAllBehaviourContainers(VFLayer layer) {
            var set = new HashSet<VFBehaviourContainer>();
            foreach (var sm in GetAllStateMachines(layer)) {
                set.Add(new VFStateMachine(sm));
                foreach (var child in sm.states) {
                    set.Add(new VFState(child, sm));
                }
            }
            return set;
        }

        public static void ForEachBehaviourRW(
            VFLayer layer,
            Func<StateMachineBehaviour, OneOrMany<StateMachineBehaviour>> action
        ) {
            foreach (var container in GetAllBehaviourContainers(layer)) {
                container.behaviours = container.behaviours.SelectMany(b => action(b).Get()).ToArray();
            }
        }

        public static void RewriteConditions(
            VFLayer root,
            Func<AnimatorCondition, AnimatorTransitionBaseExtensions.Rewritten> action
        ) {
            ForEachTransitionRW(root, t => t.RewriteConditions(action));
        }

        public static void ForEachTransitionRW(
            VFLayer root,
            Func<AnimatorTransitionBase, OneOrMany<AnimatorTransitionBase>> action
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
            Func<AnimatorTransitionBase, OneOrMany<AnimatorTransitionBase>> action
        ) where T : AnimatorTransitionBase {
            var changed = false;
            var output = input.SelectMany(oneTransition => {
                var result = action(oneTransition).Get();
                changed |= result.Count != 1 || result[0] != oneTransition;
                return result;
            }).OfType<T>().ToArray();
            if (changed) setter(output);
        }

        public static void ReplaceClips(AnimatorController controller, Func<AnimationClip, AnimationClip> replace) {
            Motion RewriteMotion(Motion motion) {
                if (motion is AnimationClip clip) {
                    return replace(clip);
                }
                if (motion is BlendTree tree) {
                    tree.RewriteChildren(child => {
                        child.motion = RewriteMotion(child.motion);
                        return child;
                    });
                    return tree;
                }
                return motion;
            }
            
            foreach (var state in new States().From(controller)) {
                state.motion = RewriteMotion(state.motion);
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
            
            public IImmutableSet<T> From(AnimatorController root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                return From(new VFController(root));
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
                return GetAllBehaviourContainers(root)
                    .SelectMany(c => c.behaviours)
                    .ToImmutableHashSet();
            }
        }
    }
}
