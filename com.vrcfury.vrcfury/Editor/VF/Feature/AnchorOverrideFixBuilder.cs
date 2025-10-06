using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("Anchor Override Fix")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class AnchorOverrideFixBuilder : FeatureBuilder<AnchorOverrideFix2> {
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.AnchorOverrideFix)]
        public void Apply() {
            VFGameObject root;
            try {
                root = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Chest);
            } catch (Exception) {
                root = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Hips);
            }
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                if (model.ignoreExisting && skin.probeAnchor != null) continue;
                var keepAnchorOverrides = ((VFGameObject)skin.gameObject).GetComponentsInSelfAndParents<VRCFuryKeepAnchorOverride>();
                if (model.ignoreWorldDrops && keepAnchorOverrides.Any(k => k.isWorldDrop)) continue;
                if (keepAnchorOverrides.Any(k => !k.isWorldDrop)) continue;

                skin.probeAnchor = root;
            }
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will set the anchor override for every mesh on your avatar to your chest. " +
                "This will prevent different meshes from being lit differently on your body."));

            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            }.AddTo(content);

            adv.Add(VRCFuryEditorUtils.Prop(
                prop.FindPropertyRelative("ignoreExisting"),
                "Only change empty anchor overrides",
                tooltip: "Before this component existed, some assets used random anchor overrides. " +
                "They might look incorrect if this option is turned on."
            ));

            adv.Add(VRCFuryEditorUtils.Prop(
                prop.FindPropertyRelative("ignoreWorldDrops"),
                "Ignore VRCFury world drops"
            ));

            return content;
        }
    }
}
