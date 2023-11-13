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
            public HashSet<Transform> scaleIsAnimated = new HashSet<Transform>();
            public HashSet<Transform> positionIsAnimated = new HashSet<Transform>();
            public HashSet<Transform> rotationIsAnimated = new HashSet<Transform>();
            public HashSet<Transform> positionIsAnimatedByPhysbone = new HashSet<Transform>();
            public HashSet<Transform> rotationIsAnimatedByPhysbone = new HashSet<Transform>();
            private Dictionary<Transform, List<string>> debugSources = new Dictionary<Transform, List<string>>();
            
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
            foreach (var physbone in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneBase>()) {
                var affected = PhysboneUtils.GetAffectedTransforms(physbone);
                output.positionIsAnimated.UnionWith(affected.mayMove);
                output.rotationIsAnimated.UnionWith(affected.mayRotate);
                output.positionIsAnimatedByPhysbone.UnionWith(affected.mayMove);
                output.rotationIsAnimatedByPhysbone.UnionWith(affected.mayRotate);
                foreach (var r in affected.mayRotate) {
                    output.AddDebugSource(r, "Physbone on " + physbone.owner().GetPath(avatarObject));
                }
            }

            foreach (var clip in manager.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                var transformBindings = clip.GetAllBindings()
                    .Where(binding => binding.type == typeof(Transform))
                    .ToImmutableHashSet();
                foreach (var binding in transformBindings) {
                    var transform = avatarObject.Find(binding.path).transform;
                    if (transform == null) continue;
                    var lower = binding.propertyName.ToLower();
                    if (lower.Contains("scale"))
                        output.scaleIsAnimated.Add(transform);
                    else if (lower.Contains("euler"))
                        output.rotationIsAnimated.Add(transform);
                    else if (lower.Contains("position"))
                        output.positionIsAnimated.Add(transform);
                    output.AddDebugSource(transform, "Animation clip: " + clip.name);
                }
            }

            return output;
        }
    }
}