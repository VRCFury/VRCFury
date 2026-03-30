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
    internal class ObjectMoveService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly AllClipsService allClipsService;

        private readonly List<(string, string)> deferred = new List<(string, string)>();

        public void Move(VFGameObject obj, VFGameObject newParent = null, string newName = null, bool worldPositionStays = true, bool defer = false) {
            var immovableBones = new HashSet<VFGameObject>();
            immovableBones.Add(avatarObject);
            // Eyes are weird, because vrc takes full control of them, and we move them as part of the crosseye fix, so ignore them
            foreach (var pair in VRCFArmatureUtils.GetAllBones(avatarObject)) {
                var bone = pair.Key;
                var boneObj = pair.Value;
                if (bone == HumanBodyBones.LeftEye || bone == HumanBodyBones.RightEye) continue;
                var current = boneObj;
                while (current != null && current != avatarObject) {
                    immovableBones.Add(current);
                    current = current.parent;
                }
            }
            
            if (immovableBones.Contains(obj)) {
                throw new Exception(
                    $"VRCFury is trying to move the {obj.name} object, but bones / root avatar objects cannot be moved." +
                    $" You are probably trying to do something weird in one of your VRCFury components. Don't do that.");
            }
            
            var oldPath = obj.GetAnimatedPath();
            if (newParent != null)
                obj.SetParent(newParent, worldPositionStays);
            if (newName != null)
                obj.name = newName;
            obj.EnsureAnimationSafeName();
            var newPath = obj.GetAnimatedPath();
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

            allClipsService.RewriteAllClips(rewriter);
            deferred.Clear();
        }
    }
}
