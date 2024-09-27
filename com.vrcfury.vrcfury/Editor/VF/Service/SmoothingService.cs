using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Time-smooths a parameter within an animator
     */
    [VFService]
    [VFPrototypeScope]
    internal class SmoothingService {
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        [VFAutowired] private readonly FrameTimeService frameTimeService;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        
        public VFAFloat Smooth(VFBlendTreeDirect directTree, string name, VFAFloat target, float smoothingSeconds, bool useAcceleration = true, float minSupported = 0, float maxSupported = float.MaxValue) {
            if (smoothingSeconds <= 0) {
                return target;
            }
            if (smoothingSeconds > 10) smoothingSeconds = 10;
            var speed = GetSpeed(smoothingSeconds, useAcceleration);

            if (!useAcceleration) {
                return Smooth_(directTree, target, name, speed, minSupported, maxSupported);
            }

            var pass1 = Smooth_(directTree, target, $"{name}/Pass1", speed, minSupported, maxSupported);
            return Smooth_(directTree, pass1, $"{name}/Pass2", speed, minSupported, maxSupported);
        }

        private readonly Dictionary<string, VFAFloat> cachedSpeeds = new Dictionary<string, VFAFloat>();
        private VFAFloat GetSpeed(float seconds, bool useAcceleration) {
            var name = $"smoothingSpeed/{seconds}{(useAcceleration ? "/withAccel" : "")}";
            if (cachedSpeeds.TryGetValue(name, out var output)) {
                return output;
            }

            var framerateForCalculation = 60; // closer to the in game framerate, the more technically accurate it will be
            var targetFrames = seconds * framerateForCalculation;
            var currentSpeed = 0.5f;
            var nextStep = 0.25f;
            for (var i = 0; i < 20; i++) {
                var currentFrames = VRCFuryHapticSocket.GetFramesRequired(currentSpeed, useAcceleration);
                if (currentFrames > targetFrames) {
                    currentSpeed += nextStep;
                } else {
                    currentSpeed -= nextStep;
                }
                nextStep *= 0.5f;
            }
            var fractionPerSecond = currentSpeed * framerateForCalculation;

            var math = dbtLayerService.GetMath(dbtLayerService.Create($"Calculate {name}"));
            output = math.Multiply(name, frameTimeService.GetFrameTime(), fractionPerSecond);
            cachedSpeeds[name] = output;
            return output;
        }

        private BlendtreeMath.VFAap Smooth_(VFBlendTreeDirect directTree, VFAFloat target, string name, VFAFloat speedParam, float minSupported, float maxSupported) {
            var output = fx.MakeAap(name, def: target.GetDefault());
            
            // Maintain tree - keeps the current value
            var maintainTree = BlendtreeMath.MakeCopier(output, output, minSupported, maxSupported);

            // Target tree - uses the target (input) value
            var targetTree = BlendtreeMath.MakeCopier(target, output, minSupported, maxSupported);

            //The following two trees merge the update and the maintain tree together. The smoothParam controls 
            //how much from either tree should be applied during each tick
            var smoothTree = VFBlendTree1D.CreateWithData(
                $"{output.Name()} smoothto {target.Name()}",
                speedParam,
                (0, maintainTree),
                (1, targetTree)
            );
            directTree.Add(smoothTree);

            return output;
        }

    }
}
