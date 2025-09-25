using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Inspector {

    [CustomEditor(typeof(VRCFury), true)]
    internal class VRCFuryEditor : VRCFuryComponentEditor<VRCFury> {
        protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFury target) {
            return VRCFuryEditorUtils.Prop(serializedObject.FindProperty("content"));
        }

        [CustomPropertyDrawer(typeof(VF.Model.Feature.FeatureModel))]
        public class FeatureDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty property) {
                return FeatureFinder.RenderFeatureEditor(property, (title, bodyContent, builderType) => {
                    var wrapper = new VisualElement();

                    wrapper.Add(VRCFuryComponentHeader.CreateHeaderOverlay(title));

                    var body = new VisualElement().AddTo(wrapper);
                    body.Add(bodyContent);
                    body.style.marginTop = 5;

                    return wrapper;
                });
            }
        }
        
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(VRCFury vf, GizmoType gizmoType) {
            foreach (var g in vf.GetAllFeatures().OfType<Gizmo>()) {
                var q = Quaternion.Euler(g.rotation);
                Vector3 getPoint(Vector3 input) {
                    return vf.owner().TransformPoint(q * input);
                }

                var worldPos = getPoint(Vector3.zero);

                if (g.arrowLength > 0) {
                    var tip = getPoint(new Vector3(0, 0, g.arrowLength));
                    VRCFuryGizmoUtils.DrawArrow(worldPos, tip, Color.red);
                }

                if (!string.IsNullOrWhiteSpace(g.text)) {
                    VRCFuryGizmoUtils.DrawText(worldPos, g.text, Color.white);
                }

                if (g.sphereRadius > 0) {
                    VRCFuryGizmoUtils.DrawSphere(worldPos, g.sphereRadius * vf.owner().worldScale.x, Color.red);
                }
            }
        }
    }

}
