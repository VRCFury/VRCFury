using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
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

        private List<(VFAFloat, string, float)> paramTriggers = new ();
        private List<(VFAFloat, string, float)> toggleTriggers = new ();
        private List<(VFAFloat, string, float, FeatureBuilder)> tagTriggers = new ();

        public void CreateParamTrigger(VFAFloat triggerParam, string param, float target) {
            paramTriggers.Add((triggerParam, param, target));
        }

        public void CreateToggleTrigger(VFAFloat triggerParam, string toggle, float target) {
            toggleTriggers.Add((triggerParam, toggle, target));
        }

        public void CreateTagTrigger(VFAFloat triggerParam, string tag, float target, FeatureBuilder feature = null) {
            tagTriggers.Add((triggerParam, tag, target, feature));
        }

        public void ApplyTriggers() {
            
            List<(VFAFloat, string, float)> triggers = new();
            foreach (var trigger in tagTriggers) {
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

            foreach (var trigger in toggleTriggers) {
                var (param, path, target) = trigger;
                var control = manager.GetMenu().GetMenuItem(path);
                if (control == null) continue;
                if (target == 0) triggers.Add((param, control.parameter.name, 0));
                else if (control.type == ControlType.RadialPuppet) triggers.Add((param, control.parameter.name, target));
                else triggers.Add((param, control.parameter.name, control.value));
            }

            foreach (var trigger in paramTriggers) {
                var (triggerParam, param, target) = trigger;
                triggers.Add((triggerParam, param, target));
            }

            if (triggers.Count() == 0) return;

            var fx = manager.GetFx();
            var layer = fx.NewLayer("Drive Triggers");
            var start = layer.NewState("Start");

            Dictionary<VFAFloat, VFState> states = new ();

            foreach (var trigger in triggers) {
                var (triggerParam, param, value) = trigger;

                if (!states.ContainsKey(triggerParam)) {
                    var clip = VrcfObjectFactory.Create<AnimationClip>();
                    clip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), triggerParam.Name()), 0);
                    var state = layer.NewState(triggerParam.Name()).WithAnimation(clip);
                    start.TransitionsTo(state).When(triggerParam.IsGreaterThan(0.5f));
                    state.TransitionsToExit().When(triggerParam.IsLessThan(0.5f));
                    states[triggerParam] = state;
                }

                states[triggerParam].Drives(param, value);
            }
        }
            
    }
}
