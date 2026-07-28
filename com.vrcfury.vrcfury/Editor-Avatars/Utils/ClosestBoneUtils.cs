using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Hooks;
using VF.Injector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    [VFService]
    internal class ClosestBoneUtils {
        private static readonly Dictionary<VFGameObject, ClosestBoneUtils> perFrame
            = new Dictionary<VFGameObject, ClosestBoneUtils>();

        private readonly VRCFObjectPathCache objectPaths;
        private readonly VRCFArmatureCache armatureCache;
        private readonly Dictionary<VFGameObject, HumanBodyBones?> results = new();
        private readonly Dictionary<VFGameObject, List<ArmatureLink>> armatureLinks = new();

        [VFAutowired]
        public ClosestBoneUtils(VRCFObjectPathCache objectPaths, VRCFArmatureCache armatureCache) {
            this.objectPaths = objectPaths;
            this.armatureCache = armatureCache;
        }

        public static ClosestBoneUtils GetPerFrame(VFGameObject avatarObject) {
            return perFrame.GetOrCreate(
                avatarObject,
                () => new ClosestBoneUtils(
                    VRCFObjectPathCache.GetPerFrame(avatarObject),
                    VRCFArmatureCache.GetPerFrame(avatarObject)
                )
            );
        }

        [VFInit]
        private static void Init() {
            Scheduler.Schedule(perFrame.Clear, 0);
        }

        private List<ArmatureLink> GetArmatureLinks(VFGameObject rootObject) {
            if (armatureLinks.TryGetValue(rootObject, out var cached)) return cached;
            return armatureLinks[rootObject] = rootObject
                .GetComponentsInSelfAndChildren<VRCFury>()
                .SelectMany(v => v.GetAllFeatures())
                .OfType<ArmatureLink>()
                .ToList();
        }

        public HumanBodyBones? GetClosestHumanoidBone(VFGameObject obj) {
            return results.GetOrCreate(obj, () => GetClosestHumanoidBoneUncached(obj));
        }

        [CanBeNull]
        public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) {
            return armatureCache.FindBoneOnArmatureOrNull(bone);
        }

        private HumanBodyBones? GetClosestHumanoidBoneUncached(VFGameObject obj) {
            var avatarObject = obj.GetAvatarRoot();

            var followConstraints = true;
            var followArmatureLink = true;

            var armatureLinks = GetArmatureLinks(avatarObject);

            var humanoidBones = armatureCache.GetAllBones()
                .ToDictionary(x => x.Value, x => x.Key);
            var alreadyChecked = new HashSet<VFGameObject>();
            var current = obj;
            while (current != null) {
                if (humanoidBones.TryGetValue(current, out var bone))
                    return bone;

                alreadyChecked.Add(current);

                if (followArmatureLink) {
                    VFGameObject foundParent = null;
                    foreach (var armatureLink in armatureLinks) {
                        var p = ArmatureLinkService.GetProbableParent(armatureLink, avatarObject, current, objectPaths, armatureCache);
                        if (p != null && !alreadyChecked.Contains(p)) {
                            foundParent = p;
                            break;
                        }
                    }

                    if (foundParent != null) {
                        current = foundParent;
                        continue;
                    }
                }
                
                if (followConstraints) {
                    var positionTo = current.GetConstraints()
                        .Where(c => c.IsParent() || c.IsPosition())
                        .Select(c => c.GetFirstSource())
                        .NotNull()
                        .FirstOrDefault();
                    if (positionTo != null && !alreadyChecked.Contains(positionTo)) {
                        current = positionTo;
                        continue;
                    }
                }
                current = current.parent;
            }
            return null;
        }
    }

    [VFService]
    internal class SpsAvatarAutoTagGenerator : SpsAutoTagGenerator {
        [VFAutowired] private readonly ClosestBoneUtils closestBoneUtils;
        [VFAutowired] [CanBeNull] private readonly VRCAvatarDescriptor avatar;

        public HumanBodyBones? GetClosestBone(VFGameObject obj) {
            return closestBoneUtils.GetClosestHumanoidBone(obj);
        }

        [CanBeNull]
        public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) {
            return closestBoneUtils.GetBone(obj, bone);
        }

        public Vector3? GetAvatarViewPosition(VFGameObject obj) {
            return (avatar ?? obj.GetAvatarRoot().GetComponent<VRCAvatarDescriptor>())?.ViewPosition;
        }
    }
}
