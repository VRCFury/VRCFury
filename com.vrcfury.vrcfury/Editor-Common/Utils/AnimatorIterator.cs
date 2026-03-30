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

        public static IImmutableSet<T> GetRecursive<T>(T root, Func<T, IEnumerable<T>> getChildren) where T : Object {
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

        public class States : Iterator<AnimatorState> {
            public override IImmutableSet<AnimatorState> From(VFLayer root) {
                return root.allStates;
            }
        }

        public class Motions : Iterator<Motion> {
            public override IImmutableSet<Motion> From(Motion root) {
                return GetRecursive(root, motion => {
                    if (motion is BlendTree tree) {
                        return tree.children.Select(child => child.motion);
                    }
                    if (motion is AnimationClip clip) {
                        var settings = AnimationUtility.GetAnimationClipSettings(clip);
                        return new Motion[] { settings.additiveReferencePoseClip };
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
    }
}
