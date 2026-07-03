using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Change SPS Tag")]
    internal class ChangeSpsTagActionBuilder : ActionBuilder<ChangeSpsTagAction> {
        [VFAutowired] private readonly VFGameObject avatarObject;

        private enum TargetType {
            None,
            Plug,
            Socket
        }

        public AnimationClip Build(ChangeSpsTagAction model) {
            var clip = NewClip();
            if (TryGetBinding(model, out var path, out var type, out var propertyName, out var selfPropertyName, out var othersPropertyName)) {
                var tagHash = SpsConfigurer.HashTag(VRCFuryHapticPlugEditor.SanitizeSpsTag(model.tag));
                clip.SetCurve(path, type, propertyName, (float)tagHash);
                if (selfPropertyName != null) {
                    clip.SetCurve(path, type, selfPropertyName, model.allowSelf ? 1 : 0);
                }
                if (othersPropertyName != null) {
                    clip.SetCurve(path, type, othersPropertyName, model.allowOthers ? 1 : 0);
                }
            }
            return clip;
        }

        private bool TryGetBinding(
            ChangeSpsTagAction model,
            out string path,
            out System.Type type,
            out string propertyName,
            out string selfPropertyName,
            out string othersPropertyName
        ) {
            path = null;
            type = null;
            propertyName = null;
            selfPropertyName = null;
            othersPropertyName = null;

            var target = model.target;
            if (target == null) return false;

            var targetType = GetTargetType(target);
            var slot = GetClampedSlot(model, targetType);
            var targetPath = target.gameObject.asVf().GetPath(avatarObject);

            if (targetType == TargetType.Plug) {
                path = JoinPath(targetPath, "BakedSpsPlug/SpsResolver");
                type = typeof(MeshRenderer);
                var prefix = model.exclude ? "_SPS_TagExclude" : "_SPS_TagInclude";
                propertyName = $"material.{prefix}{slot}";
                selfPropertyName = $"material.{prefix}{slot}Self";
                othersPropertyName = $"material.{prefix}{slot}Others";
                return true;
            }

            if (targetType == TargetType.Socket) {
                path = JoinPath(targetPath, "BakedSpsSocket/WorldSpace/SpsScreenMarker");
                type = typeof(MeshRenderer);
                propertyName = $"material._SPS_SocketTag{slot}";
                return true;
            }

            return false;
        }

        private static string JoinPath(string basePath, string suffix) {
            if (string.IsNullOrEmpty(basePath)) return suffix;
            return basePath + "/" + suffix;
        }

        private static int GetClampedSlot(ChangeSpsTagAction model, TargetType targetType) {
            var max = GetMaxSlot(targetType);
            if (max <= 0) return 1;
            return Mathf.Clamp(model.tagNumber, 1, max);
        }

        private static int GetMaxSlot(TargetType targetType) {
            switch (targetType) {
                case TargetType.Plug:
                    return VRCFuryHapticPlugEditor.SpsTagRuleCount;
                case TargetType.Socket:
                    return VRCFuryHapticSocketEditor.SpsTagCount;
                default:
                    return Mathf.Max(VRCFuryHapticPlugEditor.SpsTagRuleCount, VRCFuryHapticSocketEditor.SpsTagCount);
            }
        }

        private static TargetType GetTargetType(Transform target) {
            if (target == null) return TargetType.None;
            if (target.GetComponent<VRCFuryHapticPlug>() != null) return TargetType.Plug;
            if (target.GetComponent<VRCFuryHapticSocket>() != null) return TargetType.Socket;
            return TargetType.None;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var targetProp = prop.FindPropertyRelative("target");
            var excludeProp = prop.FindPropertyRelative("exclude");
            var allowSelfProp = prop.FindPropertyRelative("allowSelf");
            var allowOthersProp = prop.FindPropertyRelative("allowOthers");
            var tagNumberProp = prop.FindPropertyRelative("tagNumber");
            var tagProp = prop.FindPropertyRelative("tag");

            return VRCFuryEditorUtils.RefreshOnChange(() => {
                var content = new VisualElement();
                content.Add(VRCFuryEditorUtils.Prop(targetProp, "Target"));

                var targetType = GetTargetType(targetProp.objectReferenceValue as Transform);
                var maxSlot = GetMaxSlot(targetType);
                var clampedSlot = Mathf.Clamp(tagNumberProp.intValue <= 0 ? 1 : tagNumberProp.intValue, 1, maxSlot);
                if (tagNumberProp.intValue != clampedSlot) {
                    tagNumberProp.intValue = clampedSlot;
                    tagNumberProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }

                if (targetType == TargetType.Plug) {
                    var includeButton = new RadioButton {
                        value = !excludeProp.boolValue
                    };
                    var excludeButton = new RadioButton {
                        value = excludeProp.boolValue
                    };
                    includeButton.RegisterValueChangedCallback(cb => {
                        if (!cb.newValue) return;
                        excludeProp.boolValue = false;
                        excludeProp.serializedObject.ApplyModifiedProperties();
                        excludeButton.SetValueWithoutNotify(false);
                    });
                    excludeButton.RegisterValueChangedCallback(cb => {
                        if (!cb.newValue) return;
                        excludeProp.boolValue = true;
                        excludeProp.serializedObject.ApplyModifiedProperties();
                        includeButton.SetValueWithoutNotify(false);
                    });
                    var row = new VisualElement().Row();
                    row.style.flexWrap = Wrap.NoWrap;
                    var includeProp = VRCFuryEditorUtils.Prop(null, "Include", fieldOverride: includeButton);
                    includeProp.style.marginRight = 12;
                    row.Add(includeProp);
                    row.Add(VRCFuryEditorUtils.Prop(null, "Exclude", fieldOverride: excludeButton));
                    content.Add(row);
                }

                tagNumberProp.intValue = clampedSlot;
                content.Add(VRCFuryEditorUtils.Prop(tagNumberProp, "Tag #", onChange: () => {
                    tagNumberProp.intValue = Mathf.Clamp(tagNumberProp.intValue <= 0 ? 1 : tagNumberProp.intValue, 1, maxSlot);
                    tagNumberProp.serializedObject.ApplyModifiedProperties();
                }));

                content.Add(VRCFuryHapticPlugEditor.SpsTagProp(tagProp, "Tag"));

                if (targetType == TargetType.Plug) {
                    var row = new VisualElement().Row();
                    row.style.flexWrap = Wrap.NoWrap;
                    var selfProp = VRCFuryEditorUtils.Prop(allowSelfProp, "Self");
                    selfProp.style.marginRight = 12;
                    row.Add(selfProp);
                    row.Add(VRCFuryEditorUtils.Prop(allowOthersProp, "Others"));
                    content.Add(row);
                }

                if (targetType == TargetType.None && targetProp.objectReferenceValue != null) {
                    content.Add(VRCFuryEditorUtils.Warn("Target must be an SPS Plug or SPS Socket transform."));
                }

                return content;
            }, targetProp, excludeProp, allowSelfProp, allowOthersProp, tagNumberProp);
        }
    }
}
