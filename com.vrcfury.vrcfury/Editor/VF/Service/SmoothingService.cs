using VF.Builder;
using VF.Component;
using VF.Injector;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Time-smooths a parameter within an animator
     */
    [VFService]
    public class SmoothingService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly FrameTimeService frameTimeService;
        
        public VFAFloat Smooth(string name, VFAFloat target, float smoothingSeconds, bool useAcceleration = true) {
            if (smoothingSeconds <= 0) return target;
            if (smoothingSeconds > 10) smoothingSeconds = 10;
            var fractionPerFrame = GetSpeed(smoothingSeconds, useAcceleration);

            var fx = manager.GetFx();
            var speedParam = fx.NewFloat($"{name}/FractionPerFrame", def: fractionPerFrame);
            var speedParamCompensated =
                math.Multiply($"{name}/FractionPerFrameComp", speedParam, frameTimeService.GetFrameTime());

            var output = Smooth_($"{name}/Pass1", target, speedParamCompensated);
            if (useAcceleration) output = Smooth_($"{name}/Pass2", output, speedParamCompensated);
            return output;
        }

        private float GetSpeed(float seconds, bool useAcceleration) {
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
            return currentSpeed * framerateForCalculation;
        }

        private VFAFloat Smooth_(string name, VFAFloat target, VFAFloat speedParam) {
            var fx = manager.GetFx();

            var output = fx.NewFloat(name, def: target.GetDefault());

            // Maintain tree - keeps the current value
            var maintainTree = math.MakeMaintainer(output);

            // Target tree - uses the target (input) value
            var targetTree = math.MakeCopier(target, output);

            //The following two trees merge the update and the maintain tree together. The smoothParam controls 
            //how much from either tree should be applied during each tick
            var smoothTree = math.Make1D(
                $"{output.Name()} smoothto {target.Name()}",
                speedParam,
                (maintainTree, 0),
                (targetTree, 1)
            );

            directTree.Add(smoothTree);

            return output;
        }

    }
}