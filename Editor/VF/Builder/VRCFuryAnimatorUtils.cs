using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Builder {

public class VFAController {
    private readonly AnimatorController ctrl;
    private readonly VRCAvatarDescriptor.AnimLayerType type;

    public VFAController(AnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType type) {
        this.ctrl = ctrl;
        this.type = type;
    }

    public VFALayer NewLayer(string name, int insertAt = -1) {
        ctrl.AddLayer(name);
        var layers = ctrl.layers;
        var layer = layers.Last();
        if (insertAt >= 0) {
            for (var i = layers.Length-1; i > insertAt; i--) {
                layers[i] = layers[i - 1];
            }
            layers[insertAt] = layer;
            AdjustAnimatorLayerControl(layerNum => layerNum >= insertAt ? layerNum + 1 : layerNum);
        }
        layer.defaultWeight = 1;
        layer.stateMachine.anyStatePosition = VFAState.MovePos(layer.stateMachine.entryPosition, 0, 1);
        ctrl.layers = layers;
        return new VFALayer(layer, this);
    }
    
    public void RemoveLayer(int i) {
        AdjustAnimatorLayerControl(layerNum =>
            layerNum == i ? -1 :
            layerNum > i ? layerNum - 1 :
            layerNum);
        ctrl.RemoveLayer(i);
    }
        
    private void AdjustAnimatorLayerControl(Func<int,int> correction) {
        foreach (var layer in ctrl.layers) {
            AnimatorIterator.ForEachBehaviour(layer, (b, _) => {
                if (b is VRCAnimatorLayerControl layerControl) {
                    if (VRCFEnumUtils.GetName(type) == VRCFEnumUtils.GetName(layerControl.playable)) {
                        layerControl.layer = correction.Invoke(layerControl.layer);
                        if (layerControl.layer < 0) return false;
                    }
                }
                return true;
            });
        }
    }

    public void InflatePlayableLayerControl(AnimatorControllerLayer layer, int minLayer, int maxLayer) {
        var ctrlType = VRCFEnumUtils.GetName(type);
        AnimatorIterator.ForEachBehaviour(layer, (b, add) => {
            if (b is VRCPlayableLayerControl layerControl) {
                var layerControlTarget = VRCFEnumUtils.GetName(layerControl.layer);
                if (ctrlType == layerControlTarget) {
                    for (var i = minLayer; i <= maxLayer; i++) {
                        var newB = add(typeof(VRCAnimatorLayerControl)) as VRCAnimatorLayerControl;
                        newB.layer = i;
                        newB.playable = VRCFEnumUtils.Parse<VRC_AnimatorLayerControl.BlendableLayer>(ctrlType);
                        newB.goalWeight = layerControl.goalWeight;
                        newB.blendDuration = layerControl.blendDuration;
                        newB.debugString = layerControl.debugString;
                    }
                    return false;
                }
            }
            return true;
        });
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
        node.state.writeDefaultValues = true;

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

    public AnimatorControllerLayer GetRaw() {
        return layer;
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
        if (sourceField == null) throw new VRCFBuilderException("VRCFury feature failed to build because VRCSDK is outdated");
        sourceField.SetValue(p, source.Name());
        // We cast rather than use Copy directly so it doesn't fail to compile on old VRCSDK
        p.type = (VRC_AvatarParameterDriver.ChangeType)3; //VRC_AvatarParameterDriver.ChangeType.Copy;
        driver.parameters.Add(p);
        return this;
    }

    public VFAEntryTransition TransitionsFromEntry() {
        return new VFAEntryTransition(() => layer.stateMachine.AddEntryTransition(node.state));
    }
    public VFATransition TransitionsFromAny() {
        return new VFATransition(() => layer.stateMachine.AddAnyStateTransition(node.state));
    }
    public VFATransition TransitionsTo(VFAState other) {
        return new VFATransition(() => node.state.AddTransition(other.node.state));
    }
    public VFATransition TransitionsToExit() {
        return new VFATransition(() => node.state.AddExitTransition());
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
        return new VFACondition(new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = Name(), threshold = 0 });
    }
    public VFACondition IsFalse() {
        return new VFACondition(new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = Name(), threshold = 0 });
    }
}
public class VFANumber : VFAParam {
    public VFANumber(AnimatorControllerParameter param) : base(param) {}

    public VFACondition IsGreaterThan(float num) {
        return new VFACondition(new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = Name(), threshold = num });
    }
    public VFACondition IsLessThan(float num) {
        return new VFACondition(new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = Name(), threshold = num });
    }
    public VFACondition IsEqualTo(float num) {
        return new VFACondition(new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = Name(), threshold = num });
    }
    public VFACondition IsNotEqualTo(float num) {
        return new VFACondition(new AnimatorCondition { mode = AnimatorConditionMode.NotEqual, parameter = Name(), threshold = num });
    }
}

public class VFACondition {
    internal IEnumerable<IEnumerable<AnimatorCondition>> transitions;
    public VFACondition(AnimatorCondition cond) {
        var transition = new List<AnimatorCondition> { cond };
        transitions = new List<List<AnimatorCondition>> { transition };
    }
    public VFACondition(IEnumerable<IEnumerable<AnimatorCondition>> transitions) {
        this.transitions = transitions;
    }
    public VFACondition And(VFACondition other) {
        return new VFACondition(AnimatorConditionLogic.And(transitions, other.transitions));
    }
    public VFACondition Or(VFACondition other) {
        return new VFACondition(AnimatorConditionLogic.Or(transitions, other.transitions));
    }
    public VFACondition Not() {
        return new VFACondition(AnimatorConditionLogic.Not(transitions));
    }

}

public class VFAEntryTransition {
    private readonly Func<AnimatorTransition> transitionProvider;
    public VFAEntryTransition(Func<AnimatorTransition> transitionProvider) {
        this.transitionProvider = transitionProvider;
    }

    public VFAEntryTransition When(VFACondition cond) {
        foreach (var t in cond.transitions) {
            var transition = transitionProvider();
            transition.conditions = t.ToArray();
        }
        return this;
    }
}
public class VFATransition {
    private readonly List<AnimatorStateTransition> createdTransitions = new List<AnimatorStateTransition>();
    private Func<AnimatorStateTransition> transitionProvider;
    public VFATransition(Func<AnimatorStateTransition> transitionProvider) {
        this.transitionProvider = () => {
             var trans = transitionProvider();
             trans.duration = 0;
             trans.canTransitionToSelf = false;
             createdTransitions.Add(trans);
             return trans;
        };
    }

    public VFATransition When(VFACondition cond) {
        foreach (var t in cond.transitions) {
            var transition = transitionProvider();
            transition.conditions = t.ToArray();
        }
        return this;
    }
    public VFATransition WithTransitionToSelf() {
        foreach (var t in createdTransitions) {
            t.canTransitionToSelf = true;
        }
        var oldProvider = transitionProvider;
        transitionProvider = () => {
            var trans = oldProvider();
            trans.canTransitionToSelf = true;
            return trans;
        };
        return this;
    }
    public VFATransition WithTransitionDurationSeconds(float time) {
        foreach (var t in createdTransitions) {
            t.duration = time;
        }
        var oldProvider = transitionProvider;
        transitionProvider = () => {
            var trans = oldProvider();
            trans.duration = time;
            return trans;
        };
        return this;
    }
}

}
