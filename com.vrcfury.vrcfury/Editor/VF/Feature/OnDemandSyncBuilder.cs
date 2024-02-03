using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Editor.VF.Feature {
    public class OnDemandSyncBuilder : FeatureBuilder<OnDemandSync> {
        [VFAutowired] private readonly OnDemandSyncService onDemandSyncService;

        [FeatureBuilderAction]
        public void Apply() {
            foreach (var param in model.parameters) {
                var vrcParam = manager.GetParams().GetParam(param);
                if(vrcParam == null) continue;
                if(vrcParam.valueType != VRCExpressionParameters.ValueType.Float) continue;
                var controllerParam = GetFx().GetRaw().GetParam(param);
                if(controllerParam == null) continue;
                onDemandSyncService.SetParameterOnDemandSync(new VFAFloat(controllerParam));
            }
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
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("parameters")));
            return content;
        }
    }
}