using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

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
                onPlus: () => OnPlus(features, target.gameObject),
                onEmpty: () => {
                    var c = new VisualElement()
                        .Padding(10)
                        .TextAlign(TextAnchor.MiddleCenter);
                    var l = VRCFuryEditorUtils.WrappedLabel(
                        "You haven't added any VRCFury features yet.\n" + 
                        "Click the + to add your first one!");
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

            box.Add(VRCFuryEditorUtils.WrappedLabel(
                "Beware: VRCFury is non-destructive, which means these features will only be visible" +
                " when you upload or if you enter the editor Play mode!")
                .Padding(5)
                .BorderRadius(5)
            );
        }

        return container;
    }

    [CustomPropertyDrawer(typeof(VF.Model.Feature.FeatureModel))]
    public class FeatureDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            return FeatureFinder.RenderFeatureEditor(property);
        }
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

        [DrawGizmo(GizmoType.Pickable | GizmoType.NotInSelectionHierarchy | GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(VRCFury vf, GizmoType gizmoType)
        {
            foreach (var g in vf.config.features.OfType<Gizmo>())
            {
                //check if our flags match
                if ((gizmoType & g.displayFlags) == 0) continue;

                //determine if to use the gameobject's transform or the parent's
                var parent = g.parent;
                if (parent == null) parent = vf.transform;

                //convert the position offset from local to world space
                Vector3 worldPositionOffset = parent.TransformVector(g.positionOffset);

                Vector3 Position = parent.position + worldPositionOffset;
                //save old color
                var oldColor = Gizmos.color;
                //set the color
                Gizmos.color = g.color;
                switch (g.type)
                {
                    case Gizmo.GizmoDrawType.SPHERE:
                        if (!g.wireMode)
                            Gizmos.DrawSphere(Position, g.sphereRadius);
                        else
                            Gizmos.DrawWireSphere(Position, g.sphereRadius);
                        break;
                    case Gizmo.GizmoDrawType.ARROW:
                        //start calculating the tip
                        var tip = new Vector3(0, 0, g.arrowLength);
                        //rotate the tip around the position
                        tip = Quaternion.Euler(g.rotation) * tip;
                        //convert to world space
                        tip = parent.TransformPoint(tip);
                        tip += worldPositionOffset;
                        VRCFuryGizmoUtils.DrawArrow(Position, tip, g.color);
                        break;
                    case Gizmo.GizmoDrawType.TEXT:
                        VRCFuryGizmoUtils.DrawText(Position, g.text, g.color, fontSize: (int)g.fontSize);
                        break;
                    case Gizmo.GizmoDrawType.CUBE:
                        //rotate
                        var q = Quaternion.Euler(g.rotation);
                        q = parent.rotation * q;
                        Gizmos.matrix = Matrix4x4.TRS(Position, q, Vector3.one);

                        if (!g.wireMode)
                            Gizmos.DrawCube(Vector3.zero, g.scale);
                        else
                            Gizmos.DrawWireCube(Vector3.zero, g.scale);

                        Gizmos.matrix = Matrix4x4.identity;
                        break;
                    case Gizmo.GizmoDrawType.MESH:
                        //rotate
                        q = Quaternion.Euler(g.rotation);
                        q = parent.rotation * q;
                        Gizmos.matrix = Matrix4x4.TRS(Position, q, Vector3.one);

                        if (!g.wireMode)
                            Gizmos.DrawMesh(g.mesh, Vector3.zero, Quaternion.identity, g.scale);
                        else
                            Gizmos.DrawWireMesh(g.mesh, Vector3.zero, Quaternion.identity, g.scale);

                        Gizmos.matrix = Matrix4x4.identity;
                        break;
                    case Gizmo.GizmoDrawType.LINE:
                        //convert to world space
                        var start = parent.TransformPoint(g.lineStart);
                        var end = parent.TransformPoint(g.lineEnd);
                        Gizmos.DrawLine(start, end);
                        break;
                    case Gizmo.GizmoDrawType.ICON:
                        //convert the texture to the path of it
                        var path = AssetDatabase.GetAssetPath(g.texture);

                        Gizmos.DrawIcon(Position, path, true);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                //reset the color
                Gizmos.color = oldColor;
            }
        }
    }

}
