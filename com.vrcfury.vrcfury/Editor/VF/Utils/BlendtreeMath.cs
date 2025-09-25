using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VF.Utils.Controller;

namespace VF.Utils {

    /**
     * Performs math within an animator
     */
    internal class BlendtreeMath {
        private readonly ControllerManager controller;
        private readonly VFBlendTreeDirect directTree;

        public BlendtreeMath(ControllerManager controller, VFBlendTreeDirect directTree) {
            this.controller = controller;
            this.directTree = directTree;
        }

        // A VFAFloat, but it's guaranteed to be 0 or 1
        public class VFAFloatBool {
            public delegate Motion CreateCallback(Motion whenTrue, Motion whenFalse);
            public CreateCallback create { private set; get; }
            public bool defaultIsTrue { private set; get; }

            public VFAFloatBool(CreateCallback create, bool defaultIsTrue) {
                this.create = create;
                this.defaultIsTrue = defaultIsTrue;
            }

            public VFAFloatBool Or(VFAFloatBool b) {
                return new VFAFloatBool(
                    (whenTrue, whenFalse) => create(
                        whenTrue,
                        b.create(whenTrue, whenFalse)
                    ),
                    defaultIsTrue || b.defaultIsTrue
                );
            }
        
            public VFAFloatBool And(VFAFloatBool b) {
                return new VFAFloatBool(
                    (whenTrue, whenFalse) => create(
                        b.create(whenTrue, whenFalse),
                        whenFalse
                    ),
                    defaultIsTrue && b.defaultIsTrue
                );
            }
        
            public VFAFloatBool Xor(VFAFloatBool b) {
                return new VFAFloatBool(
                    (whenTrue, whenFalse) => create(
                        b.create(whenFalse, whenTrue),
                        b.create(whenTrue, whenFalse)
                    ),
                    defaultIsTrue ^ b.defaultIsTrue
                );
            }
        
            public VFAFloatBool Not() {
                return new VFAFloatBool(
                    (whenTrue, whenFalse) => create(whenFalse, whenTrue),
                    !defaultIsTrue
                );
            }
        }

        public class VFAFloatOrConst {
            public VFAFloat param { get; private set; }
            public float constt  { get; private set; }
            public static implicit operator VFAFloatOrConst(VFAFloat d) => new VFAFloatOrConst() { param = d };
            public static implicit operator VFAFloatOrConst(float d) => new VFAFloatOrConst() { constt = d };
            public float GetDefault() => param?.GetDefault() ?? constt;
            public override string ToString() {
                if (param != null) return param.ToString();
                return constt.ToString();
            }
        }

        public class VFAap {
            private readonly VFAFloat value;
            public VFAap(VFAFloat value) {
                this.value = value;
            }
            public static implicit operator string(VFAap d) => d.value;
            public static implicit operator VFAFloat(VFAap d) => d.value;
            public static implicit operator VFAFloatOrConst(VFAap d) => d.value;
            public string Name() => value.Name();
            public float GetDefault() => value.GetDefault();
            public VFAFloat AsFloat() => value;

            public AnimationClip MakeSetter(float to) {
                var clip = VrcfObjectFactory.Create<AnimationClip>();
                clip.name = $"AAP: {Name()} = {to}";
                clip.SetAap(Name(), to);
                return clip;
            }

            public Motion MakeCopier(VFAFloat from, float minSupported = 0, float maxSupported = float.MaxValue, float multiplier = 1) {
                var name = $"AAP: {Name()} = {from.Name()}";
                if (multiplier != 1) name += $" * {multiplier}";
                if (minSupported >= 0) {
                    var direct = VFBlendTreeDirect.Create(name);
                    direct.Add(from, MakeSetter(multiplier));
                    return direct;
                }

                return VFBlendTree1D.CreateWithData(name, from,
                    (minSupported, MakeSetter(minSupported*multiplier)),
                    (maxSupported, MakeSetter(maxSupported*multiplier))
                );
            }

            public override string ToString() {
                return Name();
            }
        }

        /**
         * value : [0,Infinity)
         */
        public VFAFloat SetValueWithConditions(
            string name,
            params (VFAFloatOrConst value,VFAFloatBool condition)[] targets
        ) {
            var defaultValue = targets
                .Where(target => target.condition == null || target.condition.defaultIsTrue)
                .Select(target => target.value.GetDefault())
                .DefaultIfEmpty(0)
                .First();

            var output = controller.MakeAap(name, def: defaultValue);

            var targetMotions = targets
                .Select(target => (
                    target.value.param != null ? output.MakeCopier(target.value.param) : output.MakeSetter(target.value.constt),
                    target.condition
                ))
                // The "fall through" (if all conditions are false) is to maintain the current output value
                .Append((output.MakeCopier(output), null))
                .ToArray();
            SetValueWithConditions(targetMotions);

            return output;
        }
        
        public void SetValueWithConditions(
            params (Motion whenTrue,VFAFloatBool condition)[] targets
        ) {
            Motion elseTree = null;

            foreach (var target in targets.Reverse()) {
                if (target.condition == null) {
                    elseTree = target.whenTrue;
                    continue;
                }

                elseTree = target.condition.create(target.whenTrue, elseTree);
            }
            directTree.Add(elseTree);
        }

        public static VFAFloatBool False() {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => whenFalse,
                false
            );
        }
        public static VFAFloatBool True() {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => whenTrue,
                true
            );
        }

        /**
         * input : (-Infinity,Infinity)
         */
        public VFAFloat Map(string name, VFAFloat input, float inMin, float inMax, float outMin, float outMax) {
            var outputDefault = VrcfMath.Map(input.GetDefault(), inMin, inMax, outMin, outMax);
            outputDefault = VrcfMath.Clamp(outputDefault, outMin, outMax);
            var output = controller.MakeAap(name, def: outputDefault);

            // These clips drive the output param to certain values
            var minClip = output.MakeSetter(outMin);
            var maxClip = output.MakeSetter(outMax);

            var tree = VFBlendTree1D.Create($"{input} ({inMin}-{inMax}) -> ({outMin}-{outMax})", input);
            if (inMin < inMax) {
                tree.Add(inMin, minClip);
                tree.Add(inMax, maxClip);
            } else {
                tree.Add(inMax, maxClip);
                tree.Add(inMin, minClip);
            }

            directTree.Add(tree);

            return output;
        }
        
        /**
         * a,b : (-Infinity,Infinity)
         */
        public static VFAFloatBool Equals(VFAFloat a, float b, string name = null, float epsilon = 0) {
            return new VFAFloatBool((whenTrue, whenFalse) => VFBlendTree1D.CreateWithData(
                name ?? $"{a} == {b}",
                a,
                (epsilon == 0 ? Down(b) : b - epsilon, whenFalse),
                (b, whenTrue),
                (epsilon == 0 ? Up(b) : b + epsilon, whenFalse)
            ), a.GetDefault() == b);
        }

        /**
         * a,b : [-10000,10000]
         */
        public static VFAFloatBool GreaterThan(VFAFloat a, VFAFloat b, string name = null) {
            name = name ?? $"{a} > {b}";
            return new VFAFloatBool((whenTrue, whenFalse) => {
                if (whenTrue == null) whenTrue = VrcfObjectFactory.Create<AnimationClip>();
                if (whenFalse == null) whenFalse = VrcfObjectFactory.Create<AnimationClip>();
                var tree = VFBlendTree2D.CreateSimpleDirectional(name, a, b);
                tree.Add(new Vector2(-10000, -10000), whenFalse);
                tree.Add(new Vector2(10000, 10000), whenFalse);
                tree.Add(new Vector2(0, 0), whenFalse);
                tree.Add(new Vector2(-10000, 10000), whenFalse);
                tree.Add(new Vector2(0.000001f, -0.000001f), whenTrue);
                return tree;
            }, a.GetDefault() > b.GetDefault());
        }
        
        /**
         * a,b : (-Infinity,Infinity)
         */
        public static VFAFloatBool GreaterThan(VFAFloat a, float b, bool orEqual = false, string name = null) {
            name = name ?? $"{a} {(orEqual ? ">=" : ">")} {b}";
            return new VFAFloatBool((whenTrue, whenFalse) => VFBlendTree1D.CreateWithData(
                name,
                a,
                (orEqual ? Down(b) : b, whenFalse),
                (orEqual ? b : Up(b), whenTrue)
            ), a.GetDefault() > b || (orEqual && a.GetDefault() == b));
        }

        /**
         * a,b : (-Infinity,Infinity)
         */
        public static VFAFloatBool LessThan(VFAFloat a, float b, bool orEqual = false, string name = null) {
            return GreaterThan(a, b, !orEqual, name).Not();
        }

        private static float Up(float a) {
            return VRCFuryEditorUtils.NextFloatUp(a);
        }
        private static float Down(float a) {
            return VRCFuryEditorUtils.NextFloatDown(a);
        }

        /**
         * a,b : [0,Infinity)
         */
        public VFAFloat Subtract(VFAFloatOrConst a, VFAFloatOrConst b, string name = null) {
            name = name ?? $"{a} - {b}";
            return Add(name, (a,1), (b,-1));
        }
        
        /**
         * a,b : [0,Infinity)
         */
        public VFAFloat Add(VFAFloatOrConst a, VFAFloatOrConst b, string name = null) {
            name = name ?? $"{a} + {b}";
            return Add(name, (a,1), (b,1));
        }
        
        /**
         * input : [0,Infinity)
         * multiplier : (-Infinity,Infinity)
         */
        public VFAFloat Add(string name, params (VFAFloatOrConst input,float multiplier)[] components) {
            if (components.Length == 1 && components[0].multiplier == 1 && components[0].input.param != null) {
                return components[0].input.param;
            }

            float def = 0;
            foreach (var c in components) {
                if (c.input.param != null) {
                    def += c.input.param.GetDefault() * c.multiplier;
                } else {
                    def += c.input.constt * c.multiplier;
                }
            }

            var output = controller.MakeAap(name, def: def);
            directTree.Add(Add(name, output, components));
            return output;
        }

        /**
         * input : [0,Infinity)
         * multiplier : (-Infinity,Infinity)
         */
        public static Motion Add(string name, VFAap output, params (VFAFloatOrConst input,float multiplier)[] components) {
            var clipCache = new Dictionary<float, AnimationClip>();
            Motion MakeCachedSetter(float multiplier) {
                if (clipCache.TryGetValue(multiplier, out var cached)) return cached;
                return clipCache[multiplier] = output.MakeSetter(multiplier);
            }

            var motionComponents = components.Select(pair => {
                if (pair.input.param != null) {
                    return (pair.input.param, MakeCachedSetter(pair.multiplier));
                } else {
                    return (VFBlendTreeDirect.AlwaysOneParam, MakeCachedSetter(pair.input.constt * pair.multiplier));
                }
            }).ToArray();

            return Add(name, motionComponents);
        }
        
        /**
         * input : [0,Infinity)
         */
        public static Motion Add(string name, params (string input,Motion motion)[] components) {
            var tree = VFBlendTreeDirect.Create(name);
            foreach (var (input,motion) in components) {
                tree.Add(input, motion);
            }
            return tree;
        }

        /**
         * Delays by 2 frames
         */
        public VFAFloat Invert(string name, VFAFloat input) {
            var def = input.GetDefault() == 0 ? 0 : 1f / input.GetDefault();
            var output = controller.MakeAap(name, def: def);
            var tmp = Add($"{name}/Tmp", (input, 10000), (-1, 1));
            var tree = VFBlendTreeDirect.Create(name);
            tree.SetNormalizedBlendValues(true);
            tree.Add(tmp, VrcfObjectFactory.Create<AnimationClip>());
            tree.Add(output.MakeSetter(10000));
            directTree.Add(tree);
            return output;
        }

        /*
        public VFAFloat Map1D(string name, VFAFloat input, params (float, float)[] children) {
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
        
        public VFAFloat Buffer(VFAFloat from, string to = null, bool usePrefix = true, float def = -100, float minSupported = 0, float maxSupported = float.MaxValue) {
            to = to ?? $"{from}_b";
            if (def == -100) def = from.GetDefault();
            var output = controller.MakeAap(to, def, usePrefix: usePrefix);
            directTree.Add(output.MakeCopier(from, minSupported, maxSupported));
            return output;
        }

        public VFAFloat Max(VFAFloat a, VFAFloat b, string name = null) {
            name = name ?? $"MAX({a},{b})";
            return SetValueWithConditions(name,
                (a, GreaterThan(a, b)),
                (b, null)
            );
        }
        
        /**
         * a : [0,Infinity)
         * b : [0,Infinity) (may be negative if constant)
         */
        public VFAFloat Multiply(string name, VFAFloat a, VFAFloatOrConst b) {
            var output = controller.MakeAap(name, def: a.GetDefault() * b.GetDefault());

            if (b.param != null) {
                var subTree = VFBlendTreeDirect.Create("Multiply");
                subTree.Add(b.param, output.MakeSetter(1));
                directTree.Add(a, subTree);
            } else {
                directTree.Add(a, output.MakeSetter(b.constt));
            }

            return output;
        }

        public void MultiplyInPlace(VFAap output, VFAFloat multiplier, VFAFloat existing) {
            var oldBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), existing.Name());
            var newBinding = oldBinding;
            newBinding.propertyName = output;
            foreach (var tree in new AnimatorIterator.Trees().From(directTree)) {
                tree.RewriteChildren(child => {
                    if (!(child.motion is AnimationClip oldClip)) return child;
                    var oldCurve = oldClip.GetCurve(oldBinding, true);
                    if (oldCurve == null) return child;
                    if (oldCurve.FloatCurve.keys.Length == 1 && oldCurve.FloatCurve.keys[0].value == 0) return child;

                    var newClip = VrcfObjectFactory.Create<AnimationClip>();
                    newClip.name = $"{output.Name()} = {oldCurve.FloatCurve.keys[0].value}";
                    var newCurve = oldCurve.Clone();
                    newClip.SetCurve(newBinding, newCurve);
                    var newTree = VFBlendTreeDirect.Create(oldClip.name);
                    newTree.Add(oldClip);
                    newTree.Add(multiplier, newClip);

                    child.motion = newTree;
                    return child;
                });
            }
        }
        
        public void CopyInPlace(VFAFloat existing, string output, float multiplier = 1f) {
            var oldBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), existing.Name());
            var newBinding = oldBinding;
            newBinding.propertyName = output;
            foreach (var clip in new AnimatorIterator.Clips().From(directTree)) {
                var curve = clip.GetCurve(oldBinding, true);
                if (curve != null) {
                    curve = curve.Scale(multiplier);
                    clip.SetCurve(newBinding, curve);
                }
            }
        }
    }
}
