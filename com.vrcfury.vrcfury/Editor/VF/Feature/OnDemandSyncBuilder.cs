using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Editor.VF.Feature {
    public class OnDemandSyncBuilder : FeatureBuilder<OnDemandSync> {
        [VFAutowired] private readonly OnDemandSyncService onDemandSyncService;

        [FeatureBuilderAction]
        public void Apply() {
            if (model.allRadialControls) {
                foreach (var param in GetAllRadialPuppetParameters()) {
                    onDemandSyncService.SetParameterOnDemandSync(param);
                }
            }
            foreach (var param in model.parameters) {
                var vrcParam = manager.GetParams().GetParam(param);
                if(vrcParam == null) continue;
                if(vrcParam.valueType != VRCExpressionParameters.ValueType.Float) continue;
                var controllerParam = GetFx().GetRaw().GetParam(param);
                if(controllerParam == null) continue;
                onDemandSyncService.SetParameterOnDemandSync(new VFAFloat(controllerParam));
            }
        }

        private IEnumerable<VFAFloat> GetAllRadialPuppetParameters() {
            var floatParams = new List<VFAFloat>();
            manager.GetMenu().GetRaw().ForEachMenu(ForEachItem: (control, list) => {
                if (control.type != VRCExpressionsMenu.Control.ControlType.RadialPuppet)
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var controlParam = control.GetSubParameter(0).name;
                var vrcParam = manager.GetParams().GetParam(controlParam);
                if (vrcParam is not { networkSynced: true })
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var animParam = GetFx().GetRaw().GetParam(control.GetSubParameter(0).name);
                if(animParam != null) floatParams.Add(new VFAFloat(animParam));
                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            return floatParams;
        }
        
        public override string GetEditorTitle() {
            return "On Demand Sync";
        }
        
        public override bool OnlyOneAllowed() {
            return true;
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will optimize float parameters on radial toggles into a single" +
                " 16 bits pointer and data field combination to sync the parameters on change."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("allRadialControls"), "All radial controls"));
            content.Add(VRCFuryEditorUtils.WrappedLabel("Float parameters"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("parameters")));
            return content;
        }
    }
}