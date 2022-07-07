using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;

namespace VF.Feature {
    public class ArmatureLink : FeatureBuilder<Model.Feature.ArmatureLink> {
        public override void Apply() {
            Apply(false);
        }

        public override void ApplyToVrcClone() {
            Apply(true);
        }
        
        private void Apply(bool operatingOnVrcClone) {
            if (model.propBone == null) {
                Debug.LogWarning("Root bone is null on armature link.");
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
            LinkBone(avatarBone, model.propBone, model.useOptimizedUpload, operatingOnVrcClone);
        }

        private void LinkBone(GameObject avatarBone, GameObject propBone, bool useOptimizedUpload, bool operatingOnVrcClone) {
            if (avatarBone == null || propBone == null) return;
            var p = propBone.GetComponent<ParentConstraint>();
            if (p != null) Object.DestroyImmediate(p);
            if (useOptimizedUpload && operatingOnVrcClone && !PrefabUtility.IsPartOfPrefabInstance(propBone)) {
                // If we're operating on the upload copy, we can be more efficient by just
                // moving the prop bone onto the avatar bone, rather than using constraints
                Debug.Log("Using optimized armature link for bone " + propBone);
                propBone.transform.SetParent(avatarBone.transform);
                propBone.transform.localPosition = Vector3.zero;
                propBone.transform.localRotation = Quaternion.identity;
            } else {
                p = propBone.AddComponent<ParentConstraint>();
                p.AddSource(new ConstraintSource() {
                    sourceTransform = avatarBone.transform,
                    weight = 1
                });
                p.weight = 1;
                p.constraintActive = true;
                p.locked = true;
            }

            foreach (Transform child in avatarBone.transform) {
                var childAvatarBone = child.gameObject;
                var childPropBone = propBone.transform.Find(childAvatarBone.name)?.gameObject;
                LinkBone(childAvatarBone, childPropBone, useOptimizedUpload, operatingOnVrcClone);
            }
        }

        public override string GetEditorTitle() {
            return "Armature Link";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            container.Add(new Label("Root bone in the prop:"));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("propBone")));

            container.Add(new Label("Path to corresponding root bone from root of avatar:") {
                style = { paddingTop = 10 }
            });
            container.Add(new Label("Leave empty to default to avatar's skin root bone (usually hips)"));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("bonePathOnAvatar")));

            container.Add(new Label("Use bone parenting optimization on upload (EXPERIMENTAL):") {
                style = { paddingTop = 10 }
            });
            container.Add(VRCFuryEditorUtils.WrappedLabel("Removes constraints and actually re-parents the prop's bones into the avatar's bones (only when uploading)." +
                                             " Possibly more optimized, but may affect PhysBones that share linked bones." +
                                             " Required for quest support, which doesn't allow constraints."));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("useOptimizedUpload")));
            
            return container;
        }
    }
}
