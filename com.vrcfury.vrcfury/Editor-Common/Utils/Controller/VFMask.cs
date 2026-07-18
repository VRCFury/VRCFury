using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFMask {
        private readonly AvatarMask sourceRaw;
        private readonly bool[] humanoidBodyParts;
        private bool allowAllTransforms;
        private HashSet<VFResolvedObject> transforms;
        private bool changedFromSource;
        private string maskName;

        private VFMask(
            AvatarMask sourceRaw,
            bool[] humanoidBodyParts,
            bool allowAllTransforms,
            IEnumerable<VFResolvedObject> transforms
        ) {
            this.sourceRaw = sourceRaw;
            this.humanoidBodyParts = humanoidBodyParts;
            this.allowAllTransforms = allowAllTransforms;
            this.transforms = new HashSet<VFResolvedObject>(transforms ?? Enumerable.Empty<VFResolvedObject>());
            maskName = sourceRaw != null ? sourceRaw.name : null;
        }

        internal static VFMask Load(AvatarMask raw, VFLoadContext context) {
            if (raw == null) return null;

            var humanoidBodyParts = new bool[(int)AvatarMaskBodyPart.LastBodyPart];
            for (var i = 0; i < humanoidBodyParts.Length; i++) {
                humanoidBodyParts[i] = raw.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i);
            }

            var allowAllTransforms = RawAllowsAllTransforms(raw);
            var sourcePaths = allowAllTransforms
                ? Array.Empty<string>()
                : GetRawTransforms(raw).ToArray();
            var loadedTransforms = sourcePaths
                .Select(path => VFResolvedObject.Load(path, context, typeof(Transform)))
                .ToArray();
            var transforms = allowAllTransforms
                ? Enumerable.Empty<VFResolvedObject>()
                : loadedTransforms
                    .Where(t => t.HasValue)
                    .Select(t => t.Value);

            var output = new VFMask(
                raw,
                humanoidBodyParts,
                allowAllTransforms,
                transforms
            );
            if (!allowAllTransforms && (
                loadedTransforms.Any(t => !t.HasValue)
                || output.transforms.Any(t => t.UnresolvedPath != t.SourcePath)
            )) {
                output.changedFromSource = true;
            }

            return output;
        }

        public static VFMask Empty() {
            return new VFMask(
                null,
                new bool[(int)AvatarMaskBodyPart.LastBodyPart],
                false,
                Enumerable.Empty<VFResolvedObject>()
            ) {
                changedFromSource = true
            };
        }

        public static VFMask DefaultFxMask() {
            var mask = Empty();
            mask.AllowAllTransforms();
            return mask;
        }

        public string name {
            get => maskName;
            set {
                if (maskName == value) return;
                maskName = value;
                changedFromSource = true;
            }
        }

        public VFMask Clone() {
            var output = new VFMask(
                sourceRaw,
                humanoidBodyParts.ToArray(),
                allowAllTransforms,
                transforms
            );
            output.changedFromSource = changedFromSource;
            output.maskName = maskName;
            return output;
        }

        public AvatarMask Save(VFGameObject bindingRoot, bool reuseSourceAsset = true) {
            if (bindingRoot == null) throw new ArgumentNullException(nameof(bindingRoot));
            if (reuseSourceAsset && !changedFromSource && CanUseSourceRaw(bindingRoot)) {
                return sourceRaw;
            }

            var raw = VrcfObjectFactory.Create<AvatarMask>();
            if (!string.IsNullOrEmpty(maskName)) {
                raw.name = maskName;
            }
            for (var i = 0; i < humanoidBodyParts.Length; i++) {
                raw.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, humanoidBodyParts[i]);
            }

            if (allowAllTransforms) {
                raw.transformCount = 0;
            } else {
                var active = transforms
                    .Select(t => t.GetPath(bindingRoot, "Resolved mask transform requires a binding root"))
                    .ToHashSet();
                var withParents = WithParents(active)
                    .OrderBy(path => path)
                    .ToArray();
                raw.transformCount = withParents.Length;
                foreach (var (i, path) in withParents.Select((path, i) => (i, path))) {
                    raw.SetTransformActive(i, active.Contains(path));
                    raw.SetTransformPath(i, path);
                }
                EnsureOneTransform(raw);
            }
            return raw;
        }

        private bool CanUseSourceRaw(VFGameObject bindingRoot) {
            if (sourceRaw == null) return false;
            foreach (var transform in transforms) {
                if (bindingRoot == null) return false;
                if (transform.GetPath(bindingRoot, "Resolved mask transform requires a binding root") != transform.SourcePath) {
                    return false;
                }
            }
            return true;
        }

        public void SetTransforms(IEnumerable<string> newTransforms) {
            allowAllTransforms = false;
            transforms = (newTransforms ?? Enumerable.Empty<string>())
                .Select(path => new VFResolvedObject(null, path, path))
                .ToHashSet();
            changedFromSource = true;
        }

        public bool GetHumanoidBodyPartActive(AvatarMaskBodyPart bodyPart) {
            return humanoidBodyParts[(int)bodyPart];
        }

        public void SetHumanoidBodyPartActive(AvatarMaskBodyPart bodyPart, bool active) {
            if (humanoidBodyParts[(int)bodyPart] == active) return;
            humanoidBodyParts[(int)bodyPart] = active;
            changedFromSource = true;
        }

        public void AllowAllTransforms() {
            if (allowAllTransforms) return;
            allowAllTransforms = true;
            transforms = new HashSet<VFResolvedObject>();
            changedFromSource = true;
        }

        public bool allowsAllTransforms => allowAllTransforms;

        public bool AllowsAnyMuscles() {
            return humanoidBodyParts.Any(active => active);
        }

        public void IntersectWith(VFMask other) {
            Combine(other, add: false);
        }

        public void UnionWith(VFMask other) {
            Combine(other, add: true);
        }

        private void Combine(VFMask other, bool add) {
            var oldAllowAllTransforms = allowAllTransforms;
            var oldTransforms = transforms;
            var oldHumanoidBodyParts = humanoidBodyParts.ToArray();
            for (var bodyPart = 0; bodyPart < humanoidBodyParts.Length; bodyPart++) {
                if (add) {
                    if (other == null || other.humanoidBodyParts[bodyPart]) {
                        humanoidBodyParts[bodyPart] = true;
                    }
                } else {
                    if (other != null && !other.humanoidBodyParts[bodyPart]) {
                        humanoidBodyParts[bodyPart] = false;
                    }
                }
            }

            if (add) {
                if (other == null || other.allowAllTransforms) {
                    allowAllTransforms = true;
                    transforms = new HashSet<VFResolvedObject>();
                } else if (!allowAllTransforms) {
                    transforms = transforms.Union(other.transforms).ToHashSet();
                }
            } else {
                if (!allowAllTransforms) {
                    if (other == null || other.allowAllTransforms) {
                        // No change.
                    } else {
                        transforms = transforms.Intersect(other.transforms).ToHashSet();
                    }
                } else if (other != null && !other.allowAllTransforms) {
                    allowAllTransforms = false;
                    transforms = new HashSet<VFResolvedObject>(other.transforms);
                }
            }

            if (oldAllowAllTransforms != allowAllTransforms
                || !oldTransforms.SetEquals(transforms)
                || !oldHumanoidBodyParts.SequenceEqual(humanoidBodyParts)) {
                changedFromSource = true;
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

        private static void EnsureOneTransform(AvatarMask mask) {
            if (mask.transformCount > 0) return;
            mask.transformCount = 2;
            mask.SetTransformActive(0, false);
            mask.SetTransformPath(0, "_maskFakeTransform");
            mask.SetTransformActive(1, false);
            mask.SetTransformPath(1, "_maskFakeTransform/_maskFakeTransform");
        }

        private static ISet<string> GetRawTransforms(AvatarMask mask) {
            if (mask.transformCount == 0) {
                return new HashSet<string> { "__vrcf_everything" };
            }
            return Enumerable.Range(0, mask.transformCount)
                .Where(mask.GetTransformActive)
                .Select(mask.GetTransformPath)
                .ToHashSet();
        }

        private static bool RawAllowsAllTransforms(AvatarMask mask) {
            return mask.transformCount == 0;
        }
    }
}
