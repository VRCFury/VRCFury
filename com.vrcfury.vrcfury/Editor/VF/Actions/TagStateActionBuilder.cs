using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Set a Tag State")]
    internal class TagStateActionBuilder : ActionBuilder<TagStateAction> {
        [VFAutowired] [CanBeNull] private readonly AvatarManager manager;
        [VFAutowired] [CanBeNull] private readonly GlobalsService globals;
        [VFAutowired] [CanBeNull] private readonly DriveOtherTypesFromFloatService driveOtherTypesFromFloatService;
        
        public AnimationClip Build(TagStateAction model, string actionName) {
            var onClip = NewClip();

            if (globals == null) return onClip;

            if (globals.currentTriggerParam == null) {
                globals.currentTriggerParam = manager.GetFx().NewFloat(actionName + " (Param Trigger)");
                onClip.SetAap(globals.currentTriggerParam, 1);
            }

            driveOtherTypesFromFloatService.DriveTag(globals.currentTriggerParam, model.tag, model.value, globals.currentFeature);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var col = new VisualElement();

            var row = new VisualElement().Row();
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("tag")).FlexGrow(1));
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value")).FlexBasis(30));
            col.Add(row);

            return col;
        }
    }
}
