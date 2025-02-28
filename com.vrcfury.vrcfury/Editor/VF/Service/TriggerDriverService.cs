using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

namespace VF.Service {
    [VFService]
    internal class TriggerDriverService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly FloatToDriverService floatToDriverService;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        private VFLayer layer;
        private VFState idle;

        private readonly Dictionary<string, (VFAInteger,int,VFTransition)> currentSettings = new Dictionary<string, (VFAInteger,int,VFTransition)>();
        private readonly Dictionary<VFAFloat, VFState> createdStates = new Dictionary<VFAFloat, VFState>();

        private readonly List<(AnimationClip,VFAFloat,string,float)> drivenSyncParams = new ();
        private readonly List<(AnimationClip,VFAFloat,string,float)> drivenToggles = new ();
        private readonly List<(AnimationClip,VFAFloat,string,float,FeatureBuilder)> drivenTags = new ();

        public void DriveSyncParam(AnimationClip clip, VFAFloat triggerParam, string param, float value) {
            drivenSyncParams.Add((clip, triggerParam, param, value));
        }

        public void DriveToggle(AnimationClip clip, VFAFloat triggerParam, string toggle, float value) {
            drivenToggles.Add((clip, triggerParam, toggle, value));
        }

        public void DriveTag(AnimationClip clip, VFAFloat triggerParam, string tag, float value) {
            drivenTags.Add((clip, triggerParam, tag, value, globals.currentFeature()));
        }

        public void DriveOld(VFAFloat input, string output, float value, bool reset = true) {
            if (layer == null) {
                layer = fx.NewLayer("Cross-Type Param Driver");
                idle = layer.NewState("Idle");
            }

            if (!currentSettings.ContainsKey(output)) {
                var lastState_ = fx.NewInt($"{output}_lastState");
                var off = layer.NewState($"{output} = 0");
                off.TransitionsToExit().When(fx.Always());
                off.Drives(lastState_, 0);
                var t = idle.TransitionsTo(off).When(lastState_.IsGreaterThan(0));
                if (reset) {
                    off.Drives(output, 0);
                }
                currentSettings[output] = (lastState_, 0, t);
            }

            var (lastState, lastNumber, offTransition) = currentSettings[output];
            var myNumber = lastNumber + 1;
            {
                // Increment the usage number
                var c = currentSettings[output];
                c.Item2 = myNumber;
                currentSettings[output] = c;
            }

            if (!createdStates.ContainsKey(input)) {
                var name = $"{output} = {value} (from {input.Name()})";

                var state = layer.NewState(name);
                var condition = input.IsGreaterThan(0);
                offTransition.AddCondition(condition.Not());
                if (reset) {
                    idle.TransitionsTo(state).When(condition.And(lastState.IsLessThan(myNumber)));
                } else {
                    idle.TransitionsTo(state).When(condition.And(lastState.IsNotEqualTo(myNumber)));
                }
                state.TransitionsToExit().When(fx.Always());

                state.Drives(lastState.Name(), myNumber);

                createdStates[input] = state;
            }
            createdStates[input].Drives(output, value);
        }

        [FeatureBuilderAction(FeatureOrder.EvaluateTriggerParams)]
        public void DriveNonFloatTypes() {
            List<(AnimationClip, VFAFloat, string, float)> triggers = new();
            foreach (var trigger in drivenTags) {
                var (clip, triggerParam, tag, target, feature) = trigger;
                foreach (var other in globals.allBuildersInRun
                     .OfType<ToggleBuilder>()
                     .Where(b => b != feature)) {
                        var otherTags = other.GetTags();
                        
                        if (otherTags.Contains(tag)) {
                            if (target == 0) triggers.Add((clip, triggerParam, other.getParam(), 0));
                            else triggers.Add((clip, triggerParam, other.getParam(), other.model.slider ? target : 1));
                        }
                }
            }

            foreach (var trigger in drivenToggles) {
                var (clip, triggerParam, path, target) = trigger;
                var control = menu.GetMenuItem(path);
                if (control == null) continue;
                if (target == 0) triggers.Add((clip, triggerParam, control.parameter.name, 0));
                else if (control.type == ControlType.RadialPuppet) triggers.Add((clip, triggerParam, control.parameter.name, target));
                else triggers.Add((clip, triggerParam, control.parameter.name, control.value));
            }

            foreach (var trigger in drivenSyncParams) {
                var (clip, triggerParam, param, target) = trigger;
                triggers.Add((clip, triggerParam, param, target));
            }

            foreach (var trigger in triggers) {
                var (clip, triggerParam, param, value) = trigger;
                DriveOld(triggerParam, param, value, false);
                //var newTriggerParam = floatToDriverService.Drive(param, value, null);
                //clip.SetAap(newTriggerParam, 1);
            }
        }
    }
}