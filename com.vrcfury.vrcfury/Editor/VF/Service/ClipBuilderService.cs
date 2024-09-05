using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Service {

    [VFService]
    internal class ClipBuilderService {
        [VFAutowired] private readonly AvatarBindingStateService bindingStateService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        public AnimationClip MergeSingleFrameClips(params (float, AnimationClip)[] sources) {
            var output = clipFactory.NewClip("Merged");
            foreach (var binding in sources.SelectMany(tuple => tuple.Item2.GetFloatBindings()).Distinct()) {
                var exists = bindingStateService.GetFloat(binding, out var defaultValue);
                if (!exists && binding.path == "" && binding.type == typeof(Animator)) {
                    exists = true;
                    defaultValue = 0;
                }
                if (!exists) continue;
                var outputCurve = new AnimationCurve();
                foreach (var (time,sourceClip) in sources) {
                    var sourceCurve = sourceClip.GetFloatCurve(binding);
                    if (sourceCurve != null && sourceCurve.keys.Length >= 1) {
                        outputCurve.AddKey(new Keyframe(time, sourceCurve.keys.Last().value, 0f, 0f));
                    } else {
                        outputCurve.AddKey(new Keyframe(time, defaultValue, 0f, 0f));
                    }
                }
                output.SetCurve(binding, outputCurve);
            }
            foreach (var binding in sources.SelectMany(tuple => tuple.Item2.GetObjectBindings()).Distinct()) {
                var exists = bindingStateService.GetObject(binding, out var defaultValue);
                if (!exists) continue;
                var outputCurve = new List<ObjectReferenceKeyframe>();
                foreach (var (time,sourceClip) in sources) {
                    var sourceCurve = sourceClip.GetObjectCurve(binding);
                    if (sourceCurve != null && sourceCurve.Length >= 1) {
                        outputCurve.Add(new ObjectReferenceKeyframe { time = time, value = sourceCurve.Last().value });
                    } else {
                        outputCurve.Add(new ObjectReferenceKeyframe { time = time, value = defaultValue });
                    }
                }
                output.SetCurve(binding, outputCurve.ToArray());
            }
            return output;
        }
    }

}
