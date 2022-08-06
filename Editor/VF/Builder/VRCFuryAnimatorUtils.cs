using System;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Builder {

public class VFAController {
    private readonly AnimatorController ctrl;
    internal readonly AnimationClip noopClip;
    private readonly VRCAvatarDescriptor.AnimLayerType type;

    public VFAController(AnimatorController ctrl, AnimationClip noopClip, VRCAvatarDescriptor.AnimLayerType type) {
        this.ctrl = ctrl;
        this.noopClip = noopClip;
        this.type = type;
    }

    public VFALayer NewLayer(string name, bool first = false) {
        ctrl.AddLayer(name);
        var layers = ctrl.layers;
        var layer = layers[ctrl.layers.Length-1];
        if (first) {
            // Leave top layer alone if it's base layer (with no states)
            var skipOneLayer = layers[0].stateMachine.states.Length == 0;
            for (var i = layers.Length-1; i > (skipOneLayer ? 1 : 0); i--) {
                layers[i] = layers[i - 1];
            }
            layers[skipOneLayer ? 1 : 0] = layer;
            ControllerManager.CorrectLayerReferences(ctrl, skipOneLayer ? 0 : -1, type,  1);
        }
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
    private readonly AnimatorControllerLayer layer;
    private readonly VFAController ctrl;

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

    private ChildAnimatorState? GetLastNodeForPositioning() {
        var states = layer.stateMachine.states;
        var index = Array.FindLastIndex(states, state => !state.state.name.StartsWith("_"));
        if (index < 0) return null;
        return states[index];
    }

    private ChildAnimatorState? GetLastNode() {
        var states = layer.stateMachine.states;
        if (states.Length == 0) return null;
        return states[states.Length-1];
    }
}

public class VFAState {
    private ChildAnimatorState node;
    private readonly AnimatorControllerLayer layer;

    private static readonly float X_OFFSET = 250;
    private static readonly float Y_OFFSET = 80;

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

    public VRCAvatarParameterDriver GetDriver(bool local = false) {
        foreach (var b in node.state.behaviours) {
            var d = b as VRCAvatarParameterDriver;
            if (d && d.localOnly == local) return d;
        }
        var driver = node.state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
        driver.localOnly = local;
        return driver;
    }
    private VRC_AvatarParameterDriver.Parameter Drives(string param, bool local = false) {
        var driver = GetDriver(local);
        var p = new VRC_AvatarParameterDriver.Parameter();
        p.name = param;
        p.type = VRC_AvatarParameterDriver.ChangeType.Set;
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
        p.type = VRC_AvatarParameterDriver.ChangeType.Random;
        p.valueMin = min;
        p.valueMax = max;
        return this;
    }
    public VFAState DrivesDelta(VFANumber param, float delta) {
        var p = Drives(param.Name(), true);
        p.type = VRC_AvatarParameterDriver.ChangeType.Add;
        p.value = delta;
        return this;
    }
    public VFAState DrivesCopy(VFANumber param, VFANumber source) {
        var driver = GetDriver(true);
        var p = new VRC_AvatarParameterDriver.Parameter();
        p.name = param.Name();
        var sourceField = p.GetType().GetField("source");
        if (sourceField == null) throw new Exception("VRCFury feature failed to build because VRCSDK is outdated");
        sourceField.SetValue(p, source.Name());
        // We cast rather than use Copy directly so it doesn't fail to compile on old VRCSDK
        p.type = (VRC_AvatarParameterDriver.ChangeType)3; //VRC_AvatarParameterDriver.ChangeType.Copy;
        driver.parameters.Add(p);
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
    private readonly AnimatorControllerParameter param;
    public VFAParam(AnimatorControllerParameter param) {
        this.param = param;
    }
    public string Name() {
        return param.name;
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
        return new VFACondition(trans => { apply(trans); other.apply(trans); });
    }
}

public class VFATransition {
    private readonly AnimatorTransition trans;
    public VFATransition(AnimatorTransition trans) {
        this.trans = trans;
    }

    public VFATransition When(VFACondition cond) {
        cond.apply(trans);
        return this;
    }
}
public class VFAAnyTransition {
    private readonly AnimatorStateTransition trans;
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
