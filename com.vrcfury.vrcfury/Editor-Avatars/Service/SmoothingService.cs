using System;
using System.Collections.Generic;
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
        private VFAvatarController fx => controllers.GetFx();
        
        public VFAParam Smooth(VFBlendTreeDirect directTree, string name, VFAParam target, float smoothingSeconds, bool useAcceleration = true, float minSupported = 0, float maxSupported = float.MaxValue) {
            if (smoothingSeconds <= 0) return target;
            return Smooth(directTree, name, smoothingSeconds, target.GetDefault(),
                output => output.MakeCopier(target, minSupported, maxSupported), useAcceleration, minSupported, maxSupported);
        }

        // Build the target directly into the first pass, avoiding an intermediate AAP.
        public VFAParam Smooth(VFBlendTreeDirect directTree, string name, float smoothingSeconds, float defaultValue,
            Func<VFAParam, VFMotion> targetMotion, bool useAcceleration = true, float minSupported = 0, float maxSupported = float.MaxValue) {
            if (smoothingSeconds <= 0) {
                var output = fx.CreateUniqueFloat(name, def: defaultValue);
                directTree.Add(targetMotion(output));
                return output;
            }
            if (smoothingSeconds > 10) smoothingSeconds = 10;
            var speed = GetSpeed(smoothingSeconds, useAcceleration);
            var pass1 = Smooth_(directTree, useAcceleration ? $"{name}/Pass1" : name, speed,
                defaultValue, targetMotion, minSupported, maxSupported);
            if (!useAcceleration) return pass1;
            return Smooth_(directTree, $"{name}/Pass2", speed, defaultValue,
                output => output.MakeCopier(pass1, minSupported, maxSupported), minSupported, maxSupported);
        }

        private readonly Dictionary<string, VFAParam> cachedSpeeds = new Dictionary<string, VFAParam>();
        private VFAParam GetSpeed(float seconds, bool useAcceleration) {
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

        private VFAParam Smooth_(VFBlendTreeDirect directTree, string name, VFAParam speedParam,
            float defaultValue, Func<VFAParam, VFMotion> targetMotion, float minSupported, float maxSupported) {
            var output = fx.CreateUniqueFloat(name, def: defaultValue);
            
            // Maintain tree - keeps the current value
            var maintainTree = output.MakeCopier(output, minSupported, maxSupported);

            // Target tree - uses the target (input) value
            var targetTree = targetMotion(output);

            //The following two trees merge the update and the maintain tree together. The smoothParam controls 
            //how much from either tree should be applied during each tick
            var smoothTree = VFBlendTree1D.CreateWithData(
                $"{output.name} smoothing",
                speedParam,
                (0, maintainTree),
                (1, targetTree)
            );
            directTree.Add(smoothTree);

            return output;
        }

    }
}
