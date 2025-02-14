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
    [FeatureTitle("Set a Tag State")]
    internal class TagStateActionBuilder : ActionBuilder<TagStateAction> {
        [VFAutowired] [CanBeNull] private readonly TriggerDriverService triggerDriverService;
        
        public AnimationClip Build(TagStateAction model, string actionName) {
            var onClip = NewClip();
            if (triggerDriverService == null) return onClip;
            onClip.SetCurve("TRIGGER_DUMMY",typeof(GameObject),"TRIGGER_DUMMY",1);
            triggerDriverService.DriveTag(onClip, model.tag, model.value);
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
