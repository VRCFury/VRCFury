using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Builder {

/**
 * Copies everything from one animator controller into another. Optionally rewriting all clips found along the way.
 */
public class ControllerMerger {
    private readonly Func<string, string> rewriteLayerName;
    private readonly Func<string, string> rewriteParamName;
    private readonly Func<AnimationClip, AnimationClip> rewriteClip;

    public ControllerMerger(Func<string, string> rewriteLayerName, Func<string, string> rewriteParamName, Func<AnimationClip, AnimationClip> rewriteClip) {
        this.rewriteLayerName = rewriteLayerName;
        this.rewriteParamName = rewriteParamName;
        this.rewriteClip = rewriteClip;
    }

    public void Merge(AnimatorController from, AnimatorController to) {
        foreach (var param in from.parameters) {
            var newName = rewriteParamName(param.name);
            var exists = Array.Find(to.parameters, other => other.name == newName);
            if (exists == null) {
                to.AddParameter(newName, param.type);
                var copy = to.parameters[to.parameters.Length-1];
                copy.defaultBool = param.defaultBool;
                copy.defaultFloat = param.defaultFloat;
                copy.defaultInt = param.defaultInt;
            }
        }
 
        foreach (var fromLayer in from.layers) {
            to.AddLayer(rewriteLayerName(fromLayer.name));
            var toLayers = to.layers;
            var toLayer = toLayers[to.layers.Length-1];
            toLayer.avatarMask = fromLayer.avatarMask;
            toLayer.blendingMode = fromLayer.blendingMode;
            toLayer.iKPass = fromLayer.iKPass;
            toLayer.defaultWeight = fromLayer.defaultWeight;
            to.layers = toLayers;
            CloneMachine(fromLayer.stateMachine, toLayer.stateMachine, toLayer.stateMachine);
        }
    }

    private void CloneMachine(AnimatorStateMachine from, AnimatorStateMachine to, AnimatorStateMachine toBase) {
        to.exitPosition = from.exitPosition;
        to.entryPosition = from.entryPosition;
        to.anyStatePosition = from.anyStatePosition;
        to.parentStateMachinePosition = from.parentStateMachinePosition;

        // Copy States
        foreach (var fromStateOuter in from.states) {
            var fromState = fromStateOuter.state;
            var toState = to.AddState(fromState.name, fromStateOuter.position);
            toState.speed = fromState.speed;
            toState.cycleOffset = fromState.cycleOffset;
            toState.mirror = fromState.mirror;
            toState.iKOnFeet = fromState.iKOnFeet;
            // We never use write defaults, because VRCFury will collect all the default values and handle them later
            toState.writeDefaultValues = false;
            toState.tag = fromState.tag;
            toState.speedParameter = rewriteParamName(fromState.speedParameter);
            toState.cycleOffsetParameter = rewriteParamName(fromState.cycleOffsetParameter);
            toState.mirrorParameter = rewriteParamName(fromState.mirrorParameter);
            toState.timeParameter = rewriteParamName(fromState.timeParameter);
            toState.speedParameterActive = fromState.speedParameterActive;
            toState.cycleOffsetParameterActive = fromState.cycleOffsetParameterActive;
            toState.mirrorParameterActive = fromState.mirrorParameterActive;
            toState.timeParameterActive = fromState.timeParameterActive;

            toState.motion = CloneMotion(fromState.motion);
            foreach (var b in fromState.behaviours) {
                if (b is VRCAvatarParameterDriver) {
                    var oldB = b as VRCAvatarParameterDriver;
                    var newB = toState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    foreach (var p in oldB.parameters) {
                        newB.parameters.Add(CloneDriverParameter(p));
                    }
                    newB.localOnly = oldB.localOnly;
                    newB.debugString = oldB.debugString;
                }
            }

            if (fromState == from.defaultState) {
                to.defaultState = toState;
            }
        }

        // Copy Substate Machines
        foreach (var fromMachineOuter in from.stateMachines) {
            var fromMachine = fromMachineOuter.stateMachine;
            var toMachine = new AnimatorStateMachine {
                name = fromMachine.name
            };
            to.AddStateMachine(toMachine.name, fromMachineOuter.position);
            CloneMachine(fromMachine, toMachine, toBase);
        }
 
        CloneTransitions(from, to, toBase);
    }

    private VRC_AvatarParameterDriver.Parameter CloneDriverParameter(VRC_AvatarParameterDriver.Parameter from) {
        var to = new VRC_AvatarParameterDriver.Parameter {
            type = from.type,
            name = rewriteParamName(from.name),
            value = from.value,
            valueMin = from.valueMin,
            valueMax = from.valueMax,
            chance = from.chance,
        };
        CloneFieldIfPossible(from, to, "source", s => rewriteParamName((string)s));
        CloneFieldIfPossible(from, to, "convertRange");
        CloneFieldIfPossible(from, to, "sourceMin");
        CloneFieldIfPossible(from, to, "sourceMax");
        CloneFieldIfPossible(from, to, "destMin");
        CloneFieldIfPossible(from, to, "destMax");
        CloneFieldIfPossible(from, to, "sourceParam");
        CloneFieldIfPossible(from, to, "destParam");
        return to;
    }

    private void CloneFieldIfPossible(object from, object to, string field, Func<object,object> adjust = null) {
        var toType = to.GetType();
        var fromType = from.GetType();
        var fromField = fromType.GetField(field);
        var toField = toType.GetField(field);
        if (fromField != null && toField != null) {
            var val = fromField.GetValue(from);
            if (adjust != null) val = adjust(val);
            toField.SetValue(to, val);
        }
    }

    private Motion CloneMotion(Motion from) {
        switch (from) {
            case AnimationClip clip:
                return rewriteClip(clip);
            case BlendTree tree:
                var oldBlendTree = tree;
                var newBlendTree = new BlendTree {
                    blendParameter = rewriteParamName(oldBlendTree.blendParameter),
                    blendParameterY = rewriteParamName(oldBlendTree.blendParameterY),
                    blendType = oldBlendTree.blendType,
                    useAutomaticThresholds = oldBlendTree.useAutomaticThresholds,
                    minThreshold = oldBlendTree.minThreshold,
                    maxThreshold = oldBlendTree.maxThreshold,
                };

                foreach (var oldChild in oldBlendTree.children) {
                    var newMotion = CloneMotion(oldChild.motion);
                    newBlendTree.AddChild(newMotion, oldChild.threshold);
                    var newChild = newBlendTree.children[newBlendTree.children.Length-1];
                    newChild.timeScale = oldChild.timeScale;
                    newChild.position = oldChild.position;
                    newChild.threshold = oldChild.threshold;
                    newChild.directBlendParameter = rewriteParamName(oldChild.directBlendParameter);
                    newChild.cycleOffset = oldChild.cycleOffset;
                    newChild.mirror = oldChild.mirror;
                }

                return newBlendTree;
            default:
                return rewriteClip(null);
        }
    }

    private void CloneTransition(
        AnimatorTransitionBase from,
        AnimatorStateMachine toMachine,
        AnimatorStateMachine toMachineParent,
        Func<AnimatorState,AnimatorTransitionBase> makeNewWithState,
        Func<AnimatorStateMachine,AnimatorTransitionBase> makeNewWithMachine
    ) {
        AnimatorTransitionBase to;
        if (from.isExit) {
            to = makeNewWithState(null);
        } else if (from.destinationState != null) {
            var newDestIdx = Array.FindIndex(toMachine.states, s => s.state.name == from.destinationState.name);
            if (newDestIdx < 0) return;
            to = makeNewWithState(toMachine.states[newDestIdx].state);
        } else {
            var newDestIdx = Array.FindIndex(toMachine.stateMachines, s => s.stateMachine.name == from.destinationStateMachine.name);
            if (newDestIdx >= 0) {
                to = makeNewWithMachine(toMachine.stateMachines[newDestIdx].stateMachine);
            } else {
                newDestIdx = Array.FindIndex(toMachineParent.stateMachines, s => s.stateMachine.name == from.destinationStateMachine.name);
                if (newDestIdx >= 0) {
                    to = makeNewWithMachine(toMachineParent.stateMachines[newDestIdx].stateMachine);
                } else {
                    return;
                }
            }
        }

        var conds = new List<AnimatorCondition>();
        foreach (var oldC in from.conditions) {
            conds.Add(new AnimatorCondition {
                mode = oldC.mode,
                parameter = rewriteParamName(oldC.parameter),
                threshold = oldC.threshold
            });
        }
        to.conditions = conds.ToArray();
        if (to is AnimatorStateTransition) {
            var to2 = (AnimatorStateTransition)to;
            var from2 = (AnimatorStateTransition)from;
            to2.canTransitionToSelf = from2.canTransitionToSelf;
            to2.hasExitTime = from2.hasExitTime;
            to2.exitTime = from2.exitTime;
            to2.duration = from2.duration;
            to2.interruptionSource = from2.interruptionSource;
            to2.orderedInterruption = from2.orderedInterruption;
            to2.offset = from2.offset;
            to2.hasFixedDuration = from2.hasFixedDuration;
        }
    }

    private void CloneTransitions(AnimatorStateMachine from, AnimatorStateMachine to, AnimatorStateMachine toBase) {
        foreach (var oldTrans in from.anyStateTransitions) {
            CloneTransition(
                oldTrans,
                to,
                toBase,
                to.AddAnyStateTransition,
                to.AddAnyStateTransition
            );
        }
        foreach (var oldTrans in from.entryTransitions) {
            CloneTransition(
                oldTrans,
                to,
                toBase,
                to.AddEntryTransition,
                to.AddEntryTransition
            );
        }
        foreach (var fromState in from.states) {
            var toStateIdx = Array.FindIndex(to.states, s => s.state.name == fromState.state.name);
            if (toStateIdx < 0) continue;
            var toState = to.states[toStateIdx];
            foreach (var oldTrans in fromState.state.transitions) {
                if (oldTrans.isExit) {
                    CloneTransition(
                        oldTrans,
                        to,
                        toBase,
                        s => toState.state.AddExitTransition(),
                        null
                    );
                } else {
                    CloneTransition(
                        oldTrans,
                        to,
                        toBase,
                        toState.state.AddTransition,
                        toState.state.AddTransition
                    );
                }
            }
        }
    }
}

}
