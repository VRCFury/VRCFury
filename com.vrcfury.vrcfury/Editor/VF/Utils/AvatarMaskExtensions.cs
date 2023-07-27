using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace VF.Utils {
    public static class AvatarMaskExtensions {
        public static AvatarMask Empty() {
            var mask = new AvatarMask();
            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                mask.SetHumanoidBodyPartActive(bodyPart, false);
            }
            mask.EnsureOneTransform();
            return mask;
        }

        private static void Combine(this AvatarMask mask, AvatarMask other, bool add) {
            if (other == null) return;
            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                if (add) {
                    if (other.GetHumanoidBodyPartActive(bodyPart))
                        mask.SetHumanoidBodyPartActive(bodyPart, true);
                } else {
                    if (!other.GetHumanoidBodyPartActive(bodyPart))
                        mask.SetHumanoidBodyPartActive(bodyPart, false);
                }
            }

            var transforms = new HashSet<string>(mask.GetTransforms());
            if (add) {
                transforms.UnionWith(other.GetTransforms());
            } else {
                transforms.IntersectWith(other.GetTransforms());
            }
            mask.SetTransforms(transforms);
        }
        
        public static void IntersectWith(this AvatarMask mask, AvatarMask other) {
            mask.Combine(other, false);
        }
        
        public static void UnionWith(this AvatarMask mask, AvatarMask other) {
            mask.Combine(other, true);
        }

        public static ISet<string> GetTransforms(this AvatarMask mask) {
            return Enumerable.Range(0, mask.transformCount)
                .Where(mask.GetTransformActive)
                .Select(mask.GetTransformPath)
                .ToImmutableHashSet();
        }

        public static void SetTransforms(this AvatarMask mask, IEnumerable<string> transforms) {
            var active = transforms.ToImmutableHashSet();
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

        private static ICollection<string> WithParents(ICollection<string> paths) {
            var all = new HashSet<string>();
            foreach (var path in paths) {
                var split = path.Split('/');
                for (var i = 1; i <= split.Length; i++) {
                    all.Add(string.Join("/", split.Take(i)));
                }
            }
            return all;
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
