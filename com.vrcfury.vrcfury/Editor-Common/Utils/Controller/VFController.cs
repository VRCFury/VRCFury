using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;

namespace VF.Utils.Controller {
    internal class VFController {
        private readonly AnimatorController ctrl;

        public VFController(AnimatorController ctrl) {
            this.ctrl = ctrl;
        }
    
        //public static implicit operator VFController(AnimatorController d) => new VFController(d);
        //public static implicit operator AnimatorController(VFController d) => d?.ctrl;
        //public static implicit operator bool(VFController d) => d?.ctrl != null;
        public static bool operator ==(VFController a, VFController b) => a?.Equals(b) ?? b?.Equals(null) ?? true;
        public static bool operator !=(VFController a, VFController b) => !(a == b);
        public override bool Equals(object other) {
            return (other is VFController a && ctrl == a.ctrl)
                   || (other is AnimatorController b && ctrl == b)
                   || (other == null && ctrl == null);
        }
        public override int GetHashCode() => Tuple.Create(ctrl).GetHashCode();

        public AnimatorController GetRaw() {
            return ctrl;
        }
        
        protected virtual string NewLayerName(string name) {
            return name;
        }
        
        public virtual VFLayer NewLayer(string name, int insertAt = -1) {
            name = NewLayerName(name);

            // Unity breaks if name contains .
            name = name.Replace(".", "");

            var sm = VrcfObjectFactory.Create<AnimatorStateMachine>();
            sm.name = name;
            var newLayer = new AnimatorControllerLayer {
                name = name,
                stateMachine = sm
            };
            ctrl.layers = ctrl.layers.Concat(new[] { newLayer }).ToArray();
            var layer = new VFLayer(ctrl, sm);
            if (insertAt >= 0) {
                layer.Move(insertAt);
            }
            layer.weight = 1;
            sm.anyStatePosition = VFState.CalculateOffsetPosition(sm.entryPosition, 0, 1);
            return layer;
        }
        
        /**
         * BEWARE: This consumes the ENTIRE asset file containing "other"
         * The animator controller (and its sub-assets) should be owned by vrcfury, and should
         * be the ONLY THING in that file!!!
         */
        public void TakeOwnershipOf(VFController other, bool putOnTop = false, bool prefix = true) {
            ctrl.WorkLog(
                $"Merged in {other.ctrl.layers.Length} layers and {other.parameters.Length} parameters from controller {other.ctrl.GetPathAndName()}"
            );
            // Merge Layers
            if (prefix) {
                foreach (var layer in other.layers) {
                    layer.name = NewLayerName(layer.name);
                }
            }

            if (putOnTop) {
                ctrl.layers = other.ctrl.layers.Concat(ctrl.layers).ToArray();
            } else {
                ctrl.layers = ctrl.layers.Concat(other.ctrl.layers).ToArray();
            }

            other.ctrl.layers = new AnimatorControllerLayer[] { };
            
            // Merge Params
            foreach (var p in other.parameters) {
                _NewParam(p.name, p.type, n => {
                    n.defaultBool = p.defaultBool;
                    n.defaultFloat = p.defaultFloat;
                    n.defaultInt = p.defaultInt;
                });
            }

            other.parameters = new AnimatorControllerParameter[] { };
        }

        public void RemoveParameter(int i) {
            ctrl.parameters = ctrl.parameters
                .Where((_, index) => index != i)
                .ToArray();
        }

        public VFABool _NewBool(string name, bool def = false) {
            var p = _NewParam(name, AnimatorControllerParameterType.Bool, param => param.defaultBool = def);
            return new VFABool(p.name, p.defaultBool);
        }
        public VFAFloat _NewFloat(string name, float def = 0) {
            var p = _NewParam(name, AnimatorControllerParameterType.Float, param => param.defaultFloat = def);
            return new VFAFloat(p.name, p.defaultFloat);
        }
        public VFAInteger _NewInt(string name, int def = 0) {
            var p = _NewParam(name, AnimatorControllerParameterType.Int, param => param.defaultInt = def);
            return new VFAInteger(p.name, p.defaultInt);
        }
        public AnimatorControllerParameter _NewParam(string name, AnimatorControllerParameterType type, Action<AnimatorControllerParameter> with = null) {
            var exists = GetParam(name);
            if (exists != null) {
                return exists;
            }
            var param = new AnimatorControllerParameter {
                name = name,
                type = type
            };
            with?.Invoke(param);
            ctrl.parameters = ctrl.parameters.Concat(new[] { param }).ToArray();
            return param;
        }

        public AnimatorControllerParameter GetParam(string name) {
            return Array.Find(ctrl.parameters, other => other.name == name);
        }
    
        public IList<VFLayer> GetLayers() {
            return ctrl.layers.Select(l => new VFLayer(ctrl, l.stateMachine)).ToArray();
        }
        public IList<VFLayer> layers => GetLayers();

        [CanBeNull]
        public VFLayer GetLayer(int index) {
            var ls = ctrl.layers;
            if (index < 0 || index >= ls.Length) return null;
            return new VFLayer(ctrl, ls[index].stateMachine);
        }

        public AnimatorControllerParameter[] parameters {
            get => ctrl.parameters;
            set => ctrl.parameters = value;
        }

        [CanBeNull]
        public static VFController CopyAndLoadController(RuntimeAnimatorController ctrl) {
            if (ctrl == null) {
                return null;
            }

            // Make a copy of everything
            ctrl = ctrl.Clone(addPrefix: $"Copied from {ctrl.name}/");

            // Collect any override controllers wrapping the main controller
            var overrides = new List<AnimatorOverrideController>();
            while (ctrl is AnimatorOverrideController ov) {
                overrides.Add(ov);
                ctrl = ov.runtimeAnimatorController;
            }

            // Bail if we hit a dead end
            if (!(ctrl is AnimatorController ac)) {
                return null;
            }
            
            var output = new VFController(ac);
            output.RemoveInvalidParameters();
            output.FixNullStateMachines();
            output.FixBadTransitions();
            output.RemoveBadBehaviours();
            output.ReplaceSyncedLayers();
            output.RemoveDuplicateStateMachines();

            // Apply override controllers
            if (overrides.Count > 0) {
                output.ReplaceClips(clip => {
                    return overrides
                        .Select(ov => ov[clip])
                        .Where(overrideClip => overrideClip != null)
                        .DefaultIfEmpty(clip)
                        .First();
                });
            }

            // Make sure all masks are unique, so we don't modify one and affect another
            foreach (var layer in output.GetLayers()) {
                if (layer.mask != null) {
                    layer.mask = layer.mask.Clone();
                }
            }
            
            output.FixLayer0Weight();
            output.RemoveWrongParamTypes();
            return output;
        }

        /**
         * Some people have corrupt controller layers containing no state machine.
         * The simplest fix for this is for us to just stuff an empty state machine into it.
         * We can't just delete it because it would interfere with the layer index numbers.
         */
        private void FixNullStateMachines() {
            ctrl.layers = ctrl.layers.Select(layer => {
                if (layer.stateMachine == null) {
                    var sm = VrcfObjectFactory.Create<AnimatorStateMachine>();
                    sm.name = layer.name;
                    layer.stateMachine = sm;
                }
                return layer;
            }).ToArray();
        }

        /**
         * Layer 0 is always treated as fully weighted by unity, regardless of its actually setting.
         * This can do weird things when we move that layer around, so we always ensure that the
         * setting of the base layer is properly set to 1.
         */
        private void FixLayer0Weight() {
            var layer0 = GetLayer(0);
            if (layer0 == null) return;
            layer0.weight = 1;
        }

        private void RemoveBadBehaviours() {
            foreach (var layer in layers) {
                layer.RemoveBadBehaviours();
            }
        }
        
        private void FixBadTransitions() {
            foreach (var layer in layers) {
                // layer.RewriteTransitions will automatically fix if the transitions array is null
                layer.RewriteTransitions(t => {
                    // Remove any transitions that are null
                    if (t == null) return null;
                    // Fix conditions arrays that are null
                    if (t.conditions == null) {
                        t.conditions = new AnimatorCondition[] { };
                    }
                    // Remove any transitions that are missing required conditions
                    if (t is AnimatorStateTransition st && !st.hasExitTime && !t.conditions.Any()) {
                        return null;
                    }
                    return t;
                });
            }
        }

        /**
         * Synced layers are... "very" broken in unity and in vrchat. In unity, if you delete a layer higher than the synced
         * layer, the synced layer is randomly deleted. In vrchat, synced layers don't really work properly at all.
         * When dealing with write defaults issues, synced layers break everything because the motions in the synced layer may need
         * a different WD setting than the states in the other layer.
         * We can "sorta" work around this issue by just replacing the synced layer with an identical copy. This means that timing sync won't work,
         * but it's better than nothing.
         */
        private void ReplaceSyncedLayers() {
            ctrl.layers = ctrl.layers.Select((layer, id) => {
                if (layer.syncedLayerIndex < 0 || layer.syncedLayerIndex == id) {
                    layer.syncedLayerIndex = -1;
                    return layer;
                }
                if (layer.syncedLayerIndex >= ctrl.layers.Length) {
                    var sm = VrcfObjectFactory.Create<AnimatorStateMachine>();
                    sm.name = layer.name;
                    layer.stateMachine = sm;
                    layer.syncedLayerIndex = -1;
                    return layer;
                }

                var copy = ctrl.layers[layer.syncedLayerIndex].stateMachine.Clone();
                layer.syncedLayerIndex = -1;
                layer.stateMachine = copy;
                foreach (var state in new AnimatorIterator.States().From(new VFLayer(ctrl, layer.stateMachine))) {
                    var originalState = state.GetCloneSource();
                    state.motion = layer.GetOverrideMotion(originalState);
                    state.behaviours = layer.GetOverrideBehaviours(originalState);
                    layer.SetOverrideMotion(originalState, null);
                    layer.SetOverrideBehaviours(originalState, Array.Empty<StateMachineBehaviour>());
                }

                return layer;
            }).ToArray();

        }

        /**
         * Some systems (modular avatar) can improperly add multiple layers with the same state machine.
         * This wrecks havoc, as making changes to one of the layers can impact both, while typically there is
         * expected to be no cross-talk. Since there's basically no legitimate reason for the same state machine
         * to be used more than once in the same controller, we can just nuke the copies.
         */
        private void RemoveDuplicateStateMachines() {
            var seenStateMachines = new HashSet<AnimatorStateMachine>();
            ctrl.layers = ctrl.layers.Select(layer => {
                if (layer.stateMachine != null) {
                    if (seenStateMachines.Contains(layer.stateMachine)) {
                        return null;
                    }
                    seenStateMachines.Add(layer.stateMachine);
                }
                return layer;
            }).NotNull().ToArray();
        }
        
        /**
         * Some tools add parameters with an invalid type (not bool, trigger, float, int, etc)
         * This causes the VRCSDK to blow up and break the mirror clone and throw exceptions in console.
         * https://feedback.vrchat.com/bug-reports/p/invalid-parameter-type-within-a-controller-breaks-mirror-clone-and-spams-output
         */
        private void RemoveInvalidParameters() {
            ctrl.parameters = ctrl.parameters.Where(p => VRCFEnumUtils.IsValid(p.type)).ToArray();
        }

        public static Action<VFLayer[],bool,Func<string,string>> onRewriteParameters;
        public void RewriteParameters(Func<string, string> rewriteParamNameNullUnsafe, bool includeWrites = true, ICollection<VFLayer> limitToLayers = null) {
            string RewriteParamName(string str) {
                if (string.IsNullOrEmpty(str)) return str;
                return rewriteParamNameNullUnsafe(str);
            }
            var affectsLayers = GetLayers()
                .Where(l => limitToLayers == null || limitToLayers.Contains(l))
                .ToArray();
            
            // Params
            if (includeWrites && limitToLayers == null) {
                var prms = ctrl.parameters;
                foreach (var p in prms) {
                    p.name = RewriteParamName(p.name);
                }

                ctrl.parameters = prms;
                ctrl.Dirty();
            }

            // States
            foreach (var state in new AnimatorIterator.States().From(affectsLayers)) {
                if (state.speedParameterActive) {
                    state.speedParameter = RewriteParamName(state.speedParameter);
                }
                if (state.cycleOffsetParameterActive) {
                    state.cycleOffsetParameter = RewriteParamName(state.cycleOffsetParameter);
                }
                if (state.mirrorParameterActive) {
                    state.mirrorParameter = RewriteParamName(state.mirrorParameter);
                }
                if (state.timeParameterActive) {
                    state.timeParameter = RewriteParamName(state.timeParameter);
                }
                state.Dirty();
            }

            onRewriteParameters?.Invoke(affectsLayers, includeWrites, RewriteParamName);

            // Parameter Animations
            if (includeWrites) {
                foreach (var clip in new AnimatorIterator.Clips().From(affectsLayers)) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.GetPropType() != EditorCurveBindingType.Aap) return binding;
                        binding.propertyName = RewriteParamName(binding.propertyName);
                        return binding;
                    }));
                }
            }

            // Blend trees
            foreach (var tree in new AnimatorIterator.Trees().From(affectsLayers)) {
                tree.RewriteParameters(RewriteParamName);
            }

            // Transitions
            foreach (var layer in affectsLayers) {
                layer.RewriteConditions(cond => {
                    cond.parameter = RewriteParamName(cond.parameter);
                    return cond;
                });
            }
        }

        public VFController Clone() {
            return new VFController(ctrl.Clone());
        }
        
        public void ReplaceClips(Func<AnimationClip, AnimationClip> replace) {
            Motion RewriteMotion(Motion motion) {
                if (motion is AnimationClip clip) {
                    return replace(clip);
                }
                if (motion is BlendTree tree) {
                    tree.RewriteChildren(child => {
                        child.motion = RewriteMotion(child.motion);
                        return child;
                    });
                    return tree;
                }
                return motion;
            }
            
            foreach (var state in layers.SelectMany(l => l.allStates)) {
                state.motion = RewriteMotion(state.motion);
            }
        }

        public void RemoveWrongParamTypes() {
            var badBool = new Lazy<string>(() => _NewBool("InvalidParam"));
            var badFloat = new Lazy<string>(() => _NewFloat("InvalidParamFloat"));
            var badThreshold = new Lazy<string>(() => _NewBool("BadIntThreshold", def: true));
            AnimatorCondition InvalidCondition() => new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = badBool.Value,
            };
            AnimatorCondition BadThresholdCondition() => new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = badThreshold.Value,
            };

            var paramTypes = parameters
                .ToImmutableDictionary(p => p.name, p => p.type);
            foreach (var layer in GetLayers()) {
                layer.RewriteConditions(condition => {
                    var mode = condition.mode;

                    if (!paramTypes.TryGetValue(condition.parameter, out var type)) {
                        return InvalidCondition();
                    }

                    if (type == AnimatorControllerParameterType.Bool || type == AnimatorControllerParameterType.Trigger) {
                        // When you use a bool with an incorrect mode, the editor just always says "True",
                        // so let's just actually make it do that instead of converting it to InvalidParamType
                        if (mode != AnimatorConditionMode.If && mode != AnimatorConditionMode.IfNot) {
                            condition.mode = AnimatorConditionMode.If;
                            return condition;
                        }
                    } else if (type == AnimatorControllerParameterType.Int) {
                        if (mode != AnimatorConditionMode.Equals
                            && mode != AnimatorConditionMode.NotEqual
                            && mode != AnimatorConditionMode.Greater
                            && mode != AnimatorConditionMode.Less) {
                            return InvalidCondition();
                        }

                        // When you use an int with a float threshold, the editor shows the floor value,
                        // but evaluates the condition using the original value. Let's fix that so the editor
                        // value is actually the one that is used.
                        var floored = (int)Math.Floor(condition.threshold);
                        if (condition.threshold != floored) {
                            condition.threshold = floored;
                            return AnimatorTransitionBaseExtensions.Rewritten.And(
                                condition,
                                BadThresholdCondition()
                            );
                        }
                    } else if (type == AnimatorControllerParameterType.Float) {
                        if (mode != AnimatorConditionMode.Greater && mode != AnimatorConditionMode.Less) {
                            return InvalidCondition();
                        }
                    }

                    return condition;
                });
            }

            bool Exists(string p) =>
                p != null && paramTypes.ContainsKey(p);
            bool IsFloat(string p) =>
                p != null && paramTypes.TryGetValue(p, out var type) && type == AnimatorControllerParameterType.Float;

            // Bad tree weights are weird.
            // * If the parameter doesn't exist, the value is always 0
            // * Otherwise, if the parameter is not a float, it uses the first float in the controller
            //   If there is no other float, the value is always 0
            foreach (var tree in new AnimatorIterator.Trees().From(this)) {
                tree.RewriteParameters(p => {
                    if (paramTypes.TryGetValue(p, out var type)) {
                        if (type == AnimatorControllerParameterType.Float) {
                            // It's valid
                            return p;
                        } else {
                            // It exists but isn't a float, use the first float in the controller
                            var firstFloat = parameters
                                .FirstOrDefault(pr => pr.type == AnimatorControllerParameterType.Float);
                            if (firstFloat != null) {
                                return firstFloat.name;
                            } else {
                                return badFloat.Value;
                            }
                        }
                    } else {
                        // It doesn't exist
                        return badFloat.Value;
                    }
                });
            }

            // Fix bad state fields
            // Unity treats bad state fields very strangely.
            // * If the parameter doesn't exist, it's as if the checkbox isn't even checked
            // * Otherwise, if the parameter is the wrong type, its value gets used anyways
            foreach (var state in new AnimatorIterator.States().From(this)) {
                if (state.mirrorParameterActive && !Exists(state.mirrorParameter)) {
                    state.mirrorParameterActive = false;
                }
                if (state.speedParameterActive && !Exists(state.speedParameter)) {
                    state.speedParameterActive = false;
                }
                if (state.timeParameterActive && !Exists(state.timeParameter)) {
                    state.timeParameterActive = false;
                }
                if (state.cycleOffsetParameterActive && !Exists(state.cycleOffsetParameter)) {
                    state.cycleOffsetParameterActive = false;
                }
            }

            Rewrite(AnimationRewriter.RewriteBinding(binding => {
                if (binding.GetPropType() == EditorCurveBindingType.Aap && !IsFloat(binding.propertyName)) {
                    return null;
                }
                return binding;
            }));
        }

        /**
         * "Upgrades" all parameters to the highest "type" needed for all usages, then makes all usages
         * work properly.
         *
         * For instance, if a parameter is used in both If and as a direct blendtree parameter,
         * it will be set to type Float, and the If will be converted to Greater than 0.
         */
        public void UpgradeWrongParamTypes() {
            // Figure out what types each param needs to be (at least)
            var paramTypes = new Dictionary<string, AnimatorControllerParameterType>();
            void UpgradeType(string name, AnimatorControllerParameterType newType) {
                if (!paramTypes.TryGetValue(name, out var type)) type = newType;
                else if (newType == AnimatorControllerParameterType.Float) type = newType;
                else if (newType == AnimatorControllerParameterType.Int && (type == AnimatorControllerParameterType.Bool || type == AnimatorControllerParameterType.Trigger)) type = newType;
                else if (newType == AnimatorControllerParameterType.Bool && type == AnimatorControllerParameterType.Trigger) type = newType;
                paramTypes[name] = type;
            }
            foreach (var p in parameters) {
                UpgradeType(p.name, p.type);
            }
            foreach (var condition in layers.SelectMany(layer => layer.allTransitions).SelectMany(transition => transition.conditions)) {
                var mode = condition.mode;
                if (mode == AnimatorConditionMode.Equals || mode == AnimatorConditionMode.NotEqual) {
                    UpgradeType(condition.parameter, AnimatorControllerParameterType.Int);
                }
                if (mode == AnimatorConditionMode.Greater || mode == AnimatorConditionMode.Less) {
                    if (condition.threshold % 1 == 0) {
                        UpgradeType(condition.parameter, AnimatorControllerParameterType.Int);
                    } else {
                        UpgradeType(condition.parameter, AnimatorControllerParameterType.Float);
                    }
                }
            }
            foreach (var tree in new AnimatorIterator.Trees().From(this)) {
                tree.RewriteParameters(p => {
                    UpgradeType(p, AnimatorControllerParameterType.Float);
                    return p;
                });
            }
            foreach (var state in new AnimatorIterator.States().From(this)) {
                if (state.speedParameterActive)
                    UpgradeType(state.speedParameter, AnimatorControllerParameterType.Float);
                if (state.timeParameterActive)
                    UpgradeType(state.timeParameter, AnimatorControllerParameterType.Float);
                if (state.cycleOffsetParameterActive)
                    UpgradeType(state.cycleOffsetParameter, AnimatorControllerParameterType.Float);
            }
            foreach (var clip in new AnimatorIterator.Clips().From(this)) {
                foreach (var binding in clip.GetFloatBindings()) {
                    if (binding.GetPropType() == EditorCurveBindingType.Aap) {
                        UpgradeType(binding.propertyName, AnimatorControllerParameterType.Float);
                    }
                }
            }

            // Change the param types
            parameters = parameters.Select(p => {
                if (paramTypes.TryGetValue(p.name, out var type)) {
                    var oldDefault = p.GetDefaultValueAsFloat();
                    p.type = type;
                    p.defaultBool = oldDefault > 0;
                    p.defaultInt = (int)Math.Round(oldDefault);
                    p.defaultFloat = oldDefault;
                }
                return p;
            }).ToArray();

            // Fix all of the usages
            foreach (var layer in GetLayers()) {
                layer.RewriteConditions(c => {
                    if (!paramTypes.TryGetValue(c.parameter, out var type)) {
                        return c;
                    }
                    if (type == AnimatorControllerParameterType.Int || type == AnimatorControllerParameterType.Float) {
                        if (c.mode == AnimatorConditionMode.If) {
                            c.mode = AnimatorConditionMode.NotEqual;
                            c.threshold = 0;
                        }
                        if (c.mode == AnimatorConditionMode.IfNot) {
                            c.mode = AnimatorConditionMode.Equals;
                            c.threshold = 0;
                        }
                    }
                    if (type == AnimatorControllerParameterType.Float) {
                        if (c.mode == AnimatorConditionMode.Equals) {
                            return AnimatorTransitionBaseExtensions.Rewritten.And(
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Greater, threshold = c.threshold - 0.001f },
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Less, threshold = c.threshold + 0.001f }
                            );
                        }
                        if (c.mode == AnimatorConditionMode.NotEqual) {
                            return AnimatorTransitionBaseExtensions.Rewritten.Or(
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Less, threshold = c.threshold - 0.001f },
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Greater, threshold = c.threshold + 0.001f }
                            );
                        }
                    }
                    return c;
                });
            }
        }

        public static Action<VFController,AnimationRewriter> onRewriteClips;
        public void Rewrite(AnimationRewriter rewriter) {
            // Rewrite clips
            foreach (var clip in new AnimatorIterator.Clips().From(this)) {
                clip.Rewrite(rewriter);
            }

            // Rewrite masks
            foreach (var layer in layers) {
                var mask = layer.mask;
                if (mask == null || mask.transformCount == 0) continue;
                mask.SetTransforms(mask.GetTransforms()
                    .Select(rewriter.RewritePath)
                    .Where(path => path != null));
            }

            onRewriteClips?.Invoke(this,rewriter);
        }
    }
}
