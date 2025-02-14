using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

namespace VF.Service {
    [VFService]
    internal class TriggerDriverService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly FloatToDriverService floatToDriverService;
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();

        private readonly List<(AnimationClip,string,float)> drivenSyncParams = new ();
        private readonly List<(AnimationClip,string,float)> drivenToggles = new ();
        private readonly List<(AnimationClip,string,float,FeatureBuilder)> drivenTags = new ();

        public void DriveSyncParam(AnimationClip clip, string param, float value) {
            drivenSyncParams.Add((clip, param, value));
        }

        public void DriveToggle(AnimationClip clip, string toggle, float value) {
            drivenToggles.Add((clip, toggle, value));
        }

        public void DriveTag(AnimationClip clip, string tag, float value) {
            drivenTags.Add((clip, tag, value, globals.currentFeature()));
        }

        [FeatureBuilderAction(FeatureOrder.EvaluateTriggerParams)]
        public void DriveNonFloatTypes() {
            List<(AnimationClip, string, float)> triggers = new();
            foreach (var trigger in drivenTags) {
                var (clip, tag, target, feature) = trigger;
                foreach (var other in globals.allBuildersInRun
                     .OfType<ToggleBuilder>()
                     .Where(b => b != feature)) {
                        var otherTags = other.GetTags();
                        
                        if (otherTags.Contains(tag)) {
                            if (target == 0) triggers.Add((clip, other.getParam(), 0));
                            else triggers.Add((clip, other.getParam(), other.model.slider ? target : 1));
                        }
                }
            }

            foreach (var trigger in drivenToggles) {
                var (clip, path, target) = trigger;
                var control = menu.GetMenuItem(path);
                if (control == null) continue;
                if (target == 0) triggers.Add((clip, control.parameter.name, 0));
                else if (control.type == ControlType.RadialPuppet) triggers.Add((clip, control.parameter.name, target));
                else triggers.Add((clip, control.parameter.name, control.value));
            }

            foreach (var trigger in drivenSyncParams) {
                var (clip, param, target) = trigger;
                triggers.Add((clip, param, target));
            }

            foreach (var trigger in triggers) {
                var (clip, param, value) = trigger;
                var newTriggerParam = floatToDriverService.Drive(param, value, null);
                clip.SetAap(newTriggerParam, 1);
            }
        }
    }
}