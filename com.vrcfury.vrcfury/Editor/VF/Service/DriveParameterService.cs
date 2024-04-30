using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Utils.Controller;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

namespace VF.Service {
    /**
     * Creates a physbone resetter that can be triggered by triggering the returned bool
     */
    [VFService]
    public class DriveParameterService {
        [VFAutowired] private AvatarManager avatarManager;
        [VFAutowired] private readonly GlobalsService globals;

        private List<(VFState, string, float)> paramTriggers = new ();
        private List<(VFState, string, float)> toggleTriggers = new ();
        private List<(VFState, string, float, FeatureBuilder)> tagTriggers = new ();

        public void CreateParamTrigger(VFState state, string param, float target) {
            paramTriggers.Add((state, param, target));
        }

        public void CreateToggleTrigger(VFState state, string toggle, float target) {
            toggleTriggers.Add((state, toggle, target));
        }

        public void CreateTagTrigger(VFState state, string tag, float target, FeatureBuilder feature = null) {
            tagTriggers.Add((state, tag, target, feature));
        }

        public void ApplyTriggers() {
            
            List<(VFState, string, float)> triggers = new();
            foreach (var trigger in tagTriggers) {
                var (state, tag, target, feature) = trigger;
                foreach (var other in globals.allBuildersInRun
                     .OfType<ToggleBuilder>()
                     .Where(b => b != feature)) {
                        var otherTags = other.GetExclusiveTags();
                        
                        if (otherTags.Contains(tag)) {
                            if (target == 0) triggers.Add((state, other.getParam(), 0));
                            else triggers.Add((state, other.getParam(), other.model.slider ? target : 1));
                        }

                }
            }

            foreach (var trigger in toggleTriggers) {
                var (state, path, target) = trigger;
                var control = avatarManager.GetMenu().GetMenuItem(path);

                if (target == 0) triggers.Add((state, control.parameter.name, 0));
                else if (control.type == ControlType.RadialPuppet) triggers.Add((state, control.parameter.name, target));
                else triggers.Add((state, control.parameter.name, control.value));
            }

            foreach (var trigger in paramTriggers) {
                triggers.Add(trigger);
            }

            foreach (var trigger in triggers) {
                var (state, param, value) = trigger;

                state.Drives(param, value);
            }
        }
            
    }
}
