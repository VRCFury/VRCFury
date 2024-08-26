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
    internal class FindAnimatedTransformsService {
        [VFAutowired] private readonly AvatarManager manager;
        
        public class AnimatedTransforms {
            public readonly HashSet<VFGameObject> scaleIsAnimated = new HashSet<VFGameObject>();
            public readonly HashSet<VFGameObject> positionIsAnimated = new HashSet<VFGameObject>();
            public readonly HashSet<VFGameObject> rotationIsAnimated = new HashSet<VFGameObject>();
            public readonly HashSet<VFGameObject> physboneRoot = new HashSet<VFGameObject>();
            public readonly HashSet<VFGameObject> physboneChild = new HashSet<VFGameObject>();
            public readonly HashSet<VFGameObject> activated = new HashSet<VFGameObject>();
            private readonly Dictionary<VFGameObject, List<string>> debugSources = new Dictionary<VFGameObject, List<string>>();

            public void AddDebugSource(VFGameObject t, string source) {
                if (debugSources.TryGetValue(t, out var list)) list.Add(source);
                else debugSources[t] = new List<string> { source };
            }

            public IList<string> GetDebugSources(VFGameObject t) {
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
                bool IsIgnored(VFGameObject transform) =>
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

                output.physboneChild.UnionWith(nonIgnoredChildren);
                foreach (var r in nonIgnoredChildren) {
                    output.AddDebugSource(r, $"Physbone child in {path}");
                }
            }

            // Animation clips
            foreach (var clip in manager.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                foreach (var binding in clip.GetAllBindings()) {
                    if (binding.type == typeof(Transform)) {
                        var transform = avatarObject.Find(binding.path);
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
                        var transform = avatarObject.Find(binding.path);
                        if (transform == null) continue;
                        output.activated.Add(transform);
                    }
                }
            }

            return output;
        }
    }
}