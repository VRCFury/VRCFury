using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VF.Utils.Controller;

namespace VF.Utils {
    // Traverses animator graphs, including shared motions and additive reference poses.
    internal static class AnimatorIterator {

        public abstract class Iterator<T> {
            public virtual IImmutableSet<T> From(VFMotion root) {
                return ImmutableHashSet<T>.Empty;
            }
            public IImmutableSet<T> From(VFState root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                return From(root.motion);
            }
            public virtual IImmutableSet<T> From(VFLayer root) {
                return new States().From(root).SelectMany(From).ToImmutableHashSet();
            }

            public virtual IImmutableSet<T> From(IEnumerable<VFLayer> layers) {
                if (layers == null) return ImmutableHashSet<T>.Empty;
                return layers.SelectMany(From).ToImmutableHashSet();
            }

            public IImmutableSet<T> From(VFController root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                return From(root.GetLayers());
            }
        }

        public class States : Iterator<VFState> {
            public override IImmutableSet<VFState> From(VFLayer root) {
                return root.allStates.ToImmutableHashSet();
            }
        }

        public abstract class MotionIterator<T> : Iterator<T> where T : VFMotion {
            public override IImmutableSet<T> From(VFMotion root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                if (root is VFClip clip && clip.GetAdditiveReferencePoseClip() == null) {
                    return root is T match ? ImmutableHashSet.Create(match) : ImmutableHashSet<T>.Empty;
                }
                return Collect(new[] { root });
            }

            public override IImmutableSet<T> From(VFLayer root) {
                return Collect(root.allStates.Select(state => state?.motion));
            }

            public override IImmutableSet<T> From(IEnumerable<VFLayer> layers) {
                if (layers == null) return ImmutableHashSet<T>.Empty;
                return Collect(layers.SelectMany(layer => layer.allStates).Select(state => state?.motion));
            }

            private static IImmutableSet<T> Collect(IEnumerable<VFMotion> roots) {
                var visited = new HashSet<VFMotion>();
                var output = ImmutableHashSet.CreateBuilder<T>();
                var stack = new Stack<VFMotion>(roots);
                while (stack.Count > 0) {
                    var motion = stack.Pop();
                    if (motion == null || !visited.Add(motion)) continue;
                    if (motion is T match) output.Add(match);
                    if (motion is VFTree tree) {
                        foreach (var child in tree.children) stack.Push(child.motion);
                    } else if (motion is VFClip clip) {
                        stack.Push(clip.GetAdditiveReferencePoseClip());
                    }
                }
                return output.ToImmutable();
            }
        }

        public class Motions : MotionIterator<VFMotion> { }
        public class Clips : MotionIterator<VFClip> { }
        public class Trees : MotionIterator<VFTree> { }
    }
}
