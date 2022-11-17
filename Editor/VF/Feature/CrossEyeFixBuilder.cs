using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class CrossEyeFixBuilder : FeatureBuilder<CrossEyeFix2> {
        [FeatureBuilderAction]
        public void Apply() {
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            if (!avatar.enableEyeLook) return;
            var eyeLeft = avatar.customEyeLookSettings.leftEye;
            var eyeRight = avatar.customEyeLookSettings.rightEye;
            if (eyeLeft) avatar.customEyeLookSettings.leftEye = AddFakeEye(eyeLeft.gameObject).transform;
            if (eyeRight) avatar.customEyeLookSettings.rightEye = AddFakeEye(eyeRight.gameObject).transform;
        }

        private static GameObject AddFakeEye(GameObject originalEye) {
            var realEyeUp = new GameObject(originalEye.name + ".Up");
            realEyeUp.transform.SetParent(originalEye.transform, false);
            realEyeUp.transform.rotation = Quaternion.identity;
            realEyeUp.transform.SetParent(originalEye.transform.parent, true);
            
            var fakeEye = new GameObject(originalEye.name + ".Fake");
            fakeEye.transform.SetParent(originalEye.transform, false);
            fakeEye.transform.SetParent(originalEye.transform.parent, true);

            var fakeEyeUp = new GameObject(originalEye.name + ".Fake.Up");
            fakeEyeUp.transform.SetParent(realEyeUp.transform, false);
            fakeEyeUp.transform.SetParent(fakeEye.transform, true);
            
            originalEye.transform.SetParent(realEyeUp.transform, true);

            var constraint = realEyeUp.AddComponent<RotationConstraint>();
            constraint.AddSource(new ConstraintSource() {
                sourceTransform = fakeEyeUp.transform,
                weight = 1
            });
            constraint.rotationAxis = Axis.X | Axis.Y;
            constraint.weight = 1;
            constraint.constraintActive = true;
            constraint.locked = true;

            return fakeEye;
        }
        
        public override string GetEditorTitle() {
            return "Cross Eye Fix";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This feature automatically tweaks your avatar eyes so that they can't go cross-eyed in VRChat." +
                " It does this by eliminating eye bone roll through fake eye bones and rotation constraints."
            );
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}