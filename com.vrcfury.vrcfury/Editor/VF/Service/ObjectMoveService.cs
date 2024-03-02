using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            var immovableBones = new HashSet<VFGameObject>();
            immovableBones.Add(manager.AvatarObject);
            // Eyes are weird, because vrc takes full control of them, and we move them as part of the crosseye fix, so ignore them
            var leftEye = VRCFArmatureUtils.FindBoneOnArmatureOrNull(manager.AvatarObject, HumanBodyBones.LeftEye);
            var rightEye = VRCFArmatureUtils.FindBoneOnArmatureOrNull(manager.AvatarObject, HumanBodyBones.RightEye);
            foreach (var bone in VRCFArmatureUtils.GetAllBones(manager.AvatarObject)) {
                if (bone == leftEye || bone == rightEye) continue;
                var current = bone;
                while (current != null && current != manager.AvatarObject) {
                    immovableBones.Add(current);
                    current = current.parent;
                }
            }
            
            if (immovableBones.Contains(obj)) {
                throw new Exception(
                    $"VRCFury is trying to move the {obj.name} object, but bones / root avatar objects cannot be moved." +
                    $" You are probably trying to do something weird in one of your VRCFury components. Don't do that.");
            }
            
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
