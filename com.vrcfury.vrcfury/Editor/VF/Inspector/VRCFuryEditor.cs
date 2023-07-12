using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Menu;
using VF.Model;
using VF.Model.Feature;

namespace VF.Inspector {
    

[CustomEditor(typeof(VRCFury), true)]
public class VRCFuryEditor : VRCFuryComponentEditor<VRCFury> {
    public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFury target) {
        var container = new VisualElement();

        var features = serializedObject.FindProperty("config.features");
        if (features == null) {
            container.Add(VRCFuryEditorUtils.WrappedLabel("Feature list is missing? This is a bug."));
        } else {

            var featureList = VRCFuryEditorUtils.List(features, 
                renderElement: (i, prop) => renderFeature(target.config.features[i], prop, target.gameObject),
                onPlus: () => OnPlus(features, target.gameObject),
                onEmpty: () => {
                    var c = new VisualElement();
                    VRCFuryEditorUtils.Padding(c, 10);
                    var l = VRCFuryEditorUtils.WrappedLabel(
                        "You haven't added any VRCFury features yet.\n" + 
                        "Click the + to add your first one!");
                    l.style.unityTextAlign = TextAnchor.MiddleCenter;
                    c.Add(l);
                    return c;
                }
            );
            container.Add(featureList);
        }

        var pointingToAvatar = target.gameObject.GetComponent<VRCAvatarDescriptor>() != null;
        if (pointingToAvatar) {
            var box = new Box();
            box.style.marginTop = box.style.marginBottom = 10;
            container.Add(box);

            var label = VRCFuryEditorUtils.WrappedLabel(
                "Beware: VRCFury is non-destructive, which means these features will only be visible" +
                " when you upload or if you enter the editor Play mode!");
            VRCFuryEditorUtils.Padding(box, 5);
            VRCFuryEditorUtils.BorderRadius(box, 5);
            box.Add(label);
        }

        return container;
    }

    private VisualElement renderFeature(FeatureModel model, SerializedProperty prop, GameObject gameObject) {
        return FeatureFinder.RenderFeatureEditor(prop, model, gameObject);
    }

    private void OnPlus(SerializedProperty listProp, GameObject gameObject) {
        var menu = new GenericMenu();
        foreach (var feature in FeatureFinder.GetAllFeaturesForMenu(gameObject)) {
            var editorInst = (FeatureBuilder) Activator.CreateInstance(feature.Value);
            var title = editorInst.GetEditorTitle();
            if (title != null) {
                menu.AddItem(new GUIContent(title), false, () => {
                    var modelInst = Activator.CreateInstance(feature.Key);
                    VRCFuryEditorUtils.AddToList(listProp, entry => entry.managedReferenceValue = modelInst);
                });
            }
        }
        menu.ShowAsContext();
    }
    
    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
    static void DrawGizmo(VRCFury vf, GizmoType gizmoType) {
        foreach (var g in vf.config.features.OfType<Gizmo>()) {
            var q = Quaternion.Euler(g.rotation);
            Vector3 getPoint(Vector3 input) {
                return vf.transform.TransformPoint(q * input);
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
                VRCFuryGizmoUtils.DrawSphere(worldPos, g.sphereRadius * vf.transform.lossyScale.x, Color.red);
            }
        }
    }
}

}
