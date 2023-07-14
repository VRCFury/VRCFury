using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class CrossEyeFixBuilder : FeatureBuilder<CrossEyeFix2> {
        [FeatureBuilderAction]
        public void Apply() {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
                return;
            }

            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            if (!avatar.enableEyeLook) return;
            var eyeLeft = avatar.customEyeLookSettings.leftEye;
            var eyeRight = avatar.customEyeLookSettings.rightEye;
            if (eyeLeft) avatar.customEyeLookSettings.leftEye = AddFakeEye(eyeLeft);
            if (eyeRight) avatar.customEyeLookSettings.rightEye = AddFakeEye(eyeRight);
        }

        private Transform AddFakeEye(VFGameObject originalEye) {
            var baseName = originalEye.name;

            var realEyeUp = GameObjects.Create(baseName + ".Up", originalEye.parent, useTransformFrom: originalEye);
            realEyeUp.worldRotation = Quaternion.identity;
            
            var fakeEye = GameObjects.Create(baseName + ".Fake", originalEye.parent, useTransformFrom: originalEye);

            var fakeEyeUp = GameObjects.Create(baseName + ".Fake.Up", fakeEye, useTransformFrom: realEyeUp);
            
            var mover = allBuildersInRun.OfType<ObjectMoveBuilder>().First();
            mover.Move(originalEye.gameObject, realEyeUp.gameObject);

            var constraint = realEyeUp.AddComponent<RotationConstraint>();
            constraint.AddSource(new ConstraintSource() {
                sourceTransform = fakeEyeUp,
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