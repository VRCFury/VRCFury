using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VRC.Dynamics;

namespace VF.Service {
    [VFService]
    public class FindAnimatedTransformsService {
        [VFAutowired] private readonly AvatarManager manager;
        
        public class AnimatedTransforms {
            public readonly HashSet<Transform> scaleIsAnimated = new HashSet<Transform>();
            public readonly HashSet<Transform> positionIsAnimated = new HashSet<Transform>();
            public readonly HashSet<Transform> rotationIsAnimated = new HashSet<Transform>();
            public readonly HashSet<Transform> physboneRoot = new HashSet<Transform>();
            public readonly HashSet<Transform> physboneChild = new HashSet<Transform>();
            public readonly HashSet<Transform> activated = new HashSet<Transform>();
            private readonly Dictionary<Transform, List<string>> debugSources = new Dictionary<Transform, List<string>>();

            public void AddDebugSource(Transform t, string source) {
                if (debugSources.TryGetValue(t, out var list)) list.Add(source);
                else debugSources[t] = new List<string> { source };
            }

            public IList<string> GetDebugSources(Transform t) {
                return debugSources.TryGetValue(t, out var output) ? output : new List<string>();
            }
        }

        public AnimatedTransforms Find() {
            var output = new AnimatedTransforms();
            var avatarObject = manager.AvatarObject;
            
            // Physbones
            foreach (var physBone in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneBase>()) {
                var root = physBone.GetRootTransform().asVf();
                var path = physBone.owner().GetPath(avatarObject);
                bool IsIgnored(Transform transform) =>
                    physBone.ignoreTransforms.Any(ignored => ignored != null && transform.IsChildOf(ignored));
                var nonIgnoredChildren = root.Children()
                    .Where(child => !IsIgnored(child))
                    .ToArray();

                if (nonIgnoredChildren.Length > 1 && physBone.multiChildType == VRCPhysBoneBase.MultiChildType.Ignore) {
                    // Root is ignored
                } else {
                    output.physboneRoot.Add(root);
                    output.AddDebugSource(root, $"Physbone root in {path}");
                }

                output.physboneChild.UnionWith(nonIgnoredChildren.Select(o => o.transform));
                foreach (var r in nonIgnoredChildren) {
                    output.AddDebugSource(r, $"Physbone child in {path}");
                }
            }

            // Animation clips
            foreach (var clip in manager.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                foreach (var binding in clip.GetAllBindings()) {
                    if (binding.type == typeof(Transform)) {
                        var transform = avatarObject.Find(binding.path).transform;
                        if (transform == null) continue;
                        var lower = binding.propertyName.ToLower();
                        if (lower.Contains("scale"))
                            output.scaleIsAnimated.Add(transform);
                        else if (lower.Contains("euler"))
                            output.rotationIsAnimated.Add(transform);
                        else if (lower.Contains("position"))
                            output.positionIsAnimated.Add(transform);
                        output.AddDebugSource(transform, "Transform animated in " + clip.name);
                    } else if (binding.type == typeof(GameObject)) {
                        var transform = avatarObject.Find(binding.path).transform;
                        if (transform == null) continue;
                        output.activated.Add(transform);
                    }
                }
            }

            return output;
        }
    }
}