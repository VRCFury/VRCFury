using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;

namespace VF.Inspector {
    [VFService]
    public class VRCFuryHapticTouchSenderBuilder {
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly UniqueHapticNamesService uniqueHapticNamesService;

        [FeatureBuilderAction]
        public void Apply() {
            foreach (var sender in manager.AvatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticTouchSender>()) {
                hapticContacts.AddSender(
                    sender.owner(),
                    Vector3.zero,
                    "Sender",
                    sender.radius,
                    new string[] { "Finger" },
                    worldScale: false
                );
            }
        }
        
        [CustomEditor(typeof(VRCFuryHapticTouchSender), true)]
        public class VRCFuryHapticTouchSenderEditor : VRCFuryComponentEditor<VRCFuryHapticTouchSender> {
            public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticTouchSender target) {
                var container = new VisualElement();
                
                container.Add(VRCFuryEditorUtils.Info(
                    "This will add an extra contact which can be used to trigger SPS/VRCFury Haptics. " +
                    "Note: This is NOT NEEDED if this area contains a VRCFury Global Collider (which can already do the same)."
                ));

                container.Add(VRCFuryHapticPlugEditor.ConstraintWarning(target.gameObject));
            
                container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("radius"), "Radius"));

                return container;
            }
        
            [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
            static void DrawGizmo(VRCFuryHapticTouchSender c, GizmoType gizmoType) {
                var worldPos = c.owner().worldPosition;
                var worldScale = c.owner().worldScale.x;
                VRCFuryGizmoUtils.DrawSphere(worldPos, worldScale * c.radius, Color.blue);
                VRCFuryGizmoUtils.DrawText(worldPos, "Touch Sender", Color.white, true);
            }
        }
    }
}