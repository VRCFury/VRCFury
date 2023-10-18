using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            public Func<Motion, Motion, Motion> create { private set; get; }
            public bool defaultIsTrue { private set; get; }

            public VFAFloatBool(Func<Motion, Motion, Motion> create, bool defaultIsTrue) {
                this.create = create;
                this.defaultIsTrue = defaultIsTrue;
            }
        }

        public class VFAFloatOrConst {
            public VFAFloat param { get; private set; }
            public float constt  { get; private set; }
            public static implicit operator VFAFloatOrConst(VFAFloat d) => new VFAFloatOrConst() { param = d };
            public static implicit operator VFAFloatOrConst(float d) => new VFAFloatOrConst() { constt = d };
            public float GetDefault() => param?.GetDefault() ?? constt;
        }

        public VFAFloat SetValueWithConditions(
            string name,
            params (VFAFloatOrConst,VFAFloatBool)[] targets
        ) {
            var defaultValue = targets
                .Where(target => target.Item2 == null || target.Item2.defaultIsTrue)
                .Select(target => target.Item1.GetDefault())
                .First();

            var output = MakeZeroBasisFloat(name, def: defaultValue);

            Motion elseTree = null;
            foreach (var (target, when) in targets.Reverse()) {
                var doWhenTrue = MakeCopier(target, output);

                if (when == null || elseTree == null) {
                    elseTree = doWhenTrue;
                    continue;
                }

                elseTree = when.create(doWhenTrue, elseTree);
            }
            directTree.Add(elseTree);
            return output;
        }

        public VFAFloatBool False() {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => whenFalse,
                false
            );
        }
        public VFAFloatBool True() {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => whenTrue,
                true
            );
        }

        public VFAFloat Map(string name, VFAFloat input, float inMin, float inMax, float outMin, float outMax) {
            var fx = avatarManager.GetFx();
            var outputDefault = VrcfMath.Map(input.GetDefault(), inMin, inMax, outMin, outMax);
            outputDefault = VrcfMath.Clamp(outputDefault, outMin, outMax);
            var output = fx.NewFloat(name, def: outputDefault);

            // These clips drive the output param to certain values
            var minClip = MakeSetter(output, outMin);
            var maxClip = MakeSetter(output, outMax);

            var tree = fx.NewBlendTree($"{CleanName(input)} ({inMin}-{inMax}) -> ({outMin}-{outMax})");
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
        
        public VFAFloatBool Equals(VFAFloat a, float b, string name = null) {
            return new VFAFloatBool((whenTrue, whenFalse) => Make1D(
                name ?? $"{CleanName(a)} == {b}",
                a,
                (Down(b), whenFalse),
                (b, whenTrue),
                (Up(b), whenFalse)
            ), a.GetDefault() == b);
        }

        public VFAFloatBool GreaterThan(VFAFloat a, VFAFloat b, bool orEqual = false) {
            return new VFAFloatBool((whenTrue, whenFalse) => {
                var fx = avatarManager.GetFx();
                var tree = fx.NewBlendTree($"{CleanName(a)} {(orEqual ? ">=" : ">")} {CleanName(b)}");
                tree.blendType = BlendTreeType.SimpleDirectional2D;
                tree.useAutomaticThresholds = false;
                tree.blendParameter = a.Name();
                tree.blendParameterY = b.Name();
                tree.AddChild(whenFalse, new Vector2(-10000, -10000));
                tree.AddChild(whenFalse, new Vector2(10000, 10000));
                tree.AddChild(whenFalse, new Vector2(0, 0));
                tree.AddChild(whenFalse, new Vector2(-10000, 10000));
                tree.AddChild(whenTrue, new Vector2(0.000001f, -0.000001f));
                return tree;
            }, a.GetDefault() > b.GetDefault() || (orEqual && a.GetDefault() == b.GetDefault()));
        }
        
        public VFAFloatBool GreaterThan(VFAFloat a, float b, bool orEqual = false) {
            return new VFAFloatBool((whenTrue, whenFalse) => Make1D(
                $"{CleanName(a)} > {b}",
                a,
                (orEqual ? Down(b) : b, whenFalse),
                (orEqual ? b : Up(b), whenTrue)
            ), a.GetDefault() > b || (orEqual && a.GetDefault() == b));
        }

        public VFAFloatBool LessThan(VFAFloat a, float b, bool orEqual = false) {
            return Not(GreaterThan(a, b, !orEqual));
        }

        private static float Up(float a) {
            return VRCFuryEditorUtils.NextFloatUp(a);
        }
        private static float Down(float a) {
            return VRCFuryEditorUtils.NextFloatDown(a);
        }

        public VFAFloat Subtract(VFAFloatOrConst a, VFAFloatOrConst b, string name = null) {
            return Add(a, b, true, name: name);
        }
        
        private VFAFloat Add(VFAFloatOrConst a, VFAFloatOrConst b, bool subtract = false, string name = null) {
            var fx = avatarManager.GetFx();
            name = name ?? $"{CleanName(a)} {(subtract ? '-' : '+')} {CleanName(b)}";
            var output = MakeZeroBasisFloat(
                name,
                def: subtract ? a.GetDefault() - b.GetDefault() : a.GetDefault() + b.GetDefault()
            );

            var tree = MakeDirect(name);
            directTree.Add(tree);

            if (a.param != null)
                tree.Add(a.param, MakeSetter(output, 1));
            else
                tree.Add(fx.One(), MakeSetter(output, a.constt));

            if (b.param != null)
                tree.Add(b.param, MakeSetter(output, subtract ? -1 : 1));
            else
                tree.Add(fx.One(), MakeSetter(output, (subtract ? -1 : 1) * a.constt));

            return output;
        }

        public AnimationClip MakeSetter(VFAFloat param, float value) {
            var fx = avatarManager.GetFx();
            var clip = fx.NewClip($"{CleanName(param)} = {value}");
            clip.SetConstant(EditorCurveBinding.FloatCurve("", typeof(Animator), param.Name()), value);
            return clip;
        }

        public BlendTree Make1D(string name, VFAFloat param, params (float, Motion)[] children) {
            var fx = avatarManager.GetFx();
            var tree = fx.NewBlendTree(name);
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            tree.blendParameter = param.Name();
            foreach (var (threshold, motion) in children) {
                tree.AddChild(motion, threshold);
            }
            return tree;
        }

        /*
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
        */

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
        public Motion MakeCopier(VFAFloatOrConst from, VFAFloat to) {
            if (from.param != null) {
                var direct = MakeDirect($"{CleanName(to)} = {CleanName(from)}");
                direct.Add(from.param, MakeSetter(to, 1));
                return direct;
            } else {
                return MakeSetter(to, from.constt);
            }
        }
        
        public Motion MakeMaintainer(VFAFloat param) {
            return MakeCopier(param, param);
        }

        public VFAFloatBool Or(VFAFloatBool a, VFAFloatBool b, string name = null) {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => a.create(whenTrue, b.create(whenTrue, whenFalse)),
                a.defaultIsTrue || b.defaultIsTrue
            );
        }
        
        public VFAFloatBool And(VFAFloatBool a, VFAFloatBool b, string name = null) {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => a.create(b.create(whenTrue, whenFalse), whenFalse),
                a.defaultIsTrue && b.defaultIsTrue
            );
        }
        
        public VFAFloatBool Not(VFAFloatBool a) {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => a.create(whenFalse, whenTrue),
                !a.defaultIsTrue
            );
        }

        public VFAFloat Max(VFAFloat a, VFAFloat b) {
            return SetValueWithConditions($"MAX({CleanName(a)},{CleanName(b)})",
                (a, GreaterThan(a, b)),
                (b, null)
            );
        }
        
        public VFAFloat Multiply(string name, VFAFloat a, VFAFloatOrConst b) {
            var fx = avatarManager.GetFx();
            var output = MakeZeroBasisFloat(name, def: a.GetDefault() * b.GetDefault());

            if (b.param != null) {
                var subTree = MakeDirect("Multiply");
                subTree.Add(b.param, MakeSetter(output, 1));
                directTree.Add(a, subTree);
            } else {
                directTree.Add(a, MakeSetter(output, b.constt));
            }

            return output;
        }

        private static string CleanName(VFAFloatOrConst a) {
            if (a.param != null) return a.param.Name();
            return a.constt + "";
        }

        // When controlling an AAP using a blend tree, the "default value" of the parameter will be included (at least partially)
        // in the calculated value UNLESS the weight of the inputs is >= 1. We can prevent it from being involved at all by animating the
        // value to 0 with weight 1. We can skip this if the default is already 0 though.
        public VFAFloat MakeZeroBasisFloat(string name, float def) {
            var fx = avatarManager.GetFx();
            var output = fx.NewFloat(name, def: def);
            if (def != 0) directTree.Add(MakeSetter(output, 0));
            return output;
        }
    }
}
