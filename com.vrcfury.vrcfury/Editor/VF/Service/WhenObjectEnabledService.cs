using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing.Drawing2D;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class WhenObjectEnabledService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        private ControllerManager fx => controllers.GetFx();
        private readonly Dictionary<VFGameObject, VFAFloat> objectStateTotalFloats = new Dictionary<VFGameObject, VFAFloat>();

        public VFAFloat WhenEnabled(VFGameObject obj) {
            return objectStateTotalFloats.GetOrCreate(obj, () => fx.NewFloat("obje_" + obj.name));
        }

        private ISet<VFGameObject> GetEnabledContributors(VFGameObject obj) {
            return obj.GetSelfAndAllParents()
                .Where(o => o.IsChildOf(avatarObject))
                .Where(o => o != avatarObject)
                .ToImmutableHashSet();
        }

        [FeatureBuilderAction(FeatureOrder.WhenObjectEnabledService)]
        public void Apply() {
            var objectsOfConcern = objectStateTotalFloats.Keys
                .NotNull()
                .SelectMany(GetEnabledContributors)
                .ToImmutableDictionary(o => o.GetPath(avatarObject), o => o);
            var objectStateIndividualFloats = new Dictionary<VFGameObject, VFAFloat>();
            VFAFloat GetSingleObjectStateFloat(VFGameObject obj) => objectStateIndividualFloats.GetOrCreate(obj, () => fx.NewFloat("obje1_" + obj.name));
            foreach (var clip in controllers.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                foreach (var (binding,curve) in clip.GetFloatCurves()) {
                    if (binding.propertyName != "m_IsActive") continue;
                    if (binding.type != typeof(GameObject)) continue;
                    if (!objectsOfConcern.TryGetValue(binding.path, out var obj)) continue;
                    var fl = GetSingleObjectStateFloat(obj);
                    clip.SetAap(fl, curve);
                }
            }

            var dbt = dbtLayerService.Create();
            foreach (var pair in objectStateTotalFloats) {
                var obj = pair.Key;
                if (obj == null) continue;
                var totalFloat = pair.Value;

                var objectIsAlwaysDisabled = false;
                var isEnabled = BlendtreeMath.True();
                foreach (var c in GetEnabledContributors(obj)) {
                    if (objectStateIndividualFloats.TryGetValue(c, out var individualFloat)) {
                        isEnabled = isEnabled.And(BlendtreeMath.GreaterThan(individualFloat, 0));
                    } else if (!c.active) {
                        objectIsAlwaysDisabled = true;
                        break;
                    }
                }
                if (objectIsAlwaysDisabled) continue;
                dbt.Add(isEnabled.create(
                    new BlendtreeMath.VFAap(totalFloat).MakeSetter(1),
                    null
                ));
            }
        }
    }
}
