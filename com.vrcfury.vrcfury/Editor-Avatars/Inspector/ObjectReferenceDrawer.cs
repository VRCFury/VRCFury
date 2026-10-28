using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Hooks;
using VF.Model;
using VF.Utils;

namespace VF.Inspector {
    [CustomPropertyDrawer(typeof(ObjectReference))]
    internal class ObjectReferenceDrawer : PropertyDrawer {
        internal class Options {
            public ObjectReference.Mode defaultMode = ObjectReference.Mode.Bone;
            public bool allowBone = true;
            public bool showOffsetPath;
            public Type componentType;
        }

        internal static void SetMode(SerializedProperty property, ObjectReference.Mode mode) {
            // A transient deselection while recycling a list row must not modify the reference.
            if (mode < ObjectReference.Mode.Bone || mode > ObjectReference.Mode.ObjectPath) return;
            if (((ObjectReference)property.GetObject()).mode != mode)
                property.FindPropertyRelative("offset").stringValue = "";
            property.FindPropertyRelative("useBone").boolValue = mode == ObjectReference.Mode.Bone;
            property.FindPropertyRelative("useObj").boolValue = mode == ObjectReference.Mode.ObjectReference;
            if (mode != ObjectReference.Mode.ObjectReference)
                property.FindPropertyRelative("obj").objectReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        }

        public static GameObject GetAvatarRoot(SerializedProperty property) {
            var owner = property.serializedObject.targetObject as UnityEngine.Component;
            return owner == null ? null : owner.GetAvatarRoot();
        }

        public static GameObject Resolve(SerializedProperty property, GameObject avatarRoot) {
            try {
                return ((ObjectReference)property.GetObject()).Resolve(avatarRoot);
            } catch (Exception) {
                return null;
            }
        }

        internal static bool IsBone(GameObject target, GameObject avatarRoot) =>
            target != null && avatarRoot != null && VRCFArmatureCache.GetPerFrame(avatarRoot)
                .GetAllBones().Values.Contains((VFGameObject)target);

        internal static void AddEntry(SerializedProperty list, Options options) =>
            VRCFuryEditorUtils.AddToList(list, entry => SetMode(entry, options.defaultMode));

        public static VisualElement List(SerializedProperty list, Options options) => VRCFuryEditorUtils.List(list,
            onPlus: () => AddEntry(list, options),
            renderItem: (entry, index) => CreateEditor(entry, options));

        public override VisualElement CreatePropertyGUI(SerializedProperty property) => CreateEditor(property, new Options());

        public static VisualElement CreateEditor(SerializedProperty property, Options options) {
            var useBone = property.FindPropertyRelative("useBone");
            var useObj = property.FindPropertyRelative("useObj");
            var obj = property.FindPropertyRelative("obj");
            var bone = property.FindPropertyRelative("bone");
            var offset = property.FindPropertyRelative("offset");
            return VRCFuryEditorUtils.RefreshOnChange(() => {
                var mode = ((ObjectReference)property.GetObject()).mode;
                var content = new VisualElement();
                var modes = new List<(ObjectReference.Mode mode, string label)> {
                    (ObjectReference.Mode.Bone, "Avatar Bone"),
                    (ObjectReference.Mode.ObjectReference, "Object Reference"),
                    (ObjectReference.Mode.ObjectPath, "Object Path")
                }.Where(choice => options.allowBone || mode == ObjectReference.Mode.Bone || choice.mode != ObjectReference.Mode.Bone).ToList();
                var choices = new PopupField<string>(modes.Select(choice => choice.label).ToList(), modes.FindIndex(choice => choice.mode == mode));
                choices.RegisterValueChangedCallback(_ => {
                    if (choices.index >= 0 && choices.index < modes.Count) SetMode(property, modes[choices.index].mode);
                });
                var row = new VisualElement().Row();
                content.Add(row);
                choices.style.width = 140;
                choices.style.flexShrink = 0;
                row.Add(choices);
                VisualElement mainField;
                if (mode == ObjectReference.Mode.Bone)
                    mainField = VRCFuryEditorUtils.Prop(bone);
                else if (mode == ObjectReference.Mode.ObjectReference)
                    mainField = VRCFuryEditorUtils.Prop(obj);
                else
                    mainField = new TextField { bindingPath = offset.propertyPath };
                mainField.style.minWidth = 0;
                mainField.style.flexShrink = 1;
                row.Add(mainField.FlexGrow(1).FlexBasis(0));
                if (mode != ObjectReference.Mode.ObjectPath && (options.showOffsetPath || !string.IsNullOrEmpty(offset.stringValue)))
                    content.Add(VRCFuryEditorUtils.Prop(offset, "Offset Path"));

                if (mode == ObjectReference.Mode.ObjectPath)
                    content.Add(VRCFuryEditorUtils.Info("Object paths are uncommon. Use an avatar-relative path only when referencing an object outside the prefab you're making. Otherwise, use an object reference."));

                content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var warnings = new VisualElement();
                    try {
                        var avatarRoot = GetAvatarRoot(property);
                        var target = ((ObjectReference)property.GetObject()).Resolve(avatarRoot);
                        if (options.allowBone && mode != ObjectReference.Mode.Bone && IsBone(target, avatarRoot))
                            warnings.Add(VRCFuryEditorUtils.Warn("The selected object is an avatar bone. Use Avatar Bone mode so addon prefabs can work on different avatars."));
                        if (options.componentType != null && target.GetComponent(options.componentType) == null)
                            warnings.Add(VRCFuryEditorUtils.Warn($"Selected object has no {options.componentType.Name}."));
                    } catch (Exception e) {
                        warnings.Add(VRCFuryEditorUtils.Warn(e.Message));
                    }
                    return warnings;
                }, obj, bone, offset));
                return content;
            }, useBone, useObj);
        }
    }
}
