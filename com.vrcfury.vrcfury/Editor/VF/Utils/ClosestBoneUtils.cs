using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;
using VF.Service;

namespace VF.Utils {
    internal static class ClosestBoneUtils {
        private static Dictionary<VFGameObject, Result> resultCache
            = new Dictionary<VFGameObject, Result>();
        private static Dictionary<VFGameObject, List<ArmatureLink>> armatureLinkCache
            = new Dictionary<VFGameObject, List<ArmatureLink>>();

        private class Result {
            public HumanBodyBones? bone;
        }

        public static void ClearCache() {
            resultCache.Clear();
            armatureLinkCache.Clear();
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            Scheduler.Schedule(ClearCache, 1000);
        }

        private static List<ArmatureLink> GetArmatuareLinks(VFGameObject rootObject) {
            if (armatureLinkCache.TryGetValue(rootObject, out var cached)) return cached;
            return armatureLinkCache[rootObject] = rootObject
                .GetComponentsInSelfAndChildren<VRCFury>()
                .SelectMany(v => v.GetAllFeatures())
                .OfType<ArmatureLink>()
                .ToList();
        }

        public static HumanBodyBones? GetClosestHumanoidBone(VFGameObject obj) {
            if (resultCache.TryGetValue(obj, out var cached)) {
                return cached.bone;
            }
            var bone = GetClosestHumanoidBoneUncached(obj);
            resultCache[obj] = new Result() { bone = bone };
            return bone;
        }

        private static HumanBodyBones? GetClosestHumanoidBoneUncached(VFGameObject obj) {
            var avatarObject = VRCAvatarUtils.GuessAvatarObject(obj);

            if (avatarObject == null) return null;
            var followConstraints = true;
            var followArmatureLink = true;

            var armatureLinks = GetArmatuareLinks(avatarObject);

            var humanoidBones = VRCFArmatureUtils.GetAllBones(avatarObject)
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
                        var p = ArmatureLinkService.GetProbableParent(armatureLink, avatarObject, current);
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
                    Transform foundConstraint = null;
                    foreach (var constraint in current.GetComponents<IConstraint>()) {
                        if (!(constraint is ParentConstraint) && !(constraint is PositionConstraint)) continue;
                        if (constraint.sourceCount == 0) continue;
                        var source = constraint.GetSource(0).sourceTransform;
                        if (source != null && !alreadyChecked.Contains(source)) {
                            foundConstraint = source;
                            break;
                        }
                    }

                    if (foundConstraint) {
                        current = foundConstraint;
                        continue;
                    }
                }
                current = current.parent;
            }
            return null;
        }
    }
}