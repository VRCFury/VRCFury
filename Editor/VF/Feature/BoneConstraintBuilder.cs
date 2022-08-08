using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Feature {

    public class BoneConstraintBuilder : FeatureBuilder<BoneConstraint> {
        [FeatureBuilderAction]
        public void Link() {
            if (model.obj == null) {
                return;
            }

            var animator = avatarObject.GetComponent<Animator>();
            if (!animator) return;
            var bone = animator.GetBoneTransform(model.bone)?.gameObject;
            if (!bone) return;
            ArmatureLinkBuilder.Constrain(model.obj, bone);
        }

        public override string GetEditorTitle() {
            return "Bone Constraint";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            container.Add(new Label("Adds a parent constraint from the specified object to the specified bone. Useful for props" +
                                    " which are packaged as VRCFury prefabs.") { style = { whiteSpace = WhiteSpace.Normal }});
            
            container.Add(new PropertyField(prop.FindPropertyRelative("obj"), "Object in prop"));
            container.Add(new PropertyField(prop.FindPropertyRelative("bone"), "Avatar Bone"));
            return container;
        }

    }
}