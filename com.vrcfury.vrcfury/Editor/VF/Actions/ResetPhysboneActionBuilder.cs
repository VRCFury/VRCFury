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
    [FeatureTitle("Reset Physbone")]
    [FeatureHideTitleInEditor]
    internal class ResetPhysboneActionBuilder : ActionBuilder<ResetPhysboneAction> {
        [VFAutowired] [CanBeNull] private readonly PhysboneResetService physboneResetService;

        public AnimationClip Build(ResetPhysboneAction model, string actionName, bool useServices) {
            var onClip = NewClip();
            if (model.physBone != null && physboneResetService != null && useServices) {
                var param = physboneResetService.CreatePhysBoneResetter(model.physBone.owner(), actionName);
                onClip.SetAap(param, 1);
            }
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var row = new VisualElement().Row();
            row.Add(VRCFuryActionDrawer.Title("Reset Physbone").FlexBasis(100));
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("physBone")).FlexGrow(1));
            return row;
        }
    }
}
