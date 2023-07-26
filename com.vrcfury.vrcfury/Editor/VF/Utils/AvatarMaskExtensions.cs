using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace VF.Utils {
    public static class AvatarMaskExtensions {
        public static void IntersectWith(this AvatarMask mask, AvatarMask other) {
            if (other == null) return;
            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                if (!other.GetHumanoidBodyPartActive(bodyPart))
                    mask.SetHumanoidBodyPartActive(bodyPart, false);
            }

            var transforms = new HashSet<string>(mask.GetTransforms());
            transforms.IntersectWith(other.GetTransforms());
            mask.SetTransforms(transforms);
        }

        public static ISet<string> GetTransforms(this AvatarMask mask) {
            return Enumerable.Range(0, mask.transformCount)
                .Where(mask.GetTransformActive)
                .Select(mask.GetTransformPath)
                .ToImmutableHashSet();
        }

        public static void SetTransforms(this AvatarMask mask, ICollection<string> transforms) {
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
    }
}
