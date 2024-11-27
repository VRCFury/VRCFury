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
    internal class DriveOtherTypesFromFloatService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        private VFLayer layer;
        private VFState idle;

        private readonly Dictionary<string, (VFAInteger,int,VFTransition)> currentSettings = new Dictionary<string, (VFAInteger,int,VFTransition)>();
        private readonly Dictionary<VFAFloat, VFState> createdStates = new Dictionary<VFAFloat, VFState>();

        private readonly List<(VFAFloat,string,float)> drivenSyncParams = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float)> drivenToggles = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float,FeatureBuilder)> drivenTags = new List<(VFAFloat,string,float,FeatureBuilder)>();

        public void DriveSyncParam(VFAFloat src, string param, float value) {
            drivenSyncParams.Add((src, param, value));
        }

        public void DriveToggle(VFAFloat src, string toggle, float value) {
            drivenToggles.Add((src, toggle, value));
        }

        public void DriveTag(VFAFloat src, string tag, float value, FeatureBuilder originFeature) {
            drivenTags.Add((src, tag, value, originFeature));
        }

        public void Drive(VFAFloat input, string output, float value, bool reset = true) {
            if (layer == null) {
                layer = fx.NewLayer("Cross-Type Param Driver");
                idle = layer.NewState("Idle");
            }

            if (!currentSettings.ContainsKey(output)) {
                var lastState_ = fx.NewInt($"{output}_lastState");
                var off = layer.NewState($"{output} = 0");
                off.TransitionsToExit().When(fx.Always());
                var driver = off.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                var t = idle.TransitionsTo(off).When(lastState_.IsGreaterThan(0));
                driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                    name = lastState_,
                    value = 0
                });
                if (reset) {
                    driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                        name = output,
                        value = 0
                    });
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

                var lastStateDriver = state.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                lastStateDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                    name = lastState.Name(),
                    value = myNumber
                });

                createdStates[input] = state;
            } else {
                var rawState = createdStates[input].GetRaw();
                //rawState.name = rawState.name.Insert(rawState.name.IndexOf(" (from"),  $", {output} = {value}");
            }
            var myDriver = createdStates[input].GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            myDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                name = output,
                value = value
            });
        }
        
        private readonly List<(VFAFloat,string,float)> drivenParams = new List<(VFAFloat,string,float)>();

        public void DriveAutoLater(VFAFloat input, string output, float value) {
            drivenParams.Add((input, output, value));
        }

        [FeatureBuilderAction(FeatureOrder.DriveNonFloatTypes)]
        public void DriveNonFloatTypes() {
            var nonFloatParams = new HashSet<string>();
            foreach (var c in controllers.GetAllUsedControllers()) {
                nonFloatParams.UnionWith(c.GetRaw().parameters
                    .Where(p => p.type != AnimatorControllerParameterType.Float || c.GetType() != VRCAvatarDescriptor.AnimLayerType.FX)
                    .Select(p => p.name));
            }

            List<(VFAFloat, string, float)> triggers = new();
            foreach (var trigger in drivenTags) {
                var (param, tag, target, feature) = trigger;
                foreach (var other in globals.allBuildersInRun
                     .OfType<ToggleBuilder>()
                     .Where(b => b != feature)) {
                        var otherTags = other.GetTags();
                        
                        if (otherTags.Contains(tag)) {
                            if (target == 0) triggers.Add((param, other.getParam(), 0));
                            else triggers.Add((param, other.getParam(), other.model.slider ? target : 1));
                        }
                }
            }

            foreach (var trigger in drivenToggles) {
                var (param, path, target) = trigger;
                var control = menu.GetMenuItem(path);
                if (control == null) continue;
                if (target == 0) triggers.Add((param, control.parameter.name, 0));
                else if (control.type == ControlType.RadialPuppet) triggers.Add((param, control.parameter.name, target));
                else triggers.Add((param, control.parameter.name, control.value));
            }

            foreach (var trigger in drivenSyncParams) {
                var (triggerParam, param, target) = trigger;
                triggers.Add((triggerParam, param, target));
            }

            foreach (var trigger in triggers) {
                var (triggerParam, param, value) = trigger;
                Drive(triggerParam, param, value, false);
            }

            var rewrites = new Dictionary<string, string>();
            foreach (var (floatParam,targetParam,onValue) in drivenParams) {
                if (nonFloatParams.Contains(targetParam)) {
                    Drive(floatParam, targetParam, onValue);
                } else {
                    rewrites.Add(floatParam, targetParam);
                }
            }

            if (rewrites.Count > 0) {
                foreach (var c in controllers.GetAllUsedControllers()) {
                    c.GetRaw().RewriteParameters(from =>
                        rewrites.TryGetValue(from, out var to) ? to : from
                    );
                }
            }
        }
    }
}
