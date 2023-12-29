using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class GizmoBuilder : FeatureBuilder<Gizmo> {
        [FeatureBuilderAction]
        public void Apply() {
        }
        
        public override string GetEditorTitle() {
            return "Gizmo";
        }

        public override VisualElement CreateEditor(SerializedProperty prop)
        {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This adds an editor gizmo to the current object. Informational only, no changes are made to the avatar."));

            //show the generic gizmo properties
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("color"), "Color"));

            var EnumFlagField = new EnumFlagsField("Display Flags", (GizmoType)prop.FindPropertyRelative("displayFlags").intValue);

            //when the value changes, convert back to an int and save
            EnumFlagField.RegisterValueChangedCallback(evt =>
            {
                prop.FindPropertyRelative("displayFlags").intValue = (int)(GizmoType)evt.newValue;
                //apply the changes
                prop.serializedObject.ApplyModifiedProperties();
            });

            //show the display flags as a list of checkboxes
            content.Add(EnumFlagField);

            //show the parent field
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("parent"), "Parent"));

            //show the position field
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("positionOffset"), "Position Offset"));

            var typeProp = prop.FindPropertyRelative("type");

            content.Add(VRCFuryEditorUtils.Prop(typeProp, "Type"));

            content.Add(VRCFuryEditorUtils.RefreshOnChange(() =>
            {
                var typeSpecificContent = new VisualElement();

                var wireModeContent = new VisualElement();
                wireModeContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("wireMode"), "Wire Frame"));

                //make containers for each type of gizmo
                #region Type Specific Content
                var textSpecificContent = new VisualElement();
                textSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("text"), "Text"));
                textSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("fontSize"), "Font Size"));
                typeSpecificContent.Add(textSpecificContent);

                var arrowSpecificContent = new VisualElement();
                arrowSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("arrowLength"), "Arrow Length"));
                arrowSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rotation"), "Rotation"));
                typeSpecificContent.Add(arrowSpecificContent);

                var sphereSpecificContent = new VisualElement();
                sphereSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("sphereRadius"), "Sphere Radius"));
                sphereSpecificContent.Add(wireModeContent);
                typeSpecificContent.Add(sphereSpecificContent);

                var cubeSpecificContent = new VisualElement();
                cubeSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("scale"), "Scale"));
                cubeSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rotation"), "Rotation"));
                cubeSpecificContent.Add(wireModeContent);
                typeSpecificContent.Add(cubeSpecificContent);

                var meshSpecificContent = new VisualElement();
                meshSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("mesh"), "Mesh"));
                meshSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("scale"), "Scale"));
                meshSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rotation"), "Rotation"));
                meshSpecificContent.Add(wireModeContent);
                typeSpecificContent.Add(meshSpecificContent);

                var lineSpecificContent = new VisualElement();
                lineSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("lineStart"), "Line Start"));
                lineSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("lineEnd"), "Line End"));
                typeSpecificContent.Add(lineSpecificContent);

                var iconSpecificContent = new VisualElement();
                iconSpecificContent.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("texture"), "Texture"));
                typeSpecificContent.Add(iconSpecificContent);
                #endregion

                //set all to invisible
                textSpecificContent.style.display = DisplayStyle.None;
                arrowSpecificContent.style.display = DisplayStyle.None;
                sphereSpecificContent.style.display = DisplayStyle.None;
                cubeSpecificContent.style.display = DisplayStyle.None;
                meshSpecificContent.style.display = DisplayStyle.None;
                lineSpecificContent.style.display = DisplayStyle.None;
                iconSpecificContent.style.display = DisplayStyle.None;

                //toggle the visibility based on the type
                switch ((Gizmo.GizmoDrawType)typeProp.intValue)
                {
                    case Gizmo.GizmoDrawType.TEXT:
                        textSpecificContent.style.display = DisplayStyle.Flex;
                        break;
                    case Gizmo.GizmoDrawType.ARROW:
                        arrowSpecificContent.style.display = DisplayStyle.Flex;
                        break;
                    case Gizmo.GizmoDrawType.SPHERE:
                        sphereSpecificContent.style.display = DisplayStyle.Flex;
                        break;
                    case Gizmo.GizmoDrawType.CUBE:
                        cubeSpecificContent.style.display = DisplayStyle.Flex;
                        break;
                    case Gizmo.GizmoDrawType.MESH:
                        meshSpecificContent.style.display = DisplayStyle.Flex;
                        break;
                    case Gizmo.GizmoDrawType.LINE:
                        lineSpecificContent.style.display = DisplayStyle.Flex;
                        break;
                    case Gizmo.GizmoDrawType.ICON:
                        iconSpecificContent.style.display = DisplayStyle.Flex;
                        break;
                }

                return typeSpecificContent;
            }, typeProp));
            return content;
        }
    }
}
