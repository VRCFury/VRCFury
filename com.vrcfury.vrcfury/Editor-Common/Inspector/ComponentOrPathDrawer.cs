using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Utils;
using VRC.SDKBase;

namespace VF.Inspector {
    [CustomPropertyDrawer(typeof(ComponentOrPath), true)]
    internal class ComponentOrPathDrawer : PropertyDrawer {
        internal static void SetUsePath(SerializedProperty property, bool value) {
            property.FindPropertyRelative("usePath").boolValue = value;
            if (value) property.FindPropertyRelative("component").objectReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        }

        public static GameObject GetAvatarRoot(SerializedProperty property) {
            var owner = property.serializedObject.targetObject as UnityEngine.Component;
            var avatar = owner == null ? null : owner.GetComponentInParent<VRC_AvatarDescriptor>(true);
            return avatar == null ? null : avatar.gameObject;
        }

        public static UnityEngine.Component Resolve(SerializedProperty property, GameObject avatarRoot) {
            if (!property.FindPropertyRelative("usePath").boolValue)
                return property.FindPropertyRelative("component").objectReferenceValue as UnityEngine.Component;
            var path = property.FindPropertyRelative("path").stringValue;
            var target = avatarRoot == null ? null : string.IsNullOrEmpty(path)
                ? avatarRoot.transform : avatarRoot.transform.Find(path);
            return target == null ? null : target.GetComponent(((ComponentOrPath)property.GetObject()).ComponentType);
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var usePath = property.FindPropertyRelative("usePath");
            var component = property.FindPropertyRelative("component");
            var path = property.FindPropertyRelative("path");
            var type = ((ComponentOrPath)property.GetObject()).ComponentType;
            return VRCFuryEditorUtils.RefreshOnChange(() => {
                var content = new VisualElement();
                var row = new VisualElement().Row();
                content.Add(row);
                var mode = new PopupField<string>(new List<string> { type.Name, "GameObject Path" }, usePath.boolValue ? 1 : 0);
                mode.style.width = 170;
                mode.style.flexShrink = 0;
                row.Add(mode);
                mode.RegisterValueChangedCallback(_ => SetUsePath(property, mode.index == 1));
                VisualElement input = usePath.boolValue
                    ? (VisualElement)new TextField { bindingPath = path.propertyPath }
                    : new ObjectField { bindingPath = component.propertyPath, objectType = type };
                input.style.flexGrow = 1;
                input.style.flexBasis = 0;
                input.style.minWidth = 0;
                row.Add(input);
                if (usePath.boolValue) {
                    content.Add(VRCFuryEditorUtils.Info("GameObject paths are uncommon. Use an avatar-relative path only when referencing an object outside the prefab you're making. Otherwise, use a direct component reference."));
                    content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                        var warnings = new VisualElement();
                        var avatarRoot = GetAvatarRoot(property);
                        var target = avatarRoot == null ? null : string.IsNullOrEmpty(path.stringValue)
                            ? avatarRoot.transform : avatarRoot.transform.Find(path.stringValue);
                        if (avatarRoot == null)
                            warnings.Add(VRCFuryEditorUtils.Warn("Cannot resolve this path without an avatar. Place the prefab on an avatar to validate it."));
                        else if (target == null)
                            warnings.Add(VRCFuryEditorUtils.Warn($"Avatar path '{path.stringValue}' was not found."));
                        else if (target.GetComponent(type) == null)
                            warnings.Add(VRCFuryEditorUtils.Warn($"Avatar path '{path.stringValue}' has no {type.Name}."));
                        return warnings;
                    }, path));
                }
                return content;
            }, usePath);
        }
    }
}
