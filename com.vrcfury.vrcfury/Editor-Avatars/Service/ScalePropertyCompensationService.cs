using System.Collections.Generic;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Handles creating the DirectTree for properties that need correction when scaling the avatar
     */
    [VFService]
    internal class ScalePropertyCompensationService {
        [VFAutowired] private readonly WorldScaleDetectorService worldScaleService;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        public void AddScaledProp(VFGameObject scaleReference, IList<(UnityEngine.Component component, string PropertyName, float LocalValue)> properties) {
            var worldScale = worldScaleService.GetWorldScale(scaleReference, "tps scale fix");
            if (worldScale == null) return;
            AddScaledProp(worldScale, properties);
        }

        public void AddScaledProp(VFAFloat worldScale, IList<(UnityEngine.Component component, string PropertyName, float LocalValue)> properties) {
            if (worldScale == null) return;

            var directTree = dbtLayerService.Create();
            
            var zeroClip = clipFactory.NewClip($"scaleComp_zero");
            directTree.Add(zeroClip);

            var scaleClip = clipFactory.NewClip($"scaleComp_one");
            directTree.Add(worldScale, scaleClip);

            foreach (var prop in properties) {
                scaleClip.SetCurve(
                    prop.component,
                    prop.PropertyName,
                    prop.LocalValue
                );
                zeroClip.SetCurve(
                    prop.component,
                    prop.PropertyName,
                    0
                );
            }
        }
    }
}
