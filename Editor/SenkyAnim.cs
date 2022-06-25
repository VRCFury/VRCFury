using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

public class SenkyAnimController {
    private AnimatorController ctrl;
    internal AnimationClip noopClip;

    public SenkyAnimController(AnimatorController ctrl, AnimationClip noopClip) {
        this.ctrl = ctrl;
        this.noopClip = noopClip;
    }

    public SenkyAnimLayer NewLayer(string name) {
        ctrl.AddLayer(name);
        var layers = ctrl.layers;
        var layer = layers[ctrl.layers.Length-1];
        layer.defaultWeight = 1;
        layer.stateMachine.anyStatePosition = SenkyAnimState.MovePos(layer.stateMachine.entryPosition, 0, 1);
        ctrl.layers = layers;
        return new SenkyAnimLayer(layer, this);
    }

    public SenkyAnimParamBool NewTrigger(string name) {
        return new SenkyAnimParamBool(NewParam(name, AnimatorControllerParameterType.Trigger));
    }
    public SenkyAnimParamBool NewBool(string name, bool def = false) {
        return new SenkyAnimParamBool(NewParam(name, AnimatorControllerParameterType.Bool, param => param.defaultBool = def));
    }
    public SenkyAnimParamNumber NewFloat(string name, float def = 0) {
        return new SenkyAnimParamNumber(NewParam(name, AnimatorControllerParameterType.Float, param => param.defaultFloat = def));
    }
    public SenkyAnimParamNumber NewInt(string name, int def = 0) {
        return new SenkyAnimParamNumber(NewParam(name, AnimatorControllerParameterType.Int, param => param.defaultInt = def));
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

public class SenkyAnimLayer {
    private AnimatorControllerLayer layer;
    private SenkyAnimController ctrl;
    private List<string> statesIgnoredForPos = new List<string>();

    public SenkyAnimLayer(AnimatorControllerLayer layer, SenkyAnimController ctrl) {
        this.layer = layer;
        this.ctrl = ctrl;
    }

    public SenkyAnimState NewState(string name) {
        var lastNode = GetLastNodeForPositioning();
        layer.stateMachine.AddState(name);
        var node = GetLastNode().Value;
        node.state.writeDefaultValues = false;
        node.state.motion = ctrl.noopClip;

        var state = new SenkyAnimState(node, layer);
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

    public SenkyAnimLayer AddRemoteEntry() {
        var remote = NewState("Remote").Move(layer.stateMachine.entryPosition, -1, 0);
        var IsRemote = ctrl.NewBool("IsRemote");
        remote.TransitionsFromEntry().When(IsRemote.IsTrue());
        return this;
    }
}

public class SenkyAnimState {
    private ChildAnimatorState node;
    private AnimatorControllerLayer layer;
    private VRCAvatarParameterDriver driver = null;

    private static float X_OFFSET = 250;
    private static float Y_OFFSET = 80;

    public SenkyAnimState(ChildAnimatorState node, AnimatorControllerLayer layer) {
        this.node = node;
        this.layer = layer;
    }

    public static Vector3 MovePos(Vector3 orig, float x, float y) {
        var pos = orig;
        pos.x += x * X_OFFSET;
        pos.y += y * Y_OFFSET;
        return pos;
    }
    public SenkyAnimState Move(Vector3 orig, float x, float y) {
        node.position = MovePos(orig, x, y);
        var states = layer.stateMachine.states;
        var index = Array.FindIndex(states, n => n.state == node.state);
        if (index >= 0) {
            states[index] = node;
            layer.stateMachine.states = states;
        }
        return this;
    }
    public SenkyAnimState Move(SenkyAnimState other, float x, float y) {
        Move(other.node.position, x, y);
        return this;
    }
    public SenkyAnimState Move(float x, float y) {
        Move(this, x, y);
        return this;
    }

    public SenkyAnimState WithAnimation(Motion motion) {
        node.state.motion = motion;
        return this;
    }
    public SenkyAnimState MotionTime(SenkyAnimParamNumber param) {
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
    public SenkyAnimState Drives(SenkyAnimParamBool param, bool value, bool local = false) {
        Drives(param.Name(), local).value = value ? 1 : 0;
        return this;
    }
    public SenkyAnimState Drives(SenkyAnimParamNumber param, float value, bool local = false) {
        Drives(param.Name(), local).value = value;
        return this;
    }
    public SenkyAnimState DrivesRandom(SenkyAnimParamNumber param, float min, float max) {
        var p = Drives(param.Name(), true);
        p.type = VRCAvatarParameterDriver.ChangeType.Random;
        p.valueMin = min;
        p.valueMax = max;
        return this;
    }
    public SenkyAnimState DrivesDelta(SenkyAnimParamNumber param, float delta) {
        var p = Drives(param.Name(), true);
        p.type = VRCAvatarParameterDriver.ChangeType.Add;
        p.value = delta;
        return this;
    }

    public SenkyAnimTransition TransitionsFromEntry() {
        var trans = layer.stateMachine.AddEntryTransition(node.state);
        return new SenkyAnimTransition(trans);
    }
    public SenkyAnimStateTransition TransitionsFromAny() {
        var trans = layer.stateMachine.AddAnyStateTransition(node.state);
        trans.canTransitionToSelf = false;
        return new SenkyAnimStateTransition(trans);
    }
    public SenkyAnimStateTransition TransitionsTo(SenkyAnimState other) {
        var trans = node.state.AddTransition(other.node.state);
        return new SenkyAnimStateTransition(trans);
    }
    public SenkyAnimStateTransition TransitionsToExit() {
        var trans = node.state.AddExitTransition();
        return new SenkyAnimStateTransition(trans);
    }
}

public class SenkyAnimParam {
    private AnimatorControllerParameter param;
    public SenkyAnimParam(AnimatorControllerParameter param) {
        this.param = param;
    }
    public string Name() {
        return this.param.name;
    }
}
public class SenkyAnimParamBool : SenkyAnimParam {
    public SenkyAnimParamBool(AnimatorControllerParameter param) : base(param) {}

    public SenkyAnimCondition IsTrue() {
        return new SenkyAnimCondition(trans => trans.AddCondition(AnimatorConditionMode.If, 0, Name()));
    }
    public SenkyAnimCondition IsFalse() {
        return new SenkyAnimCondition(trans => trans.AddCondition(AnimatorConditionMode.IfNot, 0, Name()));
    }
}
public class SenkyAnimParamNumber : SenkyAnimParam {
    public SenkyAnimParamNumber(AnimatorControllerParameter param) : base(param) {}

    public SenkyAnimCondition IsGreaterThan(float num) {
        return new SenkyAnimCondition(trans => trans.AddCondition(AnimatorConditionMode.Greater, num, Name()));
    }
    public SenkyAnimCondition IsLessThan(float num) {
        return new SenkyAnimCondition(trans => trans.AddCondition(AnimatorConditionMode.Less, num, Name()));
    }
    public SenkyAnimCondition IsEqualTo(float num) {
        return new SenkyAnimCondition(trans => trans.AddCondition(AnimatorConditionMode.Equals, num, Name()));
    }
    public SenkyAnimCondition IsNotEqualTo(float num) {
        return new SenkyAnimCondition(trans => trans.AddCondition(AnimatorConditionMode.NotEqual, num, Name()));
    }
}

public class SenkyAnimCondition {
    internal Action<AnimatorTransitionBase> apply;
    public SenkyAnimCondition(Action<AnimatorTransitionBase> apply) {
        this.apply = apply;
    }
    public SenkyAnimCondition And(SenkyAnimCondition other) {
        return new SenkyAnimCondition(trans => { this.apply(trans); other.apply(trans); });
    }
}

public class SenkyAnimTransition {
    private AnimatorTransition trans;
    public SenkyAnimTransition(AnimatorTransition trans) {
        this.trans = trans;
    }

    public SenkyAnimTransition When(SenkyAnimCondition cond) {
        cond.apply(trans);
        return this;
    }
}
public class SenkyAnimStateTransition {
    private AnimatorStateTransition trans;
    public SenkyAnimStateTransition(AnimatorStateTransition trans) {
        this.trans = trans;
        trans.duration = 0;
    }

    public SenkyAnimStateTransition When(SenkyAnimCondition cond) {
        cond.apply(trans);
        return this;
    }
    public SenkyAnimStateTransition WithTransitionToSelf() {
        trans.canTransitionToSelf = true;
        return this;
    }
    public SenkyAnimStateTransition WithTransitionDurationSeconds(float time) {
        trans.duration = time;
        return this;
    }
}
