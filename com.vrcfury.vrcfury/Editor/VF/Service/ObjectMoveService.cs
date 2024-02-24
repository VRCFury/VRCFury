using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /** This builder is responsible for moving objects for other builders,
     * then fixing any animations that referenced those objects.
     *
     * The reason we can't just move objects and rewrite the animations immediately when needed,
     * is because some animations may not be present on the avatar yet. Specifically, FullController
     * may add more animations to the avatar later on, and those may use the pre-moved paths.
     */
    [VFService]
    public class ObjectMoveService {
        [VFAutowired] private readonly ClipBuilderService clipBuilder;
        [VFAutowired] private readonly AvatarManager manager;

        private readonly List<(string, string)> deferred = new List<(string, string)>();
        private readonly List<AnimationClip> additionalClips = new List<AnimationClip>();

        public void Move(VFGameObject obj, VFGameObject newParent = null, string newName = null, bool worldPositionStays = true, bool defer = false) {
            var oldPath = clipBuilder.GetPath(obj);
            if (newParent != null)
                obj.SetParent(newParent, worldPositionStays);
            if (newName != null)
                obj.name = newName;
            obj.EnsureAnimationSafeName();
            var newPath = clipBuilder.GetPath(obj);
            PhysboneUtils.RemoveFromPhysbones(obj, true);
            deferred.Add((oldPath, newPath));
            if (!defer) {
                ApplyDeferred();
            }
        }
        
        public void ApplyDeferred() {
            var rewriter = AnimationRewriter.RewritePath(path => {
                foreach (var (from, to) in deferred) {
                    if (path.StartsWith(from + "/") || path == from) {
                        path = to + path.Substring(from.Length);
                    }
                }
                return path;
            });

            foreach (var controller in manager.GetAllUsedControllers()) {
                ((AnimatorController)controller.GetRaw()).Rewrite(rewriter);
            }
            foreach (var clip in additionalClips) {
                clip.Rewrite(rewriter);
            }
            deferred.Clear();
        }

        public void AddAdditionalManagedClip(AnimationClip clip) {
            additionalClips.Add(clip);
        }
    }
}
