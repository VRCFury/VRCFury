using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {

    /**
     * Performs math within an animator
     */
    [VFService]
    [VFPrototypeScope]
    internal class MathService {
        [VFAutowired] private readonly AvatarManager avatarManager;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        private ControllerManager fx => avatarManager.GetFx();
        
        // A VFAFloat, but it's guaranteed to be 0 or 1
        public class VFAFloatBool {
            public delegate Motion CreateCallback(Motion whenTrue, Motion whenFalse);
            public CreateCallback create { private set; get; }
            public bool defaultIsTrue { private set; get; }

            public VFAFloatBool(CreateCallback create, bool defaultIsTrue) {
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

        public class VFAap {
            private VFAFloat value;
            public VFAap(VFAFloat value) {
                this.value = value;
            }
            public static implicit operator VFAFloat(VFAap d) => d.value;
            public static implicit operator VFAFloatOrConst(VFAap d) => d.value;
            public string Name() => value.Name();
            public float GetDefault() => value.GetDefault();
            public VFAFloat AsFloat() => value;
        }

        /**
         * value : [0,Infinity)
         */
        public VFAap SetValueWithConditions(
            string name,
            params (VFAFloatOrConst value,VFAFloatBool condition)[] targets
        ) {
            var defaultValue = targets
                .Where(target => target.condition == null || target.condition.defaultIsTrue)
                .Select(target => target.value.GetDefault())
                .DefaultIfEmpty(0)
                .First();

            var output = MakeAap(name, def: defaultValue);

            // The "fall through" (if all conditions are false) is to maintain the current output value
            var elseTree = MakeCopier(output, output);

            foreach (var target in targets.Reverse()) {
                var doWhenTrue = MakeCopier(target.value, output);

                if (target.condition == null) {
                    elseTree = doWhenTrue;
                    continue;
                }

                elseTree = target.condition.create(doWhenTrue, elseTree);
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

        /**
         * input : (-Infinity,Infinity)
         */
        public VFAap Map(string name, VFAFloat input, float inMin, float inMax, float outMin, float outMax) {
            var outputDefault = VrcfMath.Map(input.GetDefault(), inMin, inMax, outMin, outMax);
            outputDefault = VrcfMath.Clamp(outputDefault, outMin, outMax);
            var output = MakeAap(name, def: outputDefault);

            // These clips drive the output param to certain values
            var minClip = MakeSetter(output, outMin);
            var maxClip = MakeSetter(output, outMax);

            var tree = clipFactory.NewBlendTree($"{CleanName(input)} ({inMin}-{inMax}) -> ({outMin}-{outMax})");
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
        
        /**
         * a,b : (-Infinity,Infinity)
         */
        public VFAFloatBool Equals(VFAFloat a, float b, string name = null) {
            return new VFAFloatBool((whenTrue, whenFalse) => Make1D(
                name ?? $"{CleanName(a)} == {b}",
                a,
                (Down(b), whenFalse),
                (b, whenTrue),
                (Up(b), whenFalse)
            ), a.GetDefault() == b);
        }

        /**
         * a,b : [-10000,10000]
         */
        public VFAFloatBool GreaterThan(VFAFloat a, VFAFloat b, bool orEqual = false, string name = null) {
            name = name ?? $"{CleanName(a)} {(orEqual ? ">=" : ">")} {CleanName(b)}";
            return new VFAFloatBool((whenTrue, whenFalse) => {
                var tree = clipFactory.NewBlendTree(name);
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
        
        /**
         * a,b : (-Infinity,Infinity)
         */
        public VFAFloatBool GreaterThan(VFAFloat a, float b, bool orEqual = false, string name = null) {
            name = name ?? $"{CleanName(a)} > {b}";
            return new VFAFloatBool((whenTrue, whenFalse) => Make1D(
                name,
                a,
                (orEqual ? Down(b) : b, whenFalse),
                (orEqual ? b : Up(b), whenTrue)
            ), a.GetDefault() > b || (orEqual && a.GetDefault() == b));
        }

        /**
         * a,b : (-Infinity,Infinity)
         */
        public VFAFloatBool LessThan(VFAFloat a, float b, bool orEqual = false, string name = null) {
            return Not(GreaterThan(a, b, !orEqual, name));
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
        public VFAap Subtract(VFAFloatOrConst a, VFAFloatOrConst b, string name = null) {
            name = name ?? $"{CleanName(a)} - {CleanName(b)}";
            return Add(name, (a,1), (b,-1));
        }
        
        /**
         * a,b : [0,Infinity)
         */
        public VFAap Add(VFAFloatOrConst a, VFAFloatOrConst b, string name = null) {
            name = name ?? $"{CleanName(a)} + {CleanName(b)}";
            return Add(name, (a,1), (b,1));
        }
        
        /**
         * input : [0,Infinity)
         * multiplier : (-Infinity,Infinity)
         */
        public VFAap Add(string name, params (VFAFloatOrConst input,float multiplier)[] components) {
            float def = 0;
            foreach (var c in components) {
                if (c.input.param != null) {
                    def += c.input.param.GetDefault() * c.multiplier;
                } else {
                    def += c.input.constt * c.multiplier;
                }
            }

            var output = MakeAap(name, def: def);

            var clipCache = new Dictionary<float, AnimationClip>();
            AnimationClip MakeCachedSetter(float multiplier) {
                if (clipCache.TryGetValue(multiplier, out var cached)) return cached;
                return clipCache[multiplier] = MakeSetter(output, multiplier);
            }
            
            foreach (var (input,multiplier) in components) {
                if (input.param != null) {
                    directTree.Add(input.param, MakeCachedSetter(multiplier));
                } else {
                    directTree.Add(MakeCachedSetter(input.constt * multiplier));
                }
            }

            return output;
        }

        public AnimationClip MakeSetter(VFAap param, float value) {
            var clip = clipFactory.NewClip($"{CleanName(param)} = {value}");
            clip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), param.Name()), value);
            return clip;
        }

        public BlendTree Make1D(string name, VFAFloat param, params (float, Motion)[] children) {
            var tree = clipFactory.NewBlendTree(name);
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
            var tree = clipFactory.NewBlendTree(name);
            tree.blendType = BlendTreeType.Direct;
            return tree;
        }
        
        /**
         * from : [0,Infinity)
         */
        public VFAap Buffer(VFAFloat from, string to = null, bool usePrefix = true) {
            to = to ?? $"{CleanName(from)}_b";
            var output = MakeAap(to, from.GetDefault(), usePrefix: usePrefix);
            directTree.Add(MakeCopier(from, output));
            return output;
        }
        
        public VFAFloatBool Buffer(VFAFloatBool from, string to) {
            var buffered = SetValueWithConditions(to, (1, from), (0, null));
            return GreaterThan(buffered, 0.5f);
        }

        public Motion MakeCopier(VFAFloatOrConst from, VFAap to, float minSupported = 0, float maxSupported = float.MaxValue) {
            if (from.param == null) {
                return MakeSetter(to, from.constt);
            }

            var name = $"{CleanName(to)} = {CleanName(from)}";
            if (minSupported >= 0) {
                var direct = MakeDirect(name);
                direct.Add(from.param, MakeSetter(to, 1));
                return direct;
            }

            return Make1D(name, from.param,
                (minSupported, MakeSetter(to, minSupported)),
                (maxSupported, MakeSetter(to, maxSupported))
            );
        }

        public VFAFloatBool Or(VFAFloatBool a, VFAFloatBool b) {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => a.create(whenTrue, b.create(whenTrue, whenFalse)),
                a.defaultIsTrue || b.defaultIsTrue
            );
        }
        
        public VFAFloatBool And(VFAFloatBool a, VFAFloatBool b) {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => a.create(b.create(whenTrue, whenFalse), whenFalse),
                a.defaultIsTrue && b.defaultIsTrue
            );
        }
        
        public VFAFloatBool Xor(VFAFloatBool a, VFAFloatBool b) {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => a.create(b.create(whenFalse, whenTrue), b.create(whenTrue, whenFalse)),
                a.defaultIsTrue ^ b.defaultIsTrue
            );
        }
        
        public VFAFloatBool Not(VFAFloatBool a) {
            return new VFAFloatBool(
                (whenTrue, whenFalse) => a.create(whenFalse, whenTrue),
                !a.defaultIsTrue
            );
        }

        public VFAap Max(VFAFloat a, VFAFloat b, string name = null) {
            name = name ?? $"MAX({CleanName(a)},{CleanName(b)})";
            return SetValueWithConditions(name,
                (a, GreaterThan(a, b)),
                (b, null)
            );
        }
        
        /**
         * a : [0,Infinity)
         * b : [0,Infinity) (may be negative if constant)
         */
        public VFAap Multiply(string name, VFAFloat a, VFAFloatOrConst b) {
            var output = MakeAap(name, def: a.GetDefault() * b.GetDefault());

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

        /**
         * WARNING: If your aap is animated from a direct blendtree OTHER THAN the main shared direct blendtree, you must set animatedFromDefaultTree to false
         * and call MakeAapSafe in all of the blendtrees animating the aap.
         */
        public VFAap MakeAap(string name, float def = 0, bool usePrefix = true, bool animatedFromDefaultTree = true) {
            var aap = new VFAap(fx.NewFloat(name, def: def, usePrefix: usePrefix));
            if (animatedFromDefaultTree) MakeAapSafe(directTree.GetTree(), aap);
            return aap;
        }

        /**
         * When controlling an AAP using a blend tree, the "default value" of the parameter will be included (at least partially)
         * in the calculated value UNLESS the weight of the inputs is >= 1. We can prevent it from being involved at all by animating the
         * value to 0 with weight 1.
         * 
         * We CANNOT skip this even if the default value of the parameter is 0, because vrchat can cause the animator's parameter defaults
         * to change unexpectedly in situations such as leaving a station.
         * 
         * In theory, we could skip the safety setter IF it's guaranteed that the weight will always be >= 1 in all other usages of the AAP
         * but this is complicated to keep track of for limited benefit.
         *         
         * WARNING: If your aap is animated from a direct blendtree OUTSIDE of the main shared direct blendtree, you must set useWeightProtection to false
         * and ensure that you weight protect the variable in your own tree.
         */
        public void MakeAapSafe(BlendTree blendTree, VFAap aap) {
            blendTree.Add(fx.One(), MakeSetter(aap, 0));
        }
    }
}
