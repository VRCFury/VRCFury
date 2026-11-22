using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    [VFService]
    internal class VRCFuryHapticTouchSenderBuilder {
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction]
        public void Apply() {
            foreach (var sender in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticTouchSender>()) {
                HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest() {
                    obj = sender.owner(),
                    objName = "Sender",
                    radius = sender.radius,
                    height = sender.height,
                    rotation = Quaternion.identity,
                    tags = new string[] { "Finger", "FingerR", "FingerIndex", "FingerIndexR" },
                    useWorldUnits = false
                });
            }
        }
        
        [CustomEditor(typeof(VRCFuryHapticTouchSender), true)]
        public class VRCFuryHapticTouchSenderEditor : VRCFuryComponentEditor<VRCFuryHapticTouchSender> {
            protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticTouchSender target) {
                var container = new VisualElement();
                
                container.Add(VRCFuryEditorUtils.Info(
                    "This will add an extra contact which can be used to trigger SPS/VRCFury Haptics. " +
                    "Note: This is NOT NEEDED if this area contains a VRCFury Global Collider (which can already do the same). " +
                    "This is essentially the same as adding a VRChat contact sender with the 'Finger' tag."
                ));

                container.Add(VRCFuryHapticPlugEditor.ConstraintWarning(target));
            
                container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("radius"), "Radius"));
                container.Add(VRCFuryEditorUtils.BetterProp(
                    serializedObject.FindProperty("height"),
                    "Height",
                    tooltip: "Total capsule height including both rounded ends. Set to 0 for a sphere."
                ));

                return container;
            }
        
            [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
            static void DrawGizmo(VRCFuryHapticTouchSender c, GizmoType gizmoType) {
                var owner = c.owner();
                var worldPos = owner.worldPosition;
                var scale = owner.worldScale;
                var worldScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                VRCFuryGizmoUtils.DrawCapsule(
                    worldPos,
                    owner.worldRotation,
                    worldScale * c.height,
                    worldScale * c.radius,
                    Color.blue
                );
                VRCFuryGizmoUtils.DrawText(worldPos, "Touch Sender", Color.white, true);
            }
        }
    }
}
