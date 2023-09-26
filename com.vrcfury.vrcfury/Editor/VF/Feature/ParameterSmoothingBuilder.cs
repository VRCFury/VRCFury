using System.Collections.Concurrent;
using System.Linq;
using UnityEditor.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature
{
    public class ParameterSmoothingBuilder : FeatureBuilder
    {

        [VFAutowired] private readonly ParamSmoothingService smoothing;

        [FeatureBuilderAction(FeatureOrder.ParameterSmoothing)]
        public void Apply()
        {
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();

            foreach (var c in manager.GetAllUsedControllers()) {
                ReplaceUsagesOfSmoothedParameters(c);
            }
        }

        private void ReplaceUsagesOfSmoothedParameters(ControllerManager c)
        {
            var smoothedParams = c.GetSmoothedParams().ToDictionary(v => v.name, v => v);
            if (smoothedParams.Count == 0) {
                return;
            }
            var raw = c.GetRaw();
            var paramsMap = raw.parameters.ToDictionary(x => x.name, x => x);
            var smoothedParamCache = new ConcurrentDictionary<string, string>();
            ((AnimatorController)raw).RewriteParameters(param => {
                if (smoothedParams.TryGetValue(param, out var smoothedParam)) {
                    return smoothedParamCache.GetOrAdd(param, _ => 
                        smoothing.Smooth(c, 
                            param, 
                            new VFAFloat(paramsMap[param]), 
                            smoothedParam.smoothingDuration, 
                            smoothedParam.useAcceleration)
                        .Name());
                }
                return param;
            }, includeWrites: false);
        }
    }
}