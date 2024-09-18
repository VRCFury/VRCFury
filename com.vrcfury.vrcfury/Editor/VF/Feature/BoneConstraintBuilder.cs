using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;

namespace VF.Feature {

    [FeatureTitle("Bone Constraint")]
    [FeatureHideInMenu]
    internal class BoneConstraintBuilder : FeatureBuilder<BoneConstraint> {
        [VFAutowired] private readonly GlobalsService globals;

        [FeatureBuilderAction]
        public void Link() {
            globals.addOtherFeature(new ArmatureLink {
                linkTo = { new ArmatureLink.LinkTo { bone = model.bone }},
                keepBoneOffsets2 = ArmatureLink.KeepBoneOffsets.No,
                linkMode = ArmatureLink.ArmatureLinkMode.ParentConstraint,
                propBone = model.obj
            });
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
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
