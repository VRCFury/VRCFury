using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Service;
using VF.Inspector;
using VF.Utils.Controller;

namespace VF.Utils {

    /**
     * Performs math within an animator
     */
    internal class BlendtreeMath {
        private readonly VFAvatarController controller;
        private readonly VFBlendTreeDirect directTree;
        private readonly Dictionary<VFClip, (VFBlendTreeDirect tree, Dictionary<string, VFClip> outputs)> multipliedClips = new();

        public BlendtreeMath(VFAvatarController controller, VFBlendTreeDirect directTree) {
            this.controller = controller;
            this.directTree = directTree;
        }

        // A boolean condition implemented using blend trees.
        public class Condition {
            public delegate VFMotion CreateCallback(VFMotion whenTrue, VFMotion whenFalse);
            public CreateCallback create { private set; get; }
            public bool defaultIsTrue { private set; get; }

            public Condition(CreateCallback create, bool defaultIsTrue) {
                this.create = create;
                this.defaultIsTrue = defaultIsTrue;
            }

            // Share a condition across outputs without buffering it through an AAP.
            public (VFBlendTreeDirect whenTrue, VFBlendTreeDirect whenFalse) CreateBranches(VFBlendTreeDirect parent, string name) {
                var whenTrue = VFBlendTreeDirect.Create(name + " - True");
                var whenFalse = VFBlendTreeDirect.Create(name + " - False");
                parent.Add(create(whenTrue, whenFalse));
                return (whenTrue, whenFalse);
            }

            public Condition Or(Condition b) {
                return new Condition(
                    (whenTrue, whenFalse) => create(
                        whenTrue,
                        b.create(whenTrue, whenFalse)
                    ),
                    defaultIsTrue || b.defaultIsTrue
                );
            }
        
            public Condition And(Condition b) {
                return new Condition(
                    (whenTrue, whenFalse) => create(
                        b.create(whenTrue, whenFalse),
                        whenFalse
                    ),
                    defaultIsTrue && b.defaultIsTrue
                );
            }
        
            public Condition Xor(Condition b) {
                return new Condition(
                    (whenTrue, whenFalse) => create(
                        b.create(whenFalse, whenTrue),
                        b.create(whenTrue, whenFalse)
                    ),
                    defaultIsTrue ^ b.defaultIsTrue
                );
            }
        
            public Condition Not() {
                return new Condition(
                    (whenTrue, whenFalse) => create(whenFalse, whenTrue),
                    !defaultIsTrue
                );
            }
        }

        public class Value {
            public VFAParam param { get; private set; }
            public float constt  { get; private set; }
            public static implicit operator Value(VFAParam d) => new Value() { param = d };
            public static implicit operator Value(float d) => new Value() { constt = d };
            public float GetDefault() => param?.GetDefault() ?? constt;
            public override string ToString() {
                if (param != null) return param.ToString();
                return constt.ToString();
            }
        }

        /**
         * value : [0,Infinity)
         */
        public VFAParam SetValueWithConditions(
            string name,
            params (Value value,Condition condition)[] targets
        ) {
            var defaultValue = targets
                .Where(target => target.condition == null || target.condition.defaultIsTrue)
                .Select(target => target.value.GetDefault())
                .DefaultIfEmpty(0)
                .First();

            var output = controller.CreateUniqueFloat(name, def: defaultValue);

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
            params (VFMotion whenTrue,Condition condition)[] targets
        ) {
            VFMotion elseTree = null;

            foreach (var target in targets.Reverse()) {
                if (target.condition == null) {
                    elseTree = target.whenTrue;
                    continue;
                }

                elseTree = target.condition.create(target.whenTrue, elseTree);
            }
            directTree.Add(elseTree);
        }

        public static Condition False() {
            return new Condition(
                (whenTrue, whenFalse) => whenFalse,
                false
            );
        }
        public static Condition True() {
            return new Condition(
                (whenTrue, whenFalse) => whenTrue,
                true
            );
        }

        /**
         * input : (-Infinity,Infinity)
         */
        public VFAParam Map(string name, VFAParam input, float inMin, float inMax, float outMin, float outMax) {
            var outputDefault = VrcfMath.Map(input.GetDefault(), inMin, inMax, outMin, outMax);
            outputDefault = VrcfMath.Clamp(outputDefault, outMin, outMax);
            var output = controller.CreateUniqueFloat(name, def: outputDefault);

            directTree.Add(Map(output, input, inMin, inMax, outMin, outMax));
            return output;
        }

        public static VFMotion Map(VFAParam output, VFAParam input, float inMin, float inMax, float outMin, float outMax) {
            var minClip = output.MakeSetter(outMin);
            var maxClip = output.MakeSetter(outMax);
            return inMin < inMax
                ? VFBlendTree1D.CreateWithData($"{input} ({inMin}-{inMax}) -> ({outMin}-{outMax})", input,
                    (inMin, minClip), (inMax, maxClip))
                : VFBlendTree1D.CreateWithData($"{input} ({inMin}-{inMax}) -> ({outMin}-{outMax})", input,
                    (inMax, maxClip), (inMin, minClip));
        }

        /**
         * a,b : (-Infinity,Infinity)
         */
        public static Condition Equals(VFAParam a, float b, string name = null, float epsilon = 0) {
            return new Condition((whenTrue, whenFalse) => VFBlendTree1D.CreateWithData(
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
        public static Condition GreaterThan(VFAParam a, VFAParam b, string name = null) {
            name = name ?? $"{a} > {b}";
            return new Condition((whenTrue, whenFalse) => {
                if (whenTrue == null) whenTrue = VFClip.Create();
                if (whenFalse == null) whenFalse = VFClip.Create();
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
        public static Condition GreaterThan(VFAParam a, float b, bool orEqual = false, string name = null) {
            name = name ?? $"{a} {(orEqual ? ">=" : ">")} {b}";
            return new Condition((whenTrue, whenFalse) => VFBlendTree1D.CreateWithData(
                name,
                a,
                (orEqual ? Down(b) : b, whenFalse),
                (orEqual ? b : Up(b), whenTrue)
            ), a.GetDefault() > b || (orEqual && a.GetDefault() == b));
        }

        /**
         * a,b : (-Infinity,Infinity)
         */
        public static Condition LessThan(VFAParam a, float b, bool orEqual = false, string name = null) {
            return GreaterThan(a, b, !orEqual, name).Not();
        }

        // A bounded interval needs one 1D tree, rather than two nested comparisons.
        public static Condition InRange(VFAParam input, float min, float max, bool includeMin = true, bool includeMax = true) {
            var first = includeMin ? min : Up(min);
            var last = includeMax ? max : Down(max);
            if (first > last) return False();
            if (first == last) return Equals(input, first);
            return new Condition((whenTrue, whenFalse) => VFBlendTree1D.CreateWithData(
                $"{input} in {min}..{max}", input,
                (Down(first), whenFalse), (first, whenTrue),
                (last, whenTrue), (Up(last), whenFalse)),
                input.GetDefault() >= first && input.GetDefault() <= last);
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
        public VFAParam Subtract(Value a, Value b, string name = null) {
            name = name ?? $"{a} - {b}";
            return Add(name, (a,1), (b,-1));
        }
        
        /**
         * a,b : [0,Infinity)
         */
        public VFAParam Add(Value a, Value b, string name = null) {
            name = name ?? $"{a} + {b}";
            return Add(name, (a,1), (b,1));
        }
        
        /**
         * input : [0,Infinity)
         * multiplier : (-Infinity,Infinity)
         */
        public VFAParam Add(string name, params (Value input,float multiplier)[] components) {
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

            var output = controller.CreateUniqueFloat(name, def: def);
            directTree.Add(Add(name, output, components));
            return output;
        }

        /**
         * input : [0,Infinity)
         * multiplier : (-Infinity,Infinity)
         */
        public static VFMotion Add(string name, VFAParam output, params (Value input,float multiplier)[] components) {
            var terms = components
                .GroupBy(pair => pair.input.param?.name ?? VFBlendTreeDirect.AlwaysOneParam)
                .Select(group => (input: group.Key, multiplier: group.Sum(pair =>
                    pair.multiplier * (pair.input.param == null ? pair.input.constt : 1))))
                .Where(pair => pair.multiplier != 0)
                .ToArray();
            // Cancellation still needs an explicit writer, including when output has a nonzero default.
            if (terms.Length == 0) return output.MakeSetter(0);

            var clipCache = new Dictionary<float, VFClip>();
            VFMotion MakeCachedSetter(float multiplier) {
                if (clipCache.TryGetValue(multiplier, out var cached)) return cached;
                return clipCache[multiplier] = output.MakeSetter(multiplier);
            }

            return Add(name, terms.Select(pair => (pair.input, MakeCachedSetter(pair.multiplier))).ToArray());
        }
        
        /**
         * input : [0,Infinity)
         */
        public static VFMotion Add(string name, params (string input,VFMotion motion)[] components) {
            var tree = VFBlendTreeDirect.Create(name);
            foreach (var (input,motion) in components) {
                tree.Add(input, motion);
            }
            return tree;
        }

        /**
         * Returns 1 / (input * inputMultiplier), with a runtime minimum denominator of 0.0001.
         * Uses two AAP stages.
         */
        public VFAParam Invert(string name, VFAParam input, float inputMultiplier = 1) {
            var inputDefault = input.GetDefault() * inputMultiplier;
            var def = inputDefault == 0 ? 0 : 1f / inputDefault;
            var output = controller.CreateUniqueFloat(name, def: def);
            var tmp = Add($"{name}/Tmp", (input, 10000 * inputMultiplier), (-1, 1));
            var tree = VFBlendTreeDirect.Create(name);
            tree.SetNormalizedBlendValues(true);
            // Cover the remainder with an explicit zero, rather than Unity's parameter default.
            tree.Add(tmp, output.MakeSetter(0));
            tree.Add(output.MakeSetter(10000));
            directTree.Add(tree);
            return output;
        }

        public VFAParam Buffer(VFAParam from, string to = null, FloatOrBool def = null, float minSupported = 0, float maxSupported = float.MaxValue) {
            to = to ?? $"{from}_b";
            var output = controller.CreateUniqueFloat(to, def ?? from.GetDefault());
            directTree.Add(output.MakeCopier(from, minSupported, maxSupported));
            return output;
        }

        public VFAParam Max(VFAParam a, VFAParam b, string name = null) {
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
        public VFAParam Multiply(string name, VFAParam a, Value b) {
            var output = controller.CreateUniqueFloat(name, def: a.GetDefault() * b.GetDefault());

            if (b.param != null) {
                var subTree = VFBlendTreeDirect.Create("Multiply");
                subTree.Add(b.param, output.MakeSetter(1));
                directTree.Add(a, subTree);
            } else {
                directTree.Add(a, output.MakeSetter(b.constt));
            }

            return output;
        }

        public void MultiplyInPlace(VFAParam output, VFAParam multiplier, VFAParam existing, float factor = 1) {
            var oldBinding = VFBinding.MakeAnimatorBinding(existing.name);
            var newBinding = oldBinding.WithPropertyName(output);
            var additions = new List<(VFBlendTreeDirect tree, VFAParam multiplier, VFClip clip)>();
            foreach (var tree in new AnimatorIterator.Trees().From(directTree)) {
                tree.RewriteChildren(child => {
                    if (!(child.motion is VFClip oldClip)) return child;
                    var oldCurve = oldClip.GetCurve(oldBinding, true);
                    if (oldCurve == null) return child;
                    if (oldCurve.FloatCurve.keys.Length == 1 && oldCurve.FloatCurve.keys[0].value == 0) return child;

                    var group = multipliedClips.GetOrCreate(oldClip, () => {
                        var groupTree = VFBlendTreeDirect.Create(oldClip.name);
                        groupTree.Add(oldClip);
                        return (tree: groupTree, outputs: new Dictionary<string, VFClip>());
                    });
                    var outputs = group.outputs.GetOrCreate(multiplier.name, () => {
                        var clip = VFClip.Create($"{oldClip.name} * {multiplier.name}");
                        // Do not append children while RewriteChildren is enumerating them.
                        additions.Add((group.tree, multiplier, clip));
                        return clip;
                    });
                    outputs.SetCurve(newBinding, oldCurve.Scale(factor));
                    // A later conversion can find the source inside its existing group.
                    // Add its binding to the shared weighted clip instead of nesting again.
                    if (tree != (VFMotion)group.tree) child.motion = group.tree;
                    return child;
                });
            }
            foreach (var (tree, weight, clip) in additions) tree.Add(weight, clip);
        }
        
        public void CopyInPlace(VFAParam existing, string output, float multiplier = 1f) {
            // This output is a calculated copy, so its default follows the source too.
            var target = controller.GetOrCreateFloat(output);
            target.SetDefault(existing.GetDefault() * multiplier);
            var oldBinding = VFBinding.MakeAnimatorBinding(existing.name);
            var newBinding = oldBinding.WithPropertyName(output);
            var found = false;
            foreach (var clip in new AnimatorIterator.Clips().From(directTree)) {
                var curve = clip.GetCurve(oldBinding, true);
                if (curve != null) {
                    curve = curve.Scale(multiplier);
                    clip.SetCurve(newBinding, curve);
                    found = true;
                }
            }
            // Measurement constants have defaults but no animated writing curves.
            if (!found) directTree.Add(target.MakeSetter(target.GetDefault()));
        }
    }
}
