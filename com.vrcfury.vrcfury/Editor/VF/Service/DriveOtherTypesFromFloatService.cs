using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Service {
    [VFService]
    internal class DriveOtherTypesFromFloatService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        private VFLayer layer;
        private VFState idle;

        private readonly Dictionary<string, (VFAInteger,int,VFTransition)> currentSettings = new Dictionary<string, (VFAInteger,int,VFTransition)>();

        public void Drive(VFAFloat input, string output, float value) {
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
                driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                    name = output,
                    value = 0
                });
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

            var name = $"{output} = {value} (from {input.Name()})";

            var state = layer.NewState(name);
            var condition = input.IsGreaterThan(0);
            offTransition.AddCondition(condition.Not());
            idle.TransitionsTo(state).When(condition.And(lastState.IsLessThan(myNumber)));
            state.TransitionsToExit().When(fx.Always());
            var myDriver = state.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            myDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                name = lastState,
                value = myNumber
            });
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
