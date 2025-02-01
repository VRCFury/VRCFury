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
    internal class FloatToDriverService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        private ControllerManager fx => controllers.GetFx();

        private BlendtreeMath math;
        private readonly Dictionary<string, VFLayer> driveLayers = new Dictionary<string, VFLayer>();

        public VFAFloat Drive(string output, float? onValue, float? offValue) {
            if (math == null) {
                var dbt = dbtLayerService.Create("FloatToDriverService");
                math = dbtLayerService.GetMath(dbt);
            }
            var control = fx.NewFloat($"Drive {output} to {onValue}/{offValue}");
            var buffer = math.Buffer(control);

            if (!driveLayers.TryGetValue(output, out var layer)) {
                layer = fx.NewLayer($"FloatToDriverService - {output}");
                layer.NewState("Idle");
                layer.SetNextOffset(1, 0);
                driveLayers[output] = layer;
            }

            void MakeLastAnyTransitionFirst() {
                var oldAny = layer.GetRawStateMachine().anyStateTransitions;
                layer.GetRawStateMachine().anyStateTransitions = new[] { oldAny.Last() }
                    .Concat(oldAny.SkipLast(1))
                    .ToArray();
            }

            if (offValue.HasValue) {
                var state = layer.NewState($"Set to {offValue}");
                state.Drives(output, offValue.Value);
                state.TransitionsFromAny().When(control.IsLessThanOrEquals(0).And(buffer.IsGreaterThan(0)));
                MakeLastAnyTransitionFirst();
            }
            if (onValue.HasValue) {
                var state = layer.NewState($"Set to {onValue}");
                state.Drives(output, onValue.Value);
                state.TransitionsFromAny().When(control.IsGreaterThan(0).And(buffer.IsLessThanOrEquals(0)));
                MakeLastAnyTransitionFirst();
            }

            return control;
        }
    }
}
