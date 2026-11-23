using System.Collections.Generic;
using System.Linq;
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
    internal class VRCFuryHapticTouchReceiverBuilder {
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction]
        public void Apply() {
            var desktop = BuildTargetUtils.IsDesktop();
            var usedNames = new HashSet<string>();
            foreach (var receiver in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticTouchReceiver>()) {
                var obj = receiver.owner();
                PhysboneUtils.RemoveFromPhysbones(obj);
                if (!desktop) continue;

                var name = HapticUtils.MakeUniqueOgbId(usedNames, HapticUtils.GetPreferredId(receiver, r => r.name, _ => HapticUtils.GetFallbackId(obj)));
                var paramPrefix = "OGB/Touch/" + name;

                void AddReceiver(HapticUtils.ReceiverParty party, IEnumerable<string> tags) {
                    hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest {
                        obj = obj,
                        paramName = paramPrefix + "/" + party,
                        objName = party.ToString(),
                        radius = receiver.radius,
                        height = receiver.height,
                        rotation = Quaternion.identity,
                        tags = tags.Concat(new[] { SpsTags.PlugClose }).ToArray(),
                        party = party,
                        worldScale = false,
                        usePrefix = false,
                        localOnly = true
                    });
                }

                AddReceiver(HapticUtils.ReceiverParty.Self, HapticUtils.SelfContacts);
                AddReceiver(HapticUtils.ReceiverParty.Others, HapticUtils.BodyContacts);
            }
        }

        [CustomEditor(typeof(VRCFuryHapticTouchReceiver), true)]
        public class VRCFuryHapticTouchReceiverEditor : VRCFuryComponentEditor<VRCFuryHapticTouchReceiver> {
            protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticTouchReceiver target) {
                var container = new VisualElement();
                
                container.Add(VRCFuryEditorUtils.Info(
                    "This will add an extra SPS Touch Zone (for purposes unrelated to Plugs / Sockets), which will activate OGB haptics when touched. " +
                    "Haptic level increases toward the center of the zone. " +
                    "This touch zone can be activated by Hands, Fingers, Feet, SPS Plugs, VRCFury Touch Senders, and Heads (other players only)."));

                container.Add(VRCFuryHapticPlugEditor.ConstraintWarning(target));
                container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("radius"), "Radius"));
                container.Add(VRCFuryEditorUtils.BetterProp(
                    serializedObject.FindProperty("height"),
                    "Height",
                    tooltip: "Total capsule height including both rounded ends. Set to 0 for a sphere."
                ));
                container.Add(SpsEditorUtils.AutoHapticIdProp(
                    serializedObject.FindProperty("name"),
                    "ID sent to OGB",
                    target,
                    target.owner(),
                    avatar => avatar.GetComponentsInSelfAndChildren<VRCFuryHapticTouchReceiver>(),
                    receiver => HapticUtils.GetPreferredId(
                        receiver,
                        r => r.name,
                        r => HapticUtils.GetFallbackId(r.owner())
                    )
                ));

                return container;
            }
        
            [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
            static void DrawGizmo(VRCFuryHapticTouchReceiver c, GizmoType gizmoType) {
                var owner = c.owner();
                var worldPos = owner.worldPosition;
                var scale = owner.worldScale;
                var worldScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                VRCFuryGizmoUtils.DrawCapsule(
                    worldPos,
                    owner.worldRotation,
                    worldScale * c.height,
                    worldScale * c.radius,
                    Color.red
                );
                VRCFuryGizmoUtils.DrawText(worldPos, "SPS Touch Zone", Color.white, true);
            }
        }
    }
}
