using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Set a Toggle State")]
    internal class ToggleStateActionBuilder : ActionBuilder<ToggleStateAction> {
        [VFAutowired] [CanBeNull] private readonly TriggerDriverService triggerDriverService;
        
        public AnimationClip Build(ToggleStateAction model, string actionName) {
            var onClip = NewClip();
            if (triggerDriverService == null) return onClip;
            onClip.SetCurve("TRIGGER_DUMMY",typeof(GameObject),"TRIGGER_DUMMY",1);
            triggerDriverService.DriveToggle(onClip, model.toggle, model.value);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var col = new VisualElement();

            var row = new VisualElement().Row();
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("toggle")).FlexGrow(1));
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value")).FlexBasis(30));
            col.Add(row);

            return col;
        }
    }
}
