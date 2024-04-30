using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils.Controller;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

namespace VF.Service {
    /**
     * Creates a physbone resetter that can be triggered by triggering the returned bool
     */
    [VFService]
    public class DriveParameterService {
        [VFAutowired] private AvatarManager manager;
        [VFAutowired] private readonly GlobalsService globals;

        private List<(AnimationClip, string, float)> paramTriggers = new ();
        private List<(AnimationClip, string, float)> toggleTriggers = new ();
        private List<(AnimationClip, string, float, FeatureBuilder)> tagTriggers = new ();

        public void CreateParamTrigger(AnimationClip clip, string param, float target) {
            paramTriggers.Add((clip, param, target));
        }

        public void CreateToggleTrigger(AnimationClip clip, string toggle, float target) {
            toggleTriggers.Add((clip, toggle, target));
        }

        public void CreateTagTrigger(AnimationClip clip, string tag, float target, FeatureBuilder feature = null) {
            tagTriggers.Add((clip, tag, target, feature));
        }

        public void ApplyTriggers() {

            Dictionary<Motion, VFState> states = new();

            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var layer in c.GetLayers()) {
                    var stateMachine = layer.stateMachine;
                    foreach (var state in stateMachine.states) {
                        if (state.state == null || state.state.motion == null) continue;
                        states[state.state.motion] = new VFState(state, stateMachine);
                    }
                }
            }
            
            List<(VFState, string, float)> triggers = new();
            foreach (var trigger in tagTriggers) {
                var (clip, tag, target, feature) = trigger;
                if (!states.Keys.Contains(clip)) continue;
                foreach (var other in globals.allBuildersInRun
                     .OfType<ToggleBuilder>()
                     .Where(b => b != feature)) {
                        var otherTags = other.GetTags();
                        
                        if (otherTags.Contains(tag)) {
                            if (target == 0) triggers.Add((states[clip], other.getParam(), 0));
                            else triggers.Add((states[clip], other.getParam(), other.model.slider ? target : 1));
                        }

                }
            }

            foreach (var trigger in toggleTriggers) {
                var (clip, path, target) = trigger;
                if (!states.Keys.Contains(clip)) continue;
                var control = manager.GetMenu().GetMenuItem(path);
                if (control == null) continue;
                if (target == 0) triggers.Add((states[clip], control.parameter.name, 0));
                else if (control.type == ControlType.RadialPuppet) triggers.Add((states[clip], control.parameter.name, target));
                else triggers.Add((states[clip], control.parameter.name, control.value));
            }

            foreach (var trigger in paramTriggers) {
                var (clip, param, target) = trigger;
                if (!states.Keys.Contains(clip)) continue;
                triggers.Add((states[clip], param, target));
            }

            foreach (var trigger in triggers) {
                var (state, param, value) = trigger;

                state.Drives(param, value);
            }
        }
            
    }
}
