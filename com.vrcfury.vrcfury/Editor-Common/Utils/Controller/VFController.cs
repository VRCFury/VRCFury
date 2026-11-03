using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Utils;

namespace VF.Utils.Controller {
    internal class VFController {
        private string _name;
        private UnityEngine.Object sourceAsset;
        private readonly List<string> workLog = new List<string>();
        private readonly Dictionary<string, VFAParam> parametersByName = new Dictionary<string, VFAParam>();
        private List<VFLayer> _layers;

        public static VFController Create(string name = null) {
            return new VFController {
                _name = name ?? "New Animator Controller",
                sourceAsset = null,
                _layers = new List<VFLayer>()
            };
        }

        private VFController(
            AnimatorController ctrl,
            VFLoadContext context
        ) : this() {
            InitFromRaw(ctrl, context);
        }

        protected VFController(VFController source) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _name = source._name;
            sourceAsset = source.sourceAsset;
            workLog.AddRange(source.workLog);
            SetParameters(source.parameters.Select(p => p.Clone(this)));
            var context = new VFCloneContext();
            _layers = source._layers.Select(layer => layer?.Clone(this, context)).ToList();
        }

        private void InitFromRaw(AnimatorController ctrl, VFLoadContext context) {
            if (ctrl == null) throw new ArgumentNullException(nameof(ctrl));
            _name = ctrl.name;
            sourceAsset = ctrl;
            // Ignore invalid declarations before resolving duplicate names; the first valid entry wins.
            SetParameters(ctrl.parameters.Where(p => p.name != null && VRCFEnumUtils.IsValid(p.type))
                .Select(VFAParam.Load));
            var rawLayers = ctrl.layers;
            _layers = Enumerable.Repeat<VFLayer>(null, rawLayers.Length).ToList();
            var loading = new HashSet<int>();
            for (var i = 0; i < rawLayers.Length; i++) {
                _layers[i] = LoadLayer(rawLayers, i, context, loading);
            }
        }

        private VFLayer LoadLayer(
            AnimatorControllerLayer[] rawLayers,
            int layerIndex,
            VFLoadContext context,
            ISet<int> loading
        ) {
            var existing = _layers[layerIndex];
            if (existing != null) {
                return existing;
            }

            var rawLayer = rawLayers[layerIndex];
            if (rawLayer == null) {
                return null;
            }

            if (!loading.Add(layerIndex)) {
                var layer = VFLayer.Load(this, rawLayer, context);
                layer.syncedLayerIndex = -1;
                return layer;
            }

            try {
                var syncedLayerIndex = rawLayer.syncedLayerIndex;
                if (syncedLayerIndex >= 0 && syncedLayerIndex != layerIndex && syncedLayerIndex < rawLayers.Length) {
                    return _layers[layerIndex] = LoadSyncedLayer(rawLayers, layerIndex, context, loading);
                }

                var clearSyncedLayerIndex = syncedLayerIndex == layerIndex;
                if (syncedLayerIndex >= rawLayers.Length) {
                    var layer = VFLayer.LoadWithoutStateMachine(this, rawLayer, context);
                    layer.syncedLayerIndex = -1;
                    layer.ReplaceStateMachine(VFStateMachine.Create(layer, layer.name));
                    return _layers[layerIndex] = layer;
                }

                var unsyncedLayer = VFLayer.Load(this, rawLayer, context);
                if (clearSyncedLayerIndex) {
                    unsyncedLayer.syncedLayerIndex = -1;
                }
                return _layers[layerIndex] = unsyncedLayer;
            } finally {
                loading.Remove(layerIndex);
            }
        }

        private VFLayer LoadSyncedLayer(
            AnimatorControllerLayer[] rawLayers,
            int layerIndex,
            VFLoadContext context,
            ISet<int> loading
        ) {
            var rawLayer = rawLayers[layerIndex];
            var sourceLayer = LoadLayer(rawLayers, rawLayer.syncedLayerIndex, context, loading);
            var syncedLayer = VFLayer.LoadWithoutStateMachine(this, rawLayer, context);
            syncedLayer.syncedLayerIndex = -1;
            if (sourceLayer?.stateMachine == null) {
                syncedLayer.ReplaceStateMachine(VFStateMachine.Create(syncedLayer, syncedLayer.name));
                return syncedLayer;
            }

            var clone = new VFCloneContext();
            var clonedStateMachine = sourceLayer.stateMachine.Clone(syncedLayer, clone);
            syncedLayer.ReplaceStateMachine(clonedStateMachine);
            ApplySyncedLayerOverrides(sourceLayer, clone.States, rawLayer, context);
            return syncedLayer;
        }


        private static void ApplySyncedLayerOverrides(
            VFLayer sourceLayer,
            IReadOnlyDictionary<VFState, VFState> clonedStates,
            AnimatorControllerLayer rawLayer,
            VFLoadContext context
        ) {
            if (rawLayer == null || clonedStates == null) {
                return;
            }

            foreach (var sourceState in sourceLayer.allStates) {
                var rawState = sourceState.GetSourceAsset();
                if (rawState == null) continue;
                var wrappedState = clonedStates.GetOrDefault(sourceState);
                if (wrappedState == null) continue;

                var overrideMotion = rawLayer.GetOverrideMotion(rawState);
                wrappedState.motion = VFMotion.Load(overrideMotion, context);

                var overrideBehaviours = VFBehaviourContainer.GetRawOverrideBehaviours(rawLayer, rawState);
                wrappedState.behaviours.ReplaceWith(overrideBehaviours.Select(behaviour => VFBehaviour.Load(behaviour, context)));
            }
        }

        private VFController() {
        }

        //public static implicit operator VFController(AnimatorController d) => VFController.Load(d);
        //public static implicit operator bool(VFController d) => d != null;
        public static bool operator ==(VFController a, VFController b) => a?.Equals(b) ?? b?.Equals(null) ?? true;
        public static bool operator !=(VFController a, VFController b) => !(a == b);
        public override bool Equals(object other) {
            return other is VFController a && ReferenceEquals(this, a);
        }
        public override int GetHashCode() => base.GetHashCode();

        public string name {
            get => _name;
            set => _name = value;
        }

        public UnityEngine.Object GetSourceAsset() {
            return sourceAsset;
        }

        public void WorkLog(string item) {
            if (string.IsNullOrEmpty(item)) return;
            workLog.Add(item);
        }

        internal void ReplaceLayers(IEnumerable<VFLayer> layers) {
            _layers = layers.ToList();
        }

        internal int GetLayerId(VFLayer layer) {
            return _layers.IndexOf(layer);
        }

        protected virtual string NewLayerName(string name) {
            return name;
        }

        public virtual VFLayer NewLayer(string name, int insertAt = -1) {
            name = NewLayerName(name);

            // Unity breaks if name contains .
            name = name.Replace(".", "");

            var layer = VFLayer.Create(this, name);
            _layers.Add(layer);
            if (insertAt >= 0) {
                layer.Move(insertAt);
            }
            layer.weight = 1;
            return layer;
        }

        /**
         * BEWARE: This consumes the ENTIRE asset file containing "other"
         * The animator controller (and its sub-assets) should be owned by vrcfury, and should
         * be the ONLY THING in that file!!!
         */
        public void TakeOwnershipOf(VFController other, bool putOnTop = false, bool prefix = true) {
            WorkLog(
                $"Merged in {other.GetLayers().Count} layers and {other.parameters.Length} parameters from controller {(other.GetSourceAsset()?.GetPathAndName() ?? "(generated)")}"
            );
            var movedLayers = other._layers.ToList();

            // Merge Layers
            if (prefix) {
                foreach (var layer in movedLayers) {
                    layer.name = NewLayerName(layer.name);
                }
            }
            foreach (var layer in movedLayers) {
                layer.ReassignController(this);
            }

            if (putOnTop) {
                ReplaceLayers(movedLayers.Concat(_layers));
            } else {
                ReplaceLayers(_layers.Concat(movedLayers));
            }

            other._layers.Clear();

            // Merge Params. Ownership moves with each parameter; existing names keep their defaults.
            foreach (var parameter in other.parameters) {
                if (TryGetParam(parameter.name, out _)) continue;
                GetOrCreateParam(parameter.name, parameter.type, () => parameter);
            }

            other.parametersByName.Clear();
        }

        public void RemoveParameter(string name) {
            parametersByName.Remove(name);
        }

        public VFAParam GetOrCreateFloat(string name, FloatOrBool def = null) =>
            GetOrCreateParam(name, AnimatorControllerParameterType.Float, def);
        public VFAParam GetOrCreateInt(string name, FloatOrBool def = null) =>
            GetOrCreateParam(name, AnimatorControllerParameterType.Int, def);
        public VFAParam GetOrCreateBool(string name, FloatOrBool def = null) =>
            GetOrCreateParam(name, AnimatorControllerParameterType.Bool, def);

        private VFAParam GetOrCreateParam(string name, AnimatorControllerParameterType type, FloatOrBool def = null) {
            return GetOrCreateParam(name, type, () => {
                var parameter = new VFAParam(name, type);
                parameter.SetDefault(def);
                return parameter;
            });
        }

        // Existing defaults are preserved; requested types establish the minimum storage type.
        internal VFAParam GetOrCreateParam(string name, AnimatorControllerParameterType type, Func<VFAParam> create) {
            if (!TryGetParam(name, out var parameter)) {
                parameter = create();
                parameter.Controller = this;
                parametersByName.Add(name, parameter);
            }
            parameter.UpgradeType(type);
            return parameter;
        }

        public bool TryGetParam(string name, out VFAParam parameter) {
            return parametersByName.TryGetValue(name, out parameter);
        }

        public IList<VFLayer> GetLayers() {
            return _layers.ToArray();
        }
        public IList<VFLayer> layers => GetLayers();

        [CanBeNull]
        public VFLayer GetLayer(int index) {
            return _layers.GetOrDefault(index);
        }

        public VFAParam[] parameters => parametersByName.Values.ToArray();

        public void SortParameters() => SetParameters(parameters.OrderBy(p => p.name));

        private void SetParameters(IEnumerable<VFAParam> parameters) {
            var entries = parameters.GroupBy(p => p.name).Select(group => group.First()).ToArray();
            parametersByName.Clear();
            foreach (var parameter in entries) {
                GetOrCreateParam(parameter.name, parameter.type, () => parameter);
            }
        }

        [CanBeNull]
        public static VFController Load(
            RuntimeAnimatorController ctrl,
            VFLoadContext context
        ) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (ctrl == null) return null;

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

            var previousRewriteMotion = context.RewriteMotion;
            context.RewriteMotion = motion => {
                var current = previousRewriteMotion?.Invoke(motion) ?? motion;
                foreach (var overrideController in overrides) {
                    if (current == null) return null;
                    if (current is AnimationClip clip) {
                        current = overrideController[clip] ?? current;
                    }
                }
                return current;
            };

            var output = new VFController(ac, context);
            output.FixNullStateMachines();
            output.FixBadTransitions();

            output.RemoveDuplicateStateMachines();

            output.FixLayer0Weight();
            var firstFloat = ac.parameters.FirstOrDefault(p => p.name != null && p.type == AnimatorControllerParameterType.Float)?.name;
            output.RemoveWrongParamTypes(firstFloat);
            return output;
        }

        public AnimatorController Save(
            VFGameObject bindingRoot,
            SaveAssetsSession saveSession,
            bool reuseSourceAssets = true
        ) {
            if (bindingRoot == null) throw new ArgumentNullException(nameof(bindingRoot));
            var context = new VFSaveContext(bindingRoot, reuseSourceAssets);

            var finalizedRaw = new AnimatorController();
            finalizedRaw.name = _name;
            finalizedRaw.parameters = parameters.Select(p => p.Save()).ToArray();
            var savedLayers = GetLayers().Select(layer => layer.Save(context)).ToArray();
            finalizedRaw.layers = savedLayers;
            finalizedRaw = VrcfObjectFactory.Register(finalizedRaw);
            foreach (var item in workLog) {
                finalizedRaw.WorkLog(item);
            }
            saveSession.SaveControllerAndChildren(
                finalizedRaw,
                context.NewAssets,
                context.OtherAssets
            );
            return finalizedRaw;
        }

        /**
         * Some people have corrupt controller layers containing no state machine.
         * The simplest fix for this is for us to just stuff an empty state machine into it.
         * We can't just delete it because it would interfere with the layer index numbers.
         */
        private void FixNullStateMachines() {
            ReplaceLayers(_layers.Select(layer => {
                if (layer.stateMachine == null) {
                    layer.stateMachine = VFStateMachine.Create(layer, layer.name);
                }
                return layer;
            }));
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
                    // Remove non-exit transitions that no longer point anywhere.
                    if (!t.isExit && t.destinationState == null && t.destinationStateMachine == null) {
                        return null;
                    }
                    // Remove any transitions that are missing required conditions
                    if (t is VFTransition st && !st.hasExitTime && !t.conditions.Any()) {
                        return null;
                    }
                    return t;
                });
            }
        }

        /**
         * Some systems (modular avatar) can improperly add multiple layers with the same state machine.
         * This wrecks havoc, as making changes to one of the layers can impact both, while typically there is
         * expected to be no cross-talk. Since there's basically no legitimate reason for the same state machine
         * to be used more than once in the same controller, we can just nuke the copies.
         */
        private void RemoveDuplicateStateMachines() {
            var seenStateMachines = new HashSet<AnimatorStateMachine>();
            ReplaceLayers(_layers.Select(layer => {
                var source = layer.stateMachine?.GetSourceAsset();
                if (source != null) {
                    if (seenStateMachines.Contains(source)) {
                        return null;
                    }
                    seenStateMachines.Add(source);
                }
                return layer;
            }).NotNull());
        }

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
                SetParameters(parameters.Select(p => p.WithName(RewriteParamName(p.name))));
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
            }

            foreach (var behaviour in affectsLayers.SelectMany(layer => layer.allBehaviours)) {
                behaviour.RewriteParameters(RewriteParamName, includeWrites);
            }

            // Parameter Animations
            if (includeWrites) {
                foreach (var clip in GetClips(affectsLayers)) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.GetPropType() != EditorCurveBindingType.Aap) return binding;
                        return binding.WithPropertyName(RewriteParamName(binding.propertyName));
                    }));
                }
            }

            // Blend trees
            foreach (var tree in GetTrees(affectsLayers)) {
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

        public virtual VFController Clone() {
            return new VFController(this);
        }

        private IEnumerable<VFMotion> GetMotions(IEnumerable<VFLayer> layerList = null) {
            return (layerList ?? layers)
                .SelectMany(layer => layer.allStates)
                .Select(state => state.motion)
                .Where(motion => motion != null);
        }

        public IEnumerable<VFClip> GetClips(IEnumerable<VFLayer> layerList = null) {
            return GetMotions(layerList)
                .SelectMany(motion => new AnimatorIterator.Clips().From(motion))
                .Distinct();
        }

        public IEnumerable<VFTree> GetTrees(IEnumerable<VFLayer> layerList = null) {
            return GetMotions(layerList)
                .SelectMany(motion => new AnimatorIterator.Trees().From(motion))
                .Distinct();
        }

        public void RemoveWrongParamTypes([CanBeNull] string firstFloat) {
            var invalidParam = new Lazy<string>(() => GetOrCreateFloat("InvalidParam".MakeUnique(parameters.Select(p => p.name))));
            var badThreshold = new Lazy<string>(() => GetOrCreateFloat("BadIntThreshold".MakeUnique(parameters.Select(p => p.name)), def: 1));
            AnimatorCondition InvalidCondition() => new AnimatorCondition {
                mode = AnimatorConditionMode.Greater,
                parameter = invalidParam.Value,
                threshold = 0,
            };
            AnimatorCondition BadThresholdCondition() => new AnimatorCondition {
                mode = AnimatorConditionMode.Greater,
                parameter = badThreshold.Value,
                threshold = 0,
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
                            return AnimatorConditionExtensions.Rewritten.And(
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
            foreach (var tree in GetTrees()) {
                tree.RewriteParameters(p => {
                    if (paramTypes.TryGetValue(p, out var type)) {
                        if (type == AnimatorControllerParameterType.Float) {
                            // It's valid
                            return p;
                        } else {
                            // It exists but isn't a float, use the first float in the controller
                            return firstFloat ?? invalidParam.Value;
                        }
                    } else {
                        // It doesn't exist
                        return invalidParam.Value;
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
         * Upgrade storage types from existing definitions and usages.
         * Float usages take precedence over int, bool and trigger requirements.
         */
        private Dictionary<string, AnimatorControllerParameterType> GetRequiredParameterTypes() {
            // Figure out what types each param needs to be (at least)
            var paramTypes = new Dictionary<string, AnimatorControllerParameterType>();
            void UpgradeType(string name, AnimatorControllerParameterType newType) {
                paramTypes[name] = paramTypes.TryGetValue(name, out var existing)
                    ? CombineParameterTypes(existing, newType) : newType;
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
            foreach (var tree in GetTrees()) {
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
            foreach (var clip in GetClips()) {
                foreach (var binding in clip.GetFloatBindings()) {
                    if (binding.GetPropType() == EditorCurveBindingType.Aap) {
                        UpgradeType(binding.propertyName, AnimatorControllerParameterType.Float);
                    }
                }
            }

            return paramTypes;
        }

        internal static AnimatorControllerParameterType CombineParameterTypes(
            AnimatorControllerParameterType a, AnimatorControllerParameterType b) {
            if (a == AnimatorControllerParameterType.Float || b == AnimatorControllerParameterType.Float) return AnimatorControllerParameterType.Float;
            if (a == AnimatorControllerParameterType.Int || b == AnimatorControllerParameterType.Int) return AnimatorControllerParameterType.Int;
            if (a == AnimatorControllerParameterType.Bool || b == AnimatorControllerParameterType.Bool) return AnimatorControllerParameterType.Bool;
            return AnimatorControllerParameterType.Trigger;
        }

        public void UpgradeWrongParamTypes() {
            var paramTypes = GetRequiredParameterTypes();
            // Change the param types
            foreach (var p in parameters) {
                if (paramTypes.TryGetValue(p.name, out var type)) {
                    p.UpgradeType(type);
                }
            }

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
                            return AnimatorConditionExtensions.Rewritten.And(
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Greater, threshold = c.threshold - 0.001f },
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Less, threshold = c.threshold + 0.001f }
                            );
                        }
                        if (c.mode == AnimatorConditionMode.NotEqual) {
                            return AnimatorConditionExtensions.Rewritten.Or(
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
            foreach (var clip in GetClips()) {
                clip.Rewrite(rewriter);
            }

            onRewriteClips?.Invoke(this,rewriter);
        }
    }
}
