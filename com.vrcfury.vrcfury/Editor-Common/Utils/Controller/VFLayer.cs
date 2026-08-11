using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Utils;

namespace VF.Utils.Controller {
    internal class VFLayer : VFPrettyNamed {
        private VFController controller;
        private VFMask maskValue;
        private VFStateMachine stateMachineValue;

        private Vector2 nextOffset = new Vector2(1, 0);
        private VFState lastCreatedState;

        private static readonly HashSet<VFState> createdStates = new HashSet<VFState>();

        [VFInit]
        private static void ClearCreatedStates() {
            EditorApplication.update += () => createdStates.Clear();
        }

        private VFLayer(VFController controller) {
            this.controller = controller;
            syncedLayerIndex = -1;
        }

        internal static VFLayer Load(
            VFController controller,
            AnimatorControllerLayer rawLayer,
            VFLoadContext context
        ) {
            var layer = LoadWithoutStateMachine(controller, rawLayer, context);
            layer.stateMachineValue = VFStateMachine.Load(
                layer,
                rawLayer.stateMachine,
                context
            );
            return layer;
        }

        internal static VFLayer LoadWithoutStateMachine(
            VFController controller,
            AnimatorControllerLayer rawLayer,
            VFLoadContext context
        ) {
            var layer = new VFLayer(controller);
            layer.name = rawLayer.name;
            layer.weight = rawLayer.defaultWeight;
            layer.blendingMode = rawLayer.blendingMode;
            layer.iKPass = rawLayer.iKPass;
            layer.syncedLayerAffectsTiming = rawLayer.syncedLayerAffectsTiming;
            layer.syncedLayerIndex = rawLayer.syncedLayerIndex;
            layer.maskValue = rawLayer.avatarMask != null
                ? VFMask.Load(rawLayer.avatarMask, context)
                : null;
            return layer;
        }

        public static VFLayer Create(VFController controller, string name) {
            var layer = new VFLayer(controller) {
                name = name,
                weight = 1
            };
            layer.stateMachineValue = VFStateMachine.Create(layer, name);
            layer.stateMachineValue.anyStatePosition = VFState.CalculateOffsetPosition(layer.stateMachineValue.entryPosition, 0, 1);
            return layer;
        }

        public VFLayer Clone(VFController newController, VFCloneContext context) {
            var output = new VFLayer(newController) {
                name = name,
                weight = weight,
                blendingMode = blendingMode,
                iKPass = iKPass,
                syncedLayerAffectsTiming = syncedLayerAffectsTiming,
                syncedLayerIndex = syncedLayerIndex,
                maskValue = maskValue?.Clone()
            };
            if (stateMachineValue != null) {
                output.stateMachineValue = stateMachineValue.Clone(output, context);
            }
            output.nextOffset = nextOffset;
            output.lastCreatedState = lastCreatedState != null ? context.States.GetOrDefault(lastCreatedState) : null;
            return output;
        }

        internal void ReassignController(VFController newController) {
            controller = newController ?? throw new ArgumentNullException(nameof(newController));
        }

        public static bool operator ==(VFLayer a, VFLayer b) => ReferenceEquals(a, b);
        public static bool operator !=(VFLayer a, VFLayer b) => !(a == b);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => base.GetHashCode();

        internal AnimatorControllerLayer Save(VFSaveContext context) {
            AvatarMask savedMask = null;
            if (maskValue != null) {
                savedMask = maskValue.Save(context);
            }
            return new AnimatorControllerLayer {
                name = name,
                avatarMask = savedMask,
                blendingMode = blendingMode,
                defaultWeight = weight,
                iKPass = iKPass,
                syncedLayerAffectsTiming = syncedLayerAffectsTiming,
                syncedLayerIndex = syncedLayerIndex,
                stateMachine = stateMachineValue?.Save(context)
            };
        }

        public VFStateMachine stateMachine {
            get => stateMachineValue;
            internal set => stateMachineValue = value;
        }

        public int syncedLayerIndex { get; internal set; }

        public bool Exists() {
            return TryGetLayerId(out _);
        }

        public int GetLayerId() {
            if (!TryGetLayerId(out var id)) {
                throw new Exception("Layer not found in controller. It may have been accessed after it was removed.");
            }
            return id;
        }

        public bool TryGetLayerId(out int id) {
            id = controller.GetLayerId(this);
            return id != -1;
        }

        public float weight { get; set; }
        public string name { get; set; }
        public bool iKPass { get; set; }
        public bool syncedLayerAffectsTiming { get; set; }
        public string prettyName => $"Controller `{controller.name}` Layer `{name}`";
        public AnimatorLayerBlendingMode blendingMode { get; set; }

        public VFMask mask {
            get => maskValue;
            set => maskValue = value;
        }

        private static string WrapStateName(string name, int attemptWrapAt = 35) {
            var lines = new List<string>();
            var currentLine = "";
            foreach (var c in name) {
                if (c == '\n' || (char.IsWhiteSpace(c) && currentLine.Length > attemptWrapAt)) {
                    lines.Add(currentLine);
                    currentLine = "";
                    continue;
                }
                if (char.IsWhiteSpace(c) && currentLine.Length == 0) {
                    continue;
                }
                currentLine += c;
            }
            if (!string.IsNullOrWhiteSpace(currentLine)) lines.Add(currentLine);
            return lines.Join('\n');
        }

        public void SetNextOffset(float x, float y) {
            nextOffset = new Vector2(x, y);
        }

        public VFState NewState(string name) {
            name = WrapStateName(name).Replace(".", "");
            var state = VFState.Create(this, stateMachineValue, name);
            stateMachineValue.AddState(state);

            if (lastCreatedState != null) {
                state.Move(lastCreatedState, nextOffset.x, nextOffset.y);
            } else {
                state.Move(stateMachineValue.entryPosition, nextOffset.x, nextOffset.y);
            }

            SetNextOffset(0, 1);
            lastCreatedState = state;
            createdStates.Add(state);
            return state;
        }

        public static bool Created(VFState state) {
            return createdStates.Contains(state);
        }

        public void Move(int newIndex) {
            var newList = controller.GetLayers()
                .Where(l => l != this)
                .ToList();
            newList.Insert(newIndex, this);
            controller.ReplaceLayers(newList);
        }

        public void Remove() {
            var layerId = GetLayerId();
            controller.ReplaceLayers(controller.GetLayers()
                .Where((_, index) => index != layerId));
        }

        public IEnumerable<VFStateMachine> allStateMachines =>
            stateMachineValue == null
                ? Enumerable.Empty<VFStateMachine>()
                : stateMachineValue.GetAllStateMachines();

        public bool hasSubMachines => allStateMachines.Skip(1).Any();
        public bool hasDefaultState => stateMachineValue?.defaultState != null;
        public VFState defaultState => stateMachineValue?.defaultState;
        public VFEntryTransition[] entryTransitions => stateMachineValue?.entryTransitions.ToArray() ?? Array.Empty<VFEntryTransition>();
        public Vector3 entryPosition => stateMachineValue?.entryPosition ?? Vector3.zero;

        public IEnumerable<VFState> allStates => allStateMachines
            .SelectMany(sm => sm.states);

        private IEnumerable<VFBehaviourContainer> allBehaviourContainers => allStateMachines
            .Select(sm => sm.behaviours)
            .Concat(allStates.Select(state => state.behaviours));

        public IEnumerable<VFBehaviour> allBehaviours => allBehaviourContainers
            .SelectMany(container => container);

        public IEnumerable<T> GetBehaviours<T>() where T : StateMachineBehaviour {
            return allBehaviourContainers
                .SelectMany(container => container.GetBehaviours<T>());
        }

        public bool HasBehaviours() {
            return allBehaviours.Any();
        }

        public bool HasBehaviour<T>() where T : StateMachineBehaviour {
            return allBehaviourContainers.Any(container => container.HasBehaviour<T>());
        }

        public void RewriteBehaviours(Func<StateMachineBehaviour, OneOrMany<StateMachineBehaviour>> action) {
            foreach (var container in allBehaviourContainers) {
                container.ReplaceWith(container.SelectMany(behaviour => behaviour.RewriteRaw<StateMachineBehaviour>(action)).ToArray());
            }
        }

        public void RewriteBehaviours<T>(Func<T, OneOrMany<StateMachineBehaviour>> action) where T : StateMachineBehaviour {
            foreach (var container in allBehaviourContainers) {
                container.ReplaceWith(container.SelectMany(behaviour => behaviour.RewriteRaw(action)).ToArray());
            }
        }

        public void RewriteBehaviours<T>(Func<VFBehaviour, T, OneOrMany<VFBehaviour>> action) where T : StateMachineBehaviour {
            foreach (var container in allBehaviourContainers) {
                container.ReplaceWith(container.SelectMany(behaviour => behaviour.Rewrite(action)).ToArray());
            }
        }

        public void RewriteConditions(Func<AnimatorCondition, AnimatorConditionExtensions.Rewritten> action) {
            RewriteTransitions(t => RewriteTransitionConditions(t, action));
        }

        public IEnumerable<VFTransitionBase> allTransitions => stateMachineValue == null
            ? Enumerable.Empty<VFTransitionBase>()
            : stateMachineValue.GetAllTransitions();

        public void RewriteTransitions(Func<VFTransitionBase, OneOrMany<VFTransitionBase>> action) {
            stateMachineValue?.RewriteTransitionLists(action);
        }

        private static OneOrMany<VFTransitionBase> RewriteTransitionConditions(
            VFTransitionBase transition,
            Func<AnimatorCondition, AnimatorConditionExtensions.Rewritten> action
        ) {
            var conditions = transition.conditions ?? Array.Empty<AnimatorCondition>();
            var updated = false;
            var andOr = conditions.SelectMany(condition => {
                var rewritten = action(condition);
                if (rewritten.andOr.Length != 1 || rewritten.andOr[0].Length != 1 || !rewritten.andOr[0][0].Equals(condition)) {
                    updated = true;
                }
                return rewritten.andOr;
            }).ToArray();

            if (!updated) {
                return transition;
            }

            return andOr.CrossProduct()
                .Select(and => {
                    var clone = transition.Clone(
                        new Dictionary<VFState, VFState>(),
                        new Dictionary<VFStateMachine, VFStateMachine>()
                    );
                    clone.conditions = and.ToArray();
                    if (clone.destinationState == null) clone.destinationState = transition.destinationState;
                    if (clone.destinationStateMachine == null) clone.destinationStateMachine = transition.destinationStateMachine;
                    return clone;
                })
                .ToArray();
        }

        internal void ReplaceStateMachine(VFStateMachine newStateMachine) {
            stateMachineValue = newStateMachine;
        }
    }
}
