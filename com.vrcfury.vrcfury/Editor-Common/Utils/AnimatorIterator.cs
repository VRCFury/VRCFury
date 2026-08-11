using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VF.Utils.Controller;

namespace VF.Utils {
    /**
     * Collects the resting value for every animated property in an animator, and puts them all into a clip.
     */
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

            public IImmutableSet<T> From(IEnumerable<VFLayer> layers) {
                if (layers == null) return ImmutableHashSet<T>.Empty;
                return layers.SelectMany(From).ToImmutableHashSet();
            }

            public IImmutableSet<T> From(VFController root) {
                if (root == null) return ImmutableHashSet<T>.Empty;
                return From(root.GetLayers());
            }
        }

        public static IImmutableSet<T> GetRecursive<T>(T root, Func<T, IEnumerable<T>> getChildren) {
            var all = new HashSet<T>();
            var stack = new Stack<T>();
            stack.Push(root);
            while (stack.Count > 0) {
                var one = stack.Pop();
                if (one == null) continue;
                if (all.Contains(one)) continue;
                all.Add(one);
                foreach (var child in getChildren(one)) {
                    stack.Push(child);
                }
            }
            return all.ToImmutableHashSet();
        }

        public class States : Iterator<VFState> {
            public override IImmutableSet<VFState> From(VFLayer root) {
                return root.allStates.ToImmutableHashSet();
            }
        }

        public class Motions : Iterator<VFMotion> {
            public override IImmutableSet<VFMotion> From(VFMotion root) {
                if (root == null) return ImmutableHashSet<VFMotion>.Empty;
                return GetRecursive(root, motion => {
                    if (motion is VFTree tree) {
                        return tree.children.Select(child => child.motion);
                    }
                    if (motion is VFClip clip) {
                        return new VFMotion[] { clip.GetAdditiveReferencePoseClip() };
                    }
                    return Array.Empty<VFMotion>();
                });
            }
        }

        public class Clips : Iterator<VFClip> {
            public override IImmutableSet<VFClip> From(VFMotion root) {
                return new Motions().From(root).OfType<VFClip>().ToImmutableHashSet();
            }
        }

        public class Trees : Iterator<VFTree> {
            public override IImmutableSet<VFTree> From(VFMotion root) {
                return new Motions().From(root).OfType<VFTree>().ToImmutableHashSet();
            }
        }
    }
}
