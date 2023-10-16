using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {

    /**
     * Performs math within an animator
     */
    [VFService]
    public class MathService {
        [VFAutowired] private readonly AvatarManager avatarManager;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        
        // A VFAFloat, but it's guaranteed to be 0 or 1
        public class VFAFloatBool {
            private readonly VFAFloat param;

            public VFAFloatBool(VFAFloat param, bool alwaysFalse = false, bool alwaysTrue = false) {
                this.param = param;
                this.alwaysTrue = alwaysTrue;
                this.alwaysFalse = alwaysFalse;
            }
            public string Name() {
                return param.Name();
            }
            public bool GetDefault() {
                return param.GetDefault() > 0.5;
            }
            public VFCondition IsTrue() {
                return param.IsGreaterThan(0.5f);
            }
            public bool alwaysTrue { get; }
            public bool alwaysFalse { get; }
            public static implicit operator VFAFloat(VFAFloatBool d) => d.param;
        }

        public VFAFloat SetValueWithConditions(
            string name,
            params (VFAFloat,VFAFloatBool)[] targets
        ) {
            var fx = avatarManager.GetFx();

            var defaultValue = targets
                .Where(target => target.Item2 == null || target.Item2.GetDefault())
                .Select(target => target.Item1.GetDefault())
                .First();

            var output = fx.NewFloat(name, def: defaultValue);

            VFAFloatBool anyPreviousState = null;
            foreach (var target in targets) {
                var couldBeThisState = target.Item2 ?? True();

                var isThisState = (anyPreviousState == null)
                    ? couldBeThisState
                    : And(couldBeThisState, Not(anyPreviousState));

                if (anyPreviousState == null) {
                    anyPreviousState = isThisState;
                } else {
                    anyPreviousState = Or(anyPreviousState, couldBeThisState);
                }

                directTree.Add(isThisState, MakeCopier(target.Item1, output));
            }

            return output;
        }

        private VFAFloatBool False() {
            var fx = avatarManager.GetFx();
            return new VFAFloatBool(fx.Zero(), alwaysFalse: true);
        }
        private VFAFloatBool True() {
            var fx = avatarManager.GetFx();
            return new VFAFloatBool(fx.One(), alwaysTrue: true);
        }

        public VFAFloat Map(string name, VFAFloat input, float inMin, float inMax, float outMin, float outMax) {
            var fx = avatarManager.GetFx();
            var outputDefault = VrcfMath.Map(input.GetDefault(), inMin, inMax, outMin, outMax);
            outputDefault = VrcfMath.Clamp(outputDefault, outMin, outMax);
            var output = fx.NewFloat(name, def: outputDefault);

            // These clips drive the output param to certain values
            var minClip = MakeSetter(output, outMin);
            var maxClip = MakeSetter(output, outMax);

            var tree = fx.NewBlendTree($"{input.Name()} ({inMin}-{inMax}) -> ({outMin}-{outMax})");
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

            directTree.Add(tree);

            return output;
        }

        public VFAFloatBool GreaterThan(VFAFloat a, VFAFloat b, bool orEqual = false, string name = null) {
            var sub = Subtract($"{a.Name()} - {b.Name()}", a, b);
            if (orEqual) {
                return new VFAFloatBool(Map1D(name ?? $"({a.Name()}) >= ({b.Name()})", sub, (VRCFuryEditorUtils.NextFloatDown(0), 0), (0, 1)));
            }
            return new VFAFloatBool(Map1D(name ?? $"({a.Name()}) > ({b.Name()})", sub, (0, 0), (VRCFuryEditorUtils.NextFloatUp(0), 1)));
        }
        
        public VFAFloatBool GreaterThan(VFAFloat a, float b, bool orEqual = false, string name = null) {
            if (orEqual) {
                return new VFAFloatBool(Map1D(name ?? $"({a.Name()}) >= {b}", a, (VRCFuryEditorUtils.NextFloatDown(b), 0), (b, 1)));
            }
            return new VFAFloatBool(Map1D(name ?? $"({a.Name()}) > {b}", a, (b, 0), (VRCFuryEditorUtils.NextFloatUp(b), 1)));
        }
        
        public VFAFloatBool LessThan(VFAFloat a, float b, bool orEqual = false, string name = null) {
            if (orEqual) {
                return new VFAFloatBool(Map1D(name ?? $"({a.Name()}) <= {b}", a, (b, 1), (VRCFuryEditorUtils.NextFloatUp(b), 0)));
            }
            return new VFAFloatBool(Map1D(name ?? $"({a.Name()}) < {b}", a, (VRCFuryEditorUtils.NextFloatDown(b), 1), (b, 0)));
        }

        public VFAFloat Subtract(string name, VFAFloat a, VFAFloat b) {
            return Add(name, a, b, true);
        }
        
        public VFAFloat Add(string name, VFAFloat a, VFAFloat b, bool subtract = false) {
            var fx = avatarManager.GetFx();
            var output = fx.NewFloat(name, def: subtract ? a.GetDefault() - b.GetDefault() : a.GetDefault() + b.GetDefault());
            var zeroClip = MakeSetter(output, 0);
            var oneClip = MakeSetter(output, 1);

            directTree.Add(zeroClip);
            directTree.Add(a, oneClip);
            directTree.Add(b, !subtract ? oneClip : MakeSetter(output, -1));

            return output;
        }

        public AnimationClip MakeSetter(VFAFloat param, float value) {
            var fx = avatarManager.GetFx();
            var clip = fx.NewClip($"{param.Name()} = {value}");
            clip.SetConstant(EditorCurveBinding.FloatCurve("", typeof(Animator), param.Name()), value);
            return clip;
        }

        public BlendTree Make1D(string name, VFAFloat param, params (Motion, float)[] children) {
            var fx = avatarManager.GetFx();
            var tree = fx.NewBlendTree(name);
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            tree.blendParameter = param.Name();
            foreach (var (motion,threshold) in children) {
                tree.AddChild(motion, threshold);
            }
            return tree;
        }

        public VFAFloat Map1D(string name, VFAFloat input, params (float, float)[] children) {
            var fx = avatarManager.GetFx();
            var defaultValue = children
                .Where(child => input.GetDefault() <= child.Item1)
                .Select(child => child.Item2)
                .DefaultIfEmpty(children.Last().Item2)
                .First();
            var output = fx.NewFloat(name, def: defaultValue);
            directTree.Add(Make1D(
                name,
                input,
                children.Select(child => ((Motion)MakeSetter(output, child.Item2), child.Item1)).ToArray()
            ));
            return output;
        }

        public BlendTree MakeDirect(string name) {
            var fx = avatarManager.GetFx();
            var tree = fx.NewBlendTree(name);
            tree.blendType = BlendTreeType.Direct;
            return tree;
        }
        
        /**
         * Only works on values > 0 !
         * Value MUST be defaulted to 0, or the copy will ADD to it
         */
        public BlendTree MakeCopier(VFAFloat from, VFAFloat to) {
            var direct = MakeDirect($"{to.Name()} = ({from.Name()})");
            direct.AddDirectChild(True().Name(), MakeSetter(to, 0));
            direct.AddDirectChild(from.Name(), MakeSetter(to, 1));
            return direct;
        }
        
        public BlendTree MakeMaintainer(VFAFloat param) {
            return MakeCopier(param, param);
        }

        public VFAFloatBool Or(VFAFloatBool a, VFAFloatBool b) {
            if (a.alwaysTrue || b.alwaysTrue) return True();
            if (a.alwaysFalse) return b;
            if (b.alwaysFalse) return a;
            return GreaterThan(Add($"({a.Name()}) OR ({b.Name()})", a, b), 0.5f);
        }
        
        public VFAFloatBool And(VFAFloatBool a, VFAFloatBool b) {
            if (a.alwaysFalse || b.alwaysFalse) return False();
            if (a.alwaysTrue) return b;
            if (b.alwaysTrue) return a;
            return GreaterThan(Add($"({a.Name()}) AND ({b.Name()})", a, b), 1.5f);
        }
        
        public VFAFloatBool Not(VFAFloatBool a) {
            if (a.alwaysFalse) return True();
            if (a.alwaysTrue) return False();
            return LessThan(a, 0, true);
        }

        public VFAFloat Max(VFAFloat a, VFAFloat b) {
            return SetValueWithConditions($"Max of ({a.Name()}) or ({b.Name()})",
                (a, GreaterThan(a, b)),
                (b, null)
            );
        }
        
        public VFAFloat Multiply(string name, VFAFloat a, VFAFloat b) {
            var fx = avatarManager.GetFx();
            var output = fx.NewFloat(name, def: a.GetDefault() * b.GetDefault());
            var zeroClip = MakeSetter(output, 0);
            var oneClip = MakeSetter(output, 1);
            
            var subTree = MakeDirect("Multiply");
            subTree.AddDirectChild(b.Name(), oneClip);

            directTree.Add(zeroClip);
            directTree.Add(a, subTree);

            return output;
        }
    }
}
