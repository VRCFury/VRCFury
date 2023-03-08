using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Builder {
    public class VRCFArmatureUtils {
        private static FieldInfo parentNameField = 
            typeof(SkeletonBone).GetField("parentName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        /**
         * This basically does what Animator.GetBoneTransform SHOULD do, except GetBoneTransform randomly sometimes
         * returns bones on clothing armatures instead of the avatar, and also sometimes returns null for no reason.
         */
        public static GameObject FindBoneOnArmature(GameObject avatarObject, HumanBodyBones findBone) {
            var animator = avatarObject.GetComponent<Animator>();
            if (!animator || !animator.avatar) {
                return null;
            }

            var humanDescription = animator.avatar.humanDescription;
            var skeleton = humanDescription.skeleton;

            string[][] GetPathsToRoot(SkeletonBone b, HashSet<SkeletonBone> seen = null) {
                if (seen == null) seen = new HashSet<SkeletonBone>();
                var boneParentName = (string)parentNameField.GetValue(b);
                if (boneParentName == null || string.IsNullOrWhiteSpace(boneParentName))
                    return new[] { new string[] { } };

                seen.Add(b);
                var pathsToRoot = skeleton
                    .Where(other => other.name == boneParentName)
                    .Where(other => !seen.Contains(other))
                    .SelectMany(other => GetPathsToRoot(other, seen))
                    .Select(path => path.Append(b.name).ToArray())
                    .ToArray();
                seen.Remove(b);
                return pathsToRoot;
            }

            var humanBoneName = HumanTrait.BoneName[(int)findBone];
            var avatarBoneName = humanDescription.human
                .FirstOrDefault(humanBone => humanBone.humanName == humanBoneName)
                .boneName;
            var paths = skeleton
                .Where(other => other.name == avatarBoneName)
                .SelectMany(other => GetPathsToRoot(other))
                .Select(path => string.Join("/", path))
                .ToImmutableHashSet()
                .ToArray();

            var matching = paths
                .Select(path => avatarObject.transform.Find(path))
                .Where(found => found != null)
                .ToArray();
            
            if (matching.Length == 0) {
                if (paths.Length > 0) {
                    throw new VRCFBuilderException(
                        "Failed to find " + findBone + " object on avatar, but bone was listed in humanoid descriptor. " +
                        "Did you rename one of your avatar's bones on accident? The path to this bone should be:\n\n" +
                        string.Join("\n\nor ", paths));
                }
                return null;
            }
            if (matching.Length > 1) {
                var matchingPaths = matching
                    .Select(o => AnimationUtility.CalculateTransformPath(o.transform, avatarObject.transform));
                throw new VRCFBuilderException(
                    "Found multiple possible matching " + matching + " bones on avatar.\n\n" + string.Join("\n\n", matchingPaths));
            }
            return matching[0].gameObject;
        }
    }
}
