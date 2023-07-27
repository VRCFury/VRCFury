using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class ShowInFirstPersonBuilder : FeatureBuilder<ShowInFirstPerson> {
        [FeatureBuilderAction]
        public void Apply() {
            var head = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Head);
            GetBuilder<ObjectMoveBuilder>().Move(featureBaseObject, head);
            GetBuilder<FakeHeadBuilder>().MarkEligible(featureBaseObject);
        }

        public override string GetEditorTitle() {
            return "Show In First Person";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This component will automatically make this GameObject a child of the head bone, and will" +
                " use constraint tricks to make it visible in first person.\n\n" +
                "Warning:\n" +
                "* Do not combine this with armature link.\n" +
                "* First person objects cannot be attached to non-first-person" +
                " objects, such as nose or ear bones.\n" +
                "* World position will be maintained when reparented.");
        }
    }
}
