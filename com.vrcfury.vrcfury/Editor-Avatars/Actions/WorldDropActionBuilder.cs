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
    [FeatureTitle("World Drop")]
    [FeatureHideTitleInEditor]
    internal class WorldDropActionBuilder : ActionBuilder<WorldDropAction> {
        [VFAutowired] [CanBeNull] private readonly WorldDropService worldDropService;
        
        public AnimationClip Build(WorldDropAction model, string actionName) {
            var onClip = NewClip();
            if (model.obj != null && worldDropService != null) {
                var param = worldDropService.Add(model.obj, actionName);
                // Doing this through an AAP will delay the drop by one frame, but that's exactly what we want
                // because we want Turn On toggles to activate first (to reset the position) before the drop happens!
                onClip.SetAap(param, 1);
            }
            return onClip;
        }
        
        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var row = new VisualElement().Row();
            row.Add(VRCFuryActionDrawer.Title("World Drop").FlexBasis(100));
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj")).FlexGrow(1));
            return row;
        }
    }
}
