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
    [FeatureTitle("Set a Synced Param")]
    internal class SyncParamActionBuilder : ActionBuilder<SyncParamAction> {
        [VFAutowired] [CanBeNull] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] [CanBeNull] private readonly GlobalsService globals;
        [VFAutowired] [CanBeNull] private readonly TriggerDriverService driveOtherTypesFromFloatService;
        
        public AnimationClip Build(SyncParamAction model, string actionName) {
            var onClip = NewClip();

            if (globals == null) return onClip;

            if (globals.currentTriggerParam == null) {
                globals.currentTriggerParam = fx.NewFloat(actionName + " (Param Trigger)");
                onClip.SetAap(globals.currentTriggerParam, 1);
            }
            onClip.SetCurve("TRIGGER_DUMMY",typeof(GameObject),"TRIGGER_DUMMY",1);
            driveOtherTypesFromFloatService.DriveSyncParam(onClip, globals.currentTriggerParam, model.param, model.value);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var col = new VisualElement();

            var row = new VisualElement().Row();
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("param")).FlexGrow(1));
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value")).FlexBasis(30));
            col.Add(row);

            return col;
        }
    }
}
