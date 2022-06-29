using System;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VRCF.Builder {

public class DataCopier {
    public static void Copy(AnimatorController from, AnimatorController to, string layerPrefix, Func<AnimationClip,AnimationClip> rewriteClip) {
        foreach (var param in from.parameters) {
            var exists = Array.Find(to.parameters, other => other.name == param.name);
            if (exists == null) {
                to.AddParameter(param.name, param.type);
                var copy = to.parameters[to.parameters.Length-1];
                copy.defaultBool = param.defaultBool;
                copy.defaultFloat = param.defaultFloat;
                copy.defaultInt = param.defaultInt;
            }
        }
 
        foreach (var fromLayer in from.layers) {
            to.AddLayer(layerPrefix + fromLayer.name);
            var toLayers = to.layers;
            var toLayer = toLayers[to.layers.Length-1];
            toLayer.avatarMask = fromLayer.avatarMask;
            toLayer.blendingMode = fromLayer.blendingMode;
            toLayer.iKPass = fromLayer.iKPass;
            toLayer.defaultWeight = fromLayer.defaultWeight;
            to.layers = toLayers;
            CopyMachine(fromLayer.stateMachine, toLayer.stateMachine, toLayer.stateMachine, rewriteClip);
        }
    }

    private static void CopyMachine(AnimatorStateMachine from, AnimatorStateMachine to, AnimatorStateMachine toBase, Func<AnimationClip,AnimationClip> rewriteClip) {
        to.exitPosition = from.exitPosition;
        to.entryPosition = from.entryPosition;
        to.anyStatePosition = from.anyStatePosition;
        to.parentStateMachinePosition = from.parentStateMachinePosition;

        // Copy States
        foreach (var fromStateOuter in from.states) {
            var fromState = fromStateOuter.state;
            var toState = to.AddState(fromState.name, fromStateOuter.position);
            toState.speed = fromState.speed;
            toState.timeParameter = fromState.timeParameter;
            toState.timeParameterActive = fromState.timeParameterActive;
            toState.motion = CopyMotion(fromState.motion, rewriteClip);
            foreach (var b in fromState.behaviours) {
                if (b is VRCAvatarParameterDriver) {
                    var oldB = b as VRCAvatarParameterDriver;
                    var newB = toState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    newB.parameters = oldB.parameters;
                    newB.localOnly = oldB.localOnly;
                    newB.debugString = oldB.debugString;
                    newB.initialized = oldB.initialized;
                    newB.isLocalPlayer = oldB.isLocalPlayer;
                    newB.isEnabled = oldB.isEnabled;
                }
            }

            if (fromState == from.defaultState) {
                to.defaultState = toState;
            }
        }

        // Copy Substate Machines
        foreach (var fromMachineOuter in from.stateMachines) {
            var fromMachine = fromMachineOuter.stateMachine;
            var toMachine = new AnimatorStateMachine();
            toMachine.name = fromMachine.name;
            to.AddStateMachine(toMachine.name, fromMachineOuter.position);
            CopyMachine(fromMachine, toMachine, toBase, rewriteClip);
        }
 
        CopyTransitions(from, to, toBase);
    }

    private static Motion CopyMotion(Motion from, Func<AnimationClip,AnimationClip> rewriteClip) {
        if (from == null) return null;
        if (from is AnimationClip) {
            return rewriteClip(from as AnimationClip);
        }
        if (from is BlendTree) {
            var oldBlendTree = from as BlendTree;
            var newBlendTree = new BlendTree();

            newBlendTree.useAutomaticThresholds = false;
            newBlendTree.blendParameter = oldBlendTree.blendParameter;
            newBlendTree.blendType = oldBlendTree.blendType;

            foreach (var oldChild in oldBlendTree.children) {
                var newMotion = CopyMotion(oldChild.motion, rewriteClip);
                newBlendTree.AddChild(newMotion, oldChild.threshold);
                var newChild = newBlendTree.children[newBlendTree.children.Length-1];
                newChild.timeScale = oldChild.timeScale;
                newChild.position = oldChild.position;
                newChild.threshold = oldChild.threshold;
                newChild.directBlendParameter = oldChild.directBlendParameter;
            }
        }
        return null;
    }

    private static void CopyTransition(
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

        to.conditions = from.conditions;
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

    private static void CopyTransitions(AnimatorStateMachine from, AnimatorStateMachine to, AnimatorStateMachine toBase) {
        foreach (var oldTrans in from.anyStateTransitions) {
            CopyTransition(oldTrans, to, toBase, s => to.AddAnyStateTransition(s), s => to.AddAnyStateTransition(s));
        }
        foreach (var oldTrans in from.entryTransitions) {
            CopyTransition(oldTrans, to, toBase, s => to.AddEntryTransition(s), s => to.AddEntryTransition(s));
        }
        foreach (var fromState in from.states) {
            var toStateIdx = Array.FindIndex(to.states, s => s.state.name == fromState.state.name);
            if (toStateIdx < 0) continue;
            var toState = to.states[toStateIdx];
            foreach (var oldTrans in fromState.state.transitions) {
                if (oldTrans.isExit) {
                    CopyTransition(oldTrans, to, toBase, s => toState.state.AddExitTransition(), null);
                } else {
                    CopyTransition(oldTrans, to, toBase, s => toState.state.AddTransition(s), s => toState.state.AddTransition(s));
                }
            }
        }
    }
}

}
