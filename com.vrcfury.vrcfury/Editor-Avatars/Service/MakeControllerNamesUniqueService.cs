using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Unity's animator tooling can sometimes resolve the wrong state when duplicate layer or sibling state names exist.
     * This pass makes layer names unique within a controller, and state names unique within each state machine,
     * immediately before controller finalization.
     */
    [VFService]
    internal class MakeControllerNamesUniqueService {
        [VFAutowired] private readonly ControllersService controllers;

        [FeatureBuilderAction(FeatureOrder.MakeControllerNamesUnique)]
        public void Apply() {
            foreach (var controller in controllers.GetAllUsedControllers()) {
                MakeLayerNamesUnique(controller);
                foreach (var layer in controller.GetLayers()) {
                    MakeStateNamesUnique(layer);
                }
            }
        }

        private static void MakeLayerNamesUnique(VFController controller) {
            var existingNames = new List<string>();
            var changed = false;
            controller.EditRawLayers(layers => {
                for (var i = 0; i < layers.Length; i++) {
                    var layer = layers[i];
                    var uniqueName = ObjectNames.GetUniqueName(existingNames.ToArray(), layer.name);
                    if (layer.name != uniqueName) {
                        layer.name = uniqueName;
                        changed = true;
                    }
                    existingNames.Add(layer.name);
                }
                return changed;
            });
        }

        private static void MakeStateNamesUnique(VFLayer layer) {
            foreach (var stateMachine in layer.allStateMachines) {
                MakeStateNamesUnique(stateMachine);
            }
        }

        private static void MakeStateNamesUnique(VFStateMachine stateMachine) {
            var existingNames = new List<string>();
            var rawStates = stateMachine.states.Select(state => state.behaviourContainer as AnimatorState).ToArray();
            foreach (var state in rawStates) {
                if (state == null) continue;
                var uniqueName = ObjectNames.GetUniqueName(existingNames.ToArray(), state.name);
                if (state.name != uniqueName) {
                    state.name = uniqueName;
                }
                existingNames.Add(state.name);
            }
        }
    }
}
