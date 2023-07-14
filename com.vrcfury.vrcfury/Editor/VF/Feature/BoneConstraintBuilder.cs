using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

    public class BoneConstraintBuilder : FeatureBuilder<BoneConstraint> {
        [FeatureBuilderAction]
        public void Link() {
            addOtherFeature(new ArmatureLink {
                boneOnAvatar = model.bone,
                keepBoneOffsets = false,
                linkMode = ArmatureLink.ArmatureLinkMode.ParentConstraint,
                propBone = model.obj
            });
        }

        public override bool ShowInMenu() {
            return false;
        }

        public override string GetEditorTitle() {
            return "Bone Constraint";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.Error(
                "This feature is deprecated. Please use Armature Link instead. It can do everything" +
                " this feature can do and more."));
            
            container.Add(VRCFuryEditorUtils.Info(
                "Adds a parent constraint from the specified object to the specified bone. Useful for props" +
                " which are packaged as VRCFury prefabs."));
            
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"), "Object in prop"));
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("bone"), "Avatar Bone"));
            return container;
        }

    }
}
