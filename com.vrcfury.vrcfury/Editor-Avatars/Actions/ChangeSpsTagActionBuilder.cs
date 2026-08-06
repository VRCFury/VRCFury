using System;
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
using VF.Utils.Controller;

namespace VF.Actions {
    [FeatureTitle("Change SPS Tag")]
    internal class ChangeSpsTagActionBuilder : ActionBuilder<ChangeSpsTagAction> {
        private enum TargetType {
            None,
            Plug,
            Socket
        }

        public VFClip Build(ChangeSpsTagAction model, bool debugMode) {
            var clip = NewClip();
            if (debugMode) return clip;
            if (TryGetBinding(model, out var target, out var type, out var lowPropertyName, out var highPropertyName, out var selfPropertyName, out var othersPropertyName)) {
                var tagHash = model.globalTag
                    ? (model.globalTagEnabled ? SpsConfigurer.SharedTag : 0u)
                    : SpsConfigurer.HashTag(VRCFuryHapticPlugEditor.SanitizeSpsTag(model.tag));
                clip.SetCurve(target, type, lowPropertyName, SpsMarkersService.GetLow(tagHash));
                clip.SetCurve(target, type, highPropertyName, SpsMarkersService.GetHigh(tagHash));
                if (selfPropertyName != null) {
                    clip.SetCurve(target, type, selfPropertyName, model.globalTag ? (model.globalTagEnabled ? 1 : 0) : (model.allowSelf ? 1 : 0));
                }
                if (othersPropertyName != null) {
                    clip.SetCurve(target, type, othersPropertyName, model.globalTag ? (model.globalTagEnabled ? 1 : 0) : (model.allowOthers ? 1 : 0));
                }
            }
            return clip;
        }

        private bool TryGetBinding(
            ChangeSpsTagAction model,
            out VFGameObject bindingTarget,
            out System.Type type,
            out string lowPropertyName,
            out string highPropertyName,
            out string selfPropertyName,
            out string othersPropertyName
        ) {
            bindingTarget = null;
            type = null;
            lowPropertyName = null;
            highPropertyName = null;
            selfPropertyName = null;
            othersPropertyName = null;

            var target = model.target;
            if (target == null) return false;

            var targetType = GetTargetType(target);
            var slot = GetClampedSlot(model, targetType);
            var targetObject = target.gameObject.asVf();

            if (targetType == TargetType.Plug) {
                bindingTarget = targetObject.Find("BakedSpsPlug/OneSpace/SpsResolver");
                if (bindingTarget == null) {
                    throw new Exception($"Change SPS Tag target `{targetObject.GetPath()}` is missing `BakedSpsPlug/OneSpace/SpsResolver`");
                }
                type = typeof(MeshRenderer);
                if (model.globalTag) {
                    lowPropertyName = "material._SPS_TagInclude4Low";
                    highPropertyName = "material._SPS_TagInclude4High";
                    selfPropertyName = "material._SPS_TagInclude4Self";
                    othersPropertyName = "material._SPS_TagInclude4Others";
                } else {
                    var prefix = model.exclude ? "_SPS_TagExclude" : "_SPS_TagInclude";
                    lowPropertyName = $"material.{prefix}{slot}Low";
                    highPropertyName = $"material.{prefix}{slot}High";
                    selfPropertyName = $"material.{prefix}{slot}Self";
                    othersPropertyName = $"material.{prefix}{slot}Others";
                }
                return true;
            }

            if (targetType == TargetType.Socket) {
                bindingTarget = targetObject.Find("BakedSpsSocket/OneSpace/SpsScreenMarker");
                if (bindingTarget == null) {
                    throw new Exception($"Change SPS Tag target `{targetObject.GetPath()}` is missing `BakedSpsSocket/OneSpace/SpsScreenMarker`");
                }
                type = typeof(MeshRenderer);
                lowPropertyName = $"material._SPS_SocketTag{slot}Low";
                highPropertyName = $"material._SPS_SocketTag{slot}High";
                return true;
            }

            return false;
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
            var globalTagProp = prop.FindPropertyRelative("globalTag");
            var globalTagEnabledProp = prop.FindPropertyRelative("globalTagEnabled");
            var allowSelfProp = prop.FindPropertyRelative("allowSelf");
            var allowOthersProp = prop.FindPropertyRelative("allowOthers");
            var tagNumberProp = prop.FindPropertyRelative("tagNumber");
            var tagProp = prop.FindPropertyRelative("tag");

            return VRCFuryEditorUtils.RefreshOnChange(() => {
                var content = new VisualElement();
                content.Add(VRCFuryEditorUtils.Prop(targetProp, "SPS Plug / Socket"));

                var targetType = GetTargetType(targetProp.objectReferenceValue as Transform);
                var maxSlot = GetMaxSlot(targetType);
                var clampedSlot = Mathf.Clamp(tagNumberProp.intValue <= 0 ? 1 : tagNumberProp.intValue, 1, maxSlot);
                if (tagNumberProp.intValue != clampedSlot) {
                    tagNumberProp.intValue = clampedSlot;
                    tagNumberProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }

                if (targetType == TargetType.Plug) {
                    content.Add(new Label("Set this tag:"));
                    var includeButton = new Toggle {
                        value = !excludeProp.boolValue && !globalTagProp.boolValue
                    };
                    var excludeButton = new Toggle {
                        value = excludeProp.boolValue && !globalTagProp.boolValue
                    };
                    var globalButton = new Toggle {
                        value = globalTagProp.boolValue
                    };
                    includeButton.RegisterValueChangedCallback(cb => {
                        if (!cb.newValue) return;
                        globalTagProp.boolValue = false;
                        excludeProp.boolValue = false;
                        excludeProp.serializedObject.ApplyModifiedProperties();
                        excludeButton.SetValueWithoutNotify(false);
                        globalButton.SetValueWithoutNotify(false);
                    });
                    excludeButton.RegisterValueChangedCallback(cb => {
                        if (!cb.newValue) return;
                        globalTagProp.boolValue = false;
                        excludeProp.boolValue = true;
                        excludeProp.serializedObject.ApplyModifiedProperties();
                        includeButton.SetValueWithoutNotify(false);
                        globalButton.SetValueWithoutNotify(false);
                    });
                    globalButton.RegisterValueChangedCallback(cb => {
                        if (!cb.newValue) return;
                        globalTagProp.boolValue = true;
                        excludeProp.serializedObject.ApplyModifiedProperties();
                        includeButton.SetValueWithoutNotify(false);
                        excludeButton.SetValueWithoutNotify(false);
                    });
                    var row = new VisualElement().Row();
                    row.style.flexWrap = Wrap.NoWrap;
                    var includeProp = VRCFuryEditorUtils.Prop(null, "Include", fieldOverride: includeButton);
                    includeProp.style.marginRight = 12;
                    row.Add(includeProp);
                    var excludeUi = VRCFuryEditorUtils.Prop(null, "Exclude", fieldOverride: excludeButton);
                    excludeUi.style.marginRight = 12;
                    row.Add(excludeUi);
                    row.Add(VRCFuryEditorUtils.Prop(null, "Global", fieldOverride: globalButton));
                    content.Add(row);
                }

                if (targetType == TargetType.Plug && globalTagProp.boolValue) {
                    content.Add(new Label("To this value: (leave empty to unset)"));
                    content.Add(VRCFuryEditorUtils.Prop(globalTagEnabledProp, "Enabled"));
                } else {
                    if (targetType == TargetType.Socket) {
                        content.Add(new Label("Set this tag:"));
                    }
                    tagNumberProp.intValue = clampedSlot;
                    content.Add(VRCFuryEditorUtils.Prop(tagNumberProp, "Tag #", onChange: () => {
                        tagNumberProp.intValue = Mathf.Clamp(tagNumberProp.intValue <= 0 ? 1 : tagNumberProp.intValue, 1, maxSlot);
                        tagNumberProp.serializedObject.ApplyModifiedProperties();
                    }));
                    content.Add(new Label("To this value: (leave empty to unset)"));
                    content.Add(VRCFuryHapticPlugEditor.SpsTagProp(tagProp, "Tag"));
                }

                if (targetType == TargetType.Plug && !globalTagProp.boolValue) {
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
            }, targetProp, excludeProp, globalTagProp, globalTagEnabledProp, allowSelfProp, allowOthersProp, tagNumberProp);
        }
    }
}
