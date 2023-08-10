using System;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;

namespace VF.Plugin {
    public class ParamSmoothingPlugin : FeaturePlugin {
        public VFAFloat Smooth(VFAFloat input, float smoothing, VFACondition pauseWhen = null, bool zeroWhenPaused = false, bool useAcceleration = true) {
            if (smoothing <= 0) return input;
            if (smoothing > 0.999) smoothing = 0.999f;

            var adjustmentExponent = 0.2f;
            smoothing = (float)Math.Pow(smoothing, adjustmentExponent);

            var output = Smooth_(input, smoothing, pauseWhen, zeroWhenPaused);
            if (useAcceleration) output = Smooth_(output, smoothing);
            return output;
        }

        private VFAFloat Smooth_(VFAFloat input, float smoothing, VFACondition pauseWhen = null, bool zeroWhenPaused = false) {
            var fx = GetFx();

            var output = fx.NewFloat(input.Name() + "_smoothed");
            
            // These clips drive the output param to certain values
            var minClip = fx.NewClip(input.Name() + "-1");
            minClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, -1f));
            var maxClip = fx.NewClip(input.Name() + "1");
            maxClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, 1f));
            
            var speedParam = fx.NewFloat(input.Name() + "_speed", def: (float)Math.Pow(smoothing, 0.1f));

            // Maintain tree - keeps the current value
            var maintainTree = fx.NewBlendTree(input.Name() + "_maintain");
            maintainTree.blendType = BlendTreeType.Simple1D;
            maintainTree.useAutomaticThresholds = false;
            maintainTree.blendParameter = output.Name();
            maintainTree.AddChild(minClip, -1);
            maintainTree.AddChild(maxClip, 1);

            Motion GenerateSmoothingTree(VFAFloat target) {
                // Target tree - uses the target (input) value
                var targetTree = fx.NewBlendTree(input.Name() + "_target");
                targetTree.blendType = BlendTreeType.Simple1D;
                targetTree.useAutomaticThresholds = false;
                targetTree.blendParameter = target.Name();
                targetTree.AddChild(minClip, -1);
                targetTree.AddChild(maxClip, 1);

                //The following two trees merge the update and the maintain tree together. The smoothParam controls 
                //how much from either tree should be applied during each tick
                smoothing = Math.Min(smoothing, 0.99f);
                smoothing = Math.Max(smoothing, 0);
                var smoothTree = fx.NewBlendTree(input.Name() + "_smooth");
                smoothTree.blendType = BlendTreeType.Simple1D;
                smoothTree.useAutomaticThresholds = false;
                smoothTree.blendParameter = speedParam.Name();
                smoothTree.AddChild(targetTree, 0);
                smoothTree.AddChild(maintainTree, 1);

                return smoothTree;
            }

            var layer = fx.NewLayer("Smoothing " + input.Name());

            if (pauseWhen != null) {
                var off = layer.NewState("Maintain").WithAnimation(zeroWhenPaused ? GenerateSmoothingTree(fx.NewFloat("zero")) : maintainTree);
                var smooth = layer.NewState("Smooth").WithAnimation(GenerateSmoothingTree(input));
                smooth.TransitionsTo(off).When(pauseWhen);
                off.TransitionsTo(smooth).When(pauseWhen.Not());
            } else {
                layer.NewState("Smooth").WithAnimation(GenerateSmoothingTree(input));
            }

            return output;
        }

        public VFACondition GreaterThan(VFAFloat a, VFAFloat b, bool orEqualTo = false) {
            var fx = GetFx();
            var bIsWinning = fx.NewFloat("comparison");
            var layer = fx.NewLayer($"{a.Name()} vs {b.Name()}");
            var tree = IsBWinningTree(a, b, bIsWinning);
            layer.NewState($"{a.Name()} vs {b.Name()}").WithAnimation(tree);
            if (orEqualTo) return bIsWinning.IsGreaterThan(0.5f).Not();
            return bIsWinning.IsLessThan(0.5f);
        }

        public Motion IsBWinningTree(VFAFloat a, VFAFloat b, VFAFloat bWinning) {
            var fx = GetFx();
            var bWinningClip = fx.NewClip("vs1");
            bWinningClip.SetCurve("", typeof(Animator), bWinning.Name(), AnimationCurve.Constant(0, 0, 1f));
            var aWinningClip = fx.NewClip("vs0");
            aWinningClip.SetCurve("", typeof(Animator), bWinning.Name(), AnimationCurve.Constant(0, 0, 0f));
            var tree = fx.NewBlendTree($"{a.Name()} vs {b.Name()}");
            tree.useAutomaticThresholds = false;
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.AddChild(aWinningClip, new Vector2(1f, 0));
            tree.AddChild(bWinningClip, new Vector2(0, 1f));
            tree.blendParameter = a.Name();
            tree.blendParameterY = b.Name();
            return tree;
        }

        public VFAFloat Map(VFAFloat input, float inMin, float inMax, float outMin, float outMax) {
            var fx = GetFx();

            var output = fx.NewFloat(input.Name() + "_mapped");

            // These clips drive the output param to certain values
            var minClip = fx.NewClip(input.Name() + "Min");
            minClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, outMin));
            var maxClip = fx.NewClip(input.Name() + "Max");
            maxClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, outMax));

            var tree = fx.NewBlendTree($"{input.Name()}_map_{inMin}_{inMax}_to_{outMin}_{outMax}");
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            tree.blendParameter = input.Name();
            if (inMin < inMax) {
                tree.AddChild(minClip, inMin);
                tree.AddChild(maxClip, inMax);
            } else {
                tree.AddChild(maxClip, inMax);
                tree.AddChild(minClip, inMin);
            }

            var layer = fx.NewLayer($"Map {input.Name()} from {inMin}-{inMax} to {outMin}-{outMax}");
            layer.NewState($"Run").WithAnimation(tree);

            return output;
        }
    }
}
