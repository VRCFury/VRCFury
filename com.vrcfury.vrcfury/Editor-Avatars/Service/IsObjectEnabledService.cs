using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class IsObjectEnabledService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        [VFAutowired] private readonly AllClipsService allClipsService;
        private ControllerManager fx => controllers.GetFx();

        private readonly Dictionary<VFGameObject, BlendtreeMath.VFAap> hierarchyEnabled =
            new Dictionary<VFGameObject, BlendtreeMath.VFAap>();

        public VFAFloat Get(VFGameObject obj) {
            return hierarchyEnabled.GetOrCreate(
                obj,
                () => fx.MakeAap($"Object Enabled/{GetPath(obj)}/Hierarchy")
            ).AsFloat();
        }

        private IEnumerable<VFGameObject> GetSelfAndParentsUnderAvatar(VFGameObject obj) {
            var output = new List<VFGameObject>();
            var current = obj;
            while (current != null && current != avatarObject) {
                output.Add(current);
                current = current.parent;
            }
            output.Reverse();
            return output;
        }

        private string GetPath(VFGameObject obj) {
            return obj.GetPath(avatarObject, prettyRoot: true);
        }

        [FeatureBuilderAction(FeatureOrder.IsObjectEnabled)]
        public void Apply() {
            if (hierarchyEnabled.Count == 0) return;

            var animatedActiveObjects = new HashSet<VFGameObject>();
            var animatedOnObjects = new HashSet<VFGameObject>();
            foreach (var clip in allClipsService.GetAllClips()) {
                foreach (var (binding, curve) in clip.GetFloatCurves()) {
                    if (binding.type != typeof(GameObject)) continue;
                    if (binding.propertyName != "m_IsActive") continue;
                    var animatedObject = avatarObject.Find(binding.path);
                    if (animatedObject == null) continue;
                    animatedActiveObjects.Add(animatedObject);
                    if (curve.keys.Any(key => key.value > 0)) {
                        animatedOnObjects.Add(animatedObject);
                    }
                }
            }

            var directTree = dbtLayerService.Create("IsObjectEnabledService");
            var math = dbtLayerService.GetMath(directTree);
            var rewrites = new Dictionary<string, string>();
            var selfEnabled = new Dictionary<VFGameObject, VFAFloat>();
            VFAFloat GetSelf(VFGameObject obj) {
                return selfEnabled.GetOrCreate(
                    obj,
                    () => fx.MakeAap($"IsObjectEnabledService/{GetPath(obj)}/Self", obj.active ? 1 : 0)
                );
            }

            foreach (var pair in hierarchyEnabled.ToArray()) {
                var obj = pair.Key;
                var output = pair.Value;

                var toggledObjects = new List<VFGameObject>();
                var alwaysOff = false;
                foreach (var current in GetSelfAndParentsUnderAvatar(obj)) {
                    var animated = animatedActiveObjects.Contains(current);
                    if (!current.active && !animatedOnObjects.Contains(current)) {
                        alwaysOff = true;
                        break;
                    }

                    if (current.active && !animated) continue;
                    toggledObjects.Add(current);
                }

                if (alwaysOff) {
                    continue;
                }
                if (toggledObjects.Count == 0) {
                    fx.SetDefault(output.AsFloat(), 1);
                    continue;
                }
                if (toggledObjects.Count == 1) {
                    rewrites[output.Name()] = GetSelf(toggledObjects[0]).Name();
                    continue;
                }

                var condition = toggledObjects.Select(GetSelf)
                    .Select(self => BlendtreeMath.GreaterThan(self, 0))
                    .Aggregate(BlendtreeMath.True(), (a, b) => a.And(b));
                math.SetValueWithConditions(
                    (output.MakeSetter(1), condition),
                    (output.MakeSetter(0), null)
                );
            }

            var byPath = selfEnabled.ToDictionary(pair => pair.Key.GetAnimatedPath(), pair => pair.Value);
            foreach (var clip in allClipsService.GetAllClips()) {
                foreach (var (binding, curve) in clip.GetFloatCurves()) {
                    if (binding.type != typeof(GameObject)) continue;
                    if (binding.propertyName != "m_IsActive") continue;
                    if (!byPath.TryGetValue(binding.path, out var enabled)) continue;

                    clip.SetAap(enabled.Name(), curve.Clone());
                }
            }

            if (rewrites.Count > 0) {
                fx.RewriteParameters(name => rewrites.TryGetValue(name, out var rewrite) ? rewrite : name, includeWrites: false);
                foreach (var oldName in rewrites.Keys) {
                    fx.RemoveParameter(oldName);
                }
            }
        }
    }
}
