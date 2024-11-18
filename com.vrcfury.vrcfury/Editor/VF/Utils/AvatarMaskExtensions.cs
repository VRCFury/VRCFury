using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace VF.Utils {
    internal static class AvatarMaskExtensions {
        private const string MagicEverythingString = "__vrcf_everything";

        public static AvatarMask Empty() {
            var mask = VrcfObjectFactory.Create<AvatarMask>();
            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                mask.SetHumanoidBodyPartActive(bodyPart, false);
            }
            mask.SetTransforms(new string[] {});
            return mask;
        }

        private static void Combine(this AvatarMask mask, [CanBeNull] AvatarMask other, bool add) {
            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                if (add) {
                    if (other == null || other.GetHumanoidBodyPartActive(bodyPart))
                        mask.SetHumanoidBodyPartActive(bodyPart, true);
                } else {
                    if (other != null && !other.GetHumanoidBodyPartActive(bodyPart))
                        mask.SetHumanoidBodyPartActive(bodyPart, false);
                }
            }

            var ourTransforms = new HashSet<string>(mask.GetTransforms());
            var otherTransforms = other == null
                ? (ICollection<string>)new []{ MagicEverythingString }
                : other.GetTransforms();
            if (add) {
                ourTransforms.UnionWith(otherTransforms);
            } else {
                if (ourTransforms.Contains(MagicEverythingString)) {
                    ourTransforms.Clear();
                    ourTransforms.UnionWith(otherTransforms);
                } else if (otherTransforms.Contains(MagicEverythingString)) {
                    // Keep our existing transforms
                } else {
                    ourTransforms.IntersectWith(otherTransforms);
                }
            }
            mask.SetTransforms(ourTransforms);
        }
        
        public static void IntersectWith(this AvatarMask mask, [CanBeNull] AvatarMask other) {
            mask.Combine(other, false);
        }
        
        public static void UnionWith(this AvatarMask mask, [CanBeNull] AvatarMask other) {
            mask.Combine(other, true);
        }

        public static ISet<string> GetTransforms(this AvatarMask mask) {
            if (mask.transformCount == 0) {
                return new HashSet<string> { MagicEverythingString };
            }
            return Enumerable.Range(0, mask.transformCount)
                .Where(mask.GetTransformActive)
                .Select(mask.GetTransformPath)
                .ToImmutableHashSet();
        }

        public static void SetTransforms(this AvatarMask mask, IEnumerable<string> transforms) {
            var active = transforms.ToImmutableHashSet();
            if (active.Contains(MagicEverythingString)) {
                mask.AllowAllTransforms();
                return;
            }
            
            var withParents = WithParents(active)
                .ToImmutableHashSet()
                .OrderBy(path => path)
                .ToArray();
            mask.transformCount = withParents.Length;
            foreach (var (i, path) in withParents.Select((path, i) => (i, path))) {
                mask.SetTransformActive(i, active.Contains(path));
                mask.SetTransformPath(i, path);
            }
            mask.EnsureOneTransform();
        }
        
        public static void AllowAllMuscles(this AvatarMask mask) {
            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                mask.SetHumanoidBodyPartActive(bodyPart, true);
            }
        }

        public static void AllowAllTransforms(this AvatarMask mask) {
            mask.transformCount = 0;
        }
        
        public static bool AllowsAnyMuscles(this AvatarMask mask) {
            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                if (mask.GetHumanoidBodyPartActive(bodyPart)) return true;
            }
            return false;
        }

        public static bool AllowsAllTransforms(this AvatarMask mask) {
            return mask.transformCount == 0;
        }

        private static ICollection<string> WithParents(ICollection<string> paths) {
            var all = new HashSet<string>();
            foreach (var path in paths) {
                var split = path.Split('/');
                for (var i = 1; i <= split.Length; i++) {
                    all.Add(split.Take(i).Join('/'));
                }
            }
            return all;
        }

        public static AvatarMask DefaultFxMask() {
            var mask = Empty();
            mask.AllowAllTransforms();
            return mask;
        }

        /**
         * If the transform list is empty, unity assumes you mean "all transforms", which is totally
         * not what we want.
         */
        private static void EnsureOneTransform(this AvatarMask mask) {
            if (mask.transformCount > 0) return;
            // The unity mask editor doesn't show the first level for some reason... so we just add two
            // so they actually show up when you view it.
            mask.transformCount = 2;
            mask.SetTransformActive(0, false);
            mask.SetTransformPath(0, "_maskFakeTransform");
            mask.SetTransformActive(1, false);
            mask.SetTransformPath(1, "_maskFakeTransform/_maskFakeTransform");
        }
    }
}
