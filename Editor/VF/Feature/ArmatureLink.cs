using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Inspector;

namespace VF.Feature {
    public class ArmatureLink : BaseFeature<Model.Feature.ArmatureLink> {
        public override void Generate(Model.Feature.ArmatureLink model) {
            if (model.propBone == null) {
                return;
            }

            GameObject avatarBone = null;
            if (string.IsNullOrWhiteSpace(model.bonePathOnAvatar)) {
                foreach (Transform child in avatarObject.transform) {
                    var skin = child.gameObject.GetComponent<SkinnedMeshRenderer>();
                    if (skin != null) {
                        avatarBone = skin.rootBone.gameObject;
                        break;
                    }
                }

                if (avatarBone == null) {
                    Debug.LogError("Failed to find root bone on avatar. Skipping armature link.");
                    return;
                }
            } else {
                avatarBone = avatarObject.transform.Find(model.bonePathOnAvatar).gameObject;
                if (avatarBone == null) {
                    Debug.LogError("Failed to find " + model.bonePathOnAvatar + " bone on avatar. Skipping armature link.");
                    return;
                }
            }
            
            Debug.Log("Avatar Link is linking " + model.propBone + " to " + avatarBone);
            LinkBone(avatarBone, model.propBone);
        }

        private void LinkBone(GameObject avatarBone, GameObject propBone) {
            if (avatarBone == null || propBone == null) return;
            var p = propBone.GetComponent<ParentConstraint>();
            if (p != null) Object.DestroyImmediate(p);
            p = propBone.AddComponent<ParentConstraint>();
            p.AddSource(new ConstraintSource() {
                sourceTransform = avatarBone.transform,
                weight = 1
            });
            p.weight = 1;
            p.constraintActive = true;
            p.locked = true;
            
            foreach (Transform child in avatarBone.transform) {
                var childAvatarBone = child.gameObject;
                var childPropBone = propBone.transform.Find(childAvatarBone.name)?.gameObject;
                LinkBone(childAvatarBone, childPropBone);
            }
        }

        public override string GetEditorTitle() {
            return "Armature Link";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            container.Add(new Label("Root Bone in this Prop:"));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("propBone")));
            container.Add(new Label("Path to corresponding root bone from root of avatar:"));
            container.Add(new Label("Lleave empty to default to avatar root bone (hips)"));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("bonePathOnAvatar")));
            return container;
        }

        public override bool AvailableOnAvatar() {
            return false;
        }
        
        public override bool ApplyToVrcClone() {
            return false;
        }
    }
}
