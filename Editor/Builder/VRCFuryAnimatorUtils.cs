using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace VRCF.Builder {

public class VFAController {
    private AnimatorController ctrl;
    internal AnimationClip noopClip;

    public VFAController(AnimatorController ctrl, AnimationClip noopClip) {
        this.ctrl = ctrl;
        this.noopClip = noopClip;
    }

    public VFALayer NewLayer(string name) {
        ctrl.AddLayer(name);
        var layers = ctrl.layers;
        var layer = layers[ctrl.layers.Length-1];
        layer.defaultWeight = 1;
        layer.stateMachine.anyStatePosition = VFAState.MovePos(layer.stateMachine.entryPosition, 0, 1);
        ctrl.layers = layers;
        return new VFALayer(layer, this);
    }

    public VFABool NewTrigger(string name) {
        return new VFABool(NewParam(name, AnimatorControllerParameterType.Trigger));
    }
    public VFABool NewBool(string name, bool def = false) {
        return new VFABool(NewParam(name, AnimatorControllerParameterType.Bool, param => param.defaultBool = def));
    }
    public VFANumber NewFloat(string name, float def = 0) {
        return new VFANumber(NewParam(name, AnimatorControllerParameterType.Float, param => param.defaultFloat = def));
    }
    public VFANumber NewInt(string name, int def = 0) {
        return new VFANumber(NewParam(name, AnimatorControllerParameterType.Int, param => param.defaultInt = def));
    }
    private AnimatorControllerParameter NewParam(string name, AnimatorControllerParameterType type, Action<AnimatorControllerParameter> with = null) {
        var exists = Array.Find(ctrl.parameters, other => other.name == name);
        if (exists != null) return exists;
        ctrl.AddParameter(name, type);
        var parameters = ctrl.parameters;
        var param = parameters[parameters.Length-1];
        if (with != null) with(param);
        ctrl.parameters = parameters;
        return param;
    }
}

public class VFALayer {
    private AnimatorControllerLayer layer;
    private VFAController ctrl;
    private List<string> statesIgnoredForPos = new List<string>();

    public VFALayer(AnimatorControllerLayer layer, VFAController ctrl) {
        this.layer = layer;
        this.ctrl = ctrl;
    }

    public VFAState NewState(string name) {
        var lastNode = GetLastNodeForPositioning();
        layer.stateMachine.AddState(name);
        var node = GetLastNode().Value;
        node.state.writeDefaultValues = false;
        node.state.motion = ctrl.noopClip;

        var state = new VFAState(node, layer);
        if (lastNode.HasValue) state.Move(lastNode.Value.position, 0, 1);
        else state.Move(layer.stateMachine.entryPosition, 1, 0);
        return state;
    }

    public ChildAnimatorState? GetLastNodeForPositioning() {
        var states = layer.stateMachine.states;
        var index = Array.FindLastIndex(states, state => !state.state.name.StartsWith("_"));
        if (index < 0) return null;
        return states[index];
    }
    public ChildAnimatorState? GetLastNode() {
        var states = layer.stateMachine.states;
        if (states.Length == 0) return null;
        return states[states.Length-1];
    }

    public VFALayer AddRemoteEntry() {
        var remote = NewState("Remote").Move(layer.stateMachine.entryPosition, -1, 0);
        var IsRemote = ctrl.NewBool("IsRemote");
        remote.TransitionsFromEntry().When(IsRemote.IsTrue());
        return this;
    }
}

public class VFAState {
    private ChildAnimatorState node;
    private AnimatorControllerLayer layer;
    private VRCAvatarParameterDriver driver = null;

    private static float X_OFFSET = 250;
    private static float Y_OFFSET = 80;

    public VFAState(ChildAnimatorState node, AnimatorControllerLayer layer) {
        this.node = node;
        this.layer = layer;
    }

    public static Vector3 MovePos(Vector3 orig, float x, float y) {
        var pos = orig;
        pos.x += x * X_OFFSET;
        pos.y += y * Y_OFFSET;
        return pos;
    }
    public VFAState Move(Vector3 orig, float x, float y) {
        node.position = MovePos(orig, x, y);
        var states = layer.stateMachine.states;
        var index = Array.FindIndex(states, n => n.state == node.state);
        if (index >= 0) {
            states[index] = node;
            layer.stateMachine.states = states;
        }
        return this;
    }
    public VFAState Move(VFAState other, float x, float y) {
        Move(other.node.position, x, y);
        return this;
    }
    public VFAState Move(float x, float y) {
        Move(this, x, y);
        return this;
    }

    public VFAState WithAnimation(Motion motion) {
        node.state.motion = motion;
        return this;
    }
    public VFAState MotionTime(VFANumber param) {
        node.state.timeParameterActive = true;
        node.state.timeParameter = param.Name();
        return this;
    }

    private VRCAvatarParameterDriver.Parameter Drives(string param, bool local = false) {
        if (driver == null) driver = node.state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
        var p = new VRCAvatarParameterDriver.Parameter();
        p.name = param;
        p.type = VRCAvatarParameterDriver.ChangeType.Set;
        driver.parameters.Add(p);
        return p;
    }
    public VFAState Drives(VFABool param, bool value, bool local = false) {
        Drives(param.Name(), local).value = value ? 1 : 0;
        return this;
    }
    public VFAState Drives(VFANumber param, float value, bool local = false) {
        Drives(param.Name(), local).value = value;
        return this;
    }
    public VFAState DrivesRandom(VFANumber param, float min, float max) {
        var p = Drives(param.Name(), true);
        p.type = VRCAvatarParameterDriver.ChangeType.Random;
        p.valueMin = min;
        p.valueMax = max;
        return this;
    }
    public VFAState DrivesDelta(VFANumber param, float delta) {
        var p = Drives(param.Name(), true);
        p.type = VRCAvatarParameterDriver.ChangeType.Add;
        p.value = delta;
        return this;
    }

    public VFATransition TransitionsFromEntry() {
        var trans = layer.stateMachine.AddEntryTransition(node.state);
        return new VFATransition(trans);
    }
    public VFAAnyTransition TransitionsFromAny() {
        var trans = layer.stateMachine.AddAnyStateTransition(node.state);
        trans.canTransitionToSelf = false;
        return new VFAAnyTransition(trans);
    }
    public VFAAnyTransition TransitionsTo(VFAState other) {
        var trans = node.state.AddTransition(other.node.state);
        return new VFAAnyTransition(trans);
    }
    public VFAAnyTransition TransitionsToExit() {
        var trans = node.state.AddExitTransition();
        return new VFAAnyTransition(trans);
    }
}

public class VFAParam {
    private AnimatorControllerParameter param;
    public VFAParam(AnimatorControllerParameter param) {
        this.param = param;
    }
    public string Name() {
        return this.param.name;
    }
}
public class VFABool : VFAParam {
    public VFABool(AnimatorControllerParameter param) : base(param) {}

    public VFACondition IsTrue() {
        return new VFACondition(trans => trans.AddCondition(AnimatorConditionMode.If, 0, Name()));
    }
    public VFACondition IsFalse() {
        return new VFACondition(trans => trans.AddCondition(AnimatorConditionMode.IfNot, 0, Name()));
    }
}
public class VFANumber : VFAParam {
    public VFANumber(AnimatorControllerParameter param) : base(param) {}

    public VFACondition IsGreaterThan(float num) {
        return new VFACondition(trans => trans.AddCondition(AnimatorConditionMode.Greater, num, Name()));
    }
    public VFACondition IsLessThan(float num) {
        return new VFACondition(trans => trans.AddCondition(AnimatorConditionMode.Less, num, Name()));
    }
    public VFACondition IsEqualTo(float num) {
        return new VFACondition(trans => trans.AddCondition(AnimatorConditionMode.Equals, num, Name()));
    }
    public VFACondition IsNotEqualTo(float num) {
        return new VFACondition(trans => trans.AddCondition(AnimatorConditionMode.NotEqual, num, Name()));
    }
}

public class VFACondition {
    internal Action<AnimatorTransitionBase> apply;
    public VFACondition(Action<AnimatorTransitionBase> apply) {
        this.apply = apply;
    }
    public VFACondition And(VFACondition other) {
        return new VFACondition(trans => { this.apply(trans); other.apply(trans); });
    }
}

public class VFATransition {
    private AnimatorTransition trans;
    public VFATransition(AnimatorTransition trans) {
        this.trans = trans;
    }

    public VFATransition When(VFACondition cond) {
        cond.apply(trans);
        return this;
    }
}
public class VFAAnyTransition {
    private AnimatorStateTransition trans;
    public VFAAnyTransition(AnimatorStateTransition trans) {
        this.trans = trans;
        trans.duration = 0;
    }

    public VFAAnyTransition When(VFACondition cond) {
        cond.apply(trans);
        return this;
    }
    public VFAAnyTransition WithTransitionToSelf() {
        trans.canTransitionToSelf = true;
        return this;
    }
    public VFAAnyTransition WithTransitionDurationSeconds(float time) {
        trans.duration = time;
        return this;
    }
}

}
