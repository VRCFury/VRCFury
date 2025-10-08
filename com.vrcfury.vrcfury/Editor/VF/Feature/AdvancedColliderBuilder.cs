using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [FeatureTitle("Advanced Collider")]
    [FeatureRootOnly]
    internal class AdvancedColliderBuilder : FeatureBuilder<AdvancedCollider> {

        [VFAutowired] private AvatarColliderService avatarColliderService;
        
        private static readonly List<string> availableColliders = typeof(VRCAvatarDescriptor).GetFields()
            .Where(f => f.FieldType == typeof(VRCAvatarDescriptor.ColliderConfig))
            .Select(f => f.Name.Replace("collider_", ""))
            .ToList();

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("radius"), "Radius"));
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("height"), "Height"));
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rootTransform"), "Transform"));
            var row = new VisualElement().Row();
            var nameProp = prop.FindPropertyRelative("colliderName");
            row.Add(VRCFuryEditorUtils.Prop(nameProp, "Collider").FlexGrow(1));
            var button = new Button(() => {
                var menu = new GenericMenu();
                foreach (var name in availableColliders) {
                    menu.AddItem(new GUIContent(name), false, () => {
                        nameProp.stringValue = name;
                        nameProp.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.ShowAsContext();
            }) {
                text = "Select"
            };
            row.Add(button);
            c.Add(row);

            var fingerWarning = VRCFuryEditorUtils.Warn(
                "If you are trying to override a finger collider, you should use a VRCFury Global Collider component instead!" +
                " It will automatically select an appropriate available finger, and can be used within prefabs."
            );
            c.Add(fingerWarning);
            void UpdateFingerWarning() {
                fingerWarning.SetVisible(nameProp.stringValue.Contains("finger"));
            }
            UpdateFingerWarning();
            c.Add(VRCFuryEditorUtils.OnChange(nameProp, UpdateFingerWarning));

            return c;
        }

        [FeatureBuilderAction(FeatureOrder.AdvancedColliders)]
        public void Apply() {
            if (model.rootTransform == null) {
                throw new Exception("Transform field is not set");
            }
            avatarColliderService.CustomizeCollider(
                "collider_" + model.colliderName,
                model.rootTransform,
                model.radius,
                model.height
            );
        }
    }
}
