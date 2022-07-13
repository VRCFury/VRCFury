using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using Object = UnityEngine.Object;

namespace VF.Feature {

    public class ArmatureLinkBuilder : FeatureBuilder<ArmatureLink> {
        [FeatureBuilderAction]
        public void Link() {
            var links = GetLinks();
            foreach (var mergeBone in links.mergeBones) {
                var propBone = mergeBone.Item1;
                var avatarBone = mergeBone.Item2;
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
            }
        }
        
        [FeatureBuilderAction(applyToVrcClone:true)]
        public void LinkOnVrcClone() {
            var links = GetLinks();

            foreach (var reparentItem in links.reparent) {
                var objectToMove = reparentItem.Item1;
                var newParent = reparentItem.Item2;
                objectToMove.transform.SetParent(newParent.transform);
            }

            var propSkins = featureBaseObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var mergeBone in links.mergeBones) {
                var propBone = mergeBone.Item1;
                var avatarBone = mergeBone.Item2;
                foreach (var skin in propSkins) {
                    if (skin.rootBone == propBone.transform) skin.rootBone = avatarBone.transform;
                    for (var i = 0; i < skin.bones.Length; i++) {
                        if (skin.bones[i] == propBone.transform) skin.bones[i] = avatarBone.transform;
                    }
                }
                Object.DestroyImmediate(propBone);
            }
        }
        
        [FeatureBuilderAction(100)]
        public void FixAnimations() {
            var links = GetLinks();

            var mapping = new Dictionary<string, string>();
            foreach (var link in links.reparent) {
                var objectToMove = link.Item1;
                var newParent = link.Item2;
                var oldPath = motions.GetPath(objectToMove);
                var newPath = ClipBuilder.Join(motions.GetPath(newParent), objectToMove.name);
                mapping.Add(oldPath, newPath);
            }

            foreach (var layer in controller.GetManagedLayers()) {
                DefaultClipBuilder.ForEachClip(layer, clip => {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                        var newPath = mapping[binding.path];
                        if (newPath != null) {
                            var b = binding;
                            b.path = newPath;
                            AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(clip, binding));
                        }
                    }
                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                        var newPath = mapping[binding.path];
                        if (newPath != null) {
                            var b = binding;
                            b.path = newPath;
                            AnimationUtility.SetObjectReferenceCurve(clip, b, AnimationUtility.GetObjectReferenceCurve(clip, binding));
                        }
                    }
                });
            }
        }

        private class Links {
            // These are stacks, because it's convenient, and we want to iterate over them in reverse order anyways
            // because when operating on the vrc clone, we delete game objects as we process them, and we want to
            // delete the children first.
            
            // left=bone in prop | right=bone in avatar
            public readonly Stack<Tuple<GameObject, GameObject>> mergeBones
                = new Stack<Tuple<GameObject, GameObject>>();
            
            // left=object to move | right=new parent
            public readonly Stack<Tuple<GameObject, GameObject>> reparent
                = new Stack<Tuple<GameObject, GameObject>>();
        }

        private Links GetLinks() {
            var links = new Links();

            if (model.propBone == null) {
                Debug.LogWarning("Root bone is null on armature link.");
                return links;
            }
            var propBone = model.propBone;

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
                    return links;
                }
            } else {
                avatarBone = avatarObject.transform.Find(model.bonePathOnAvatar).gameObject;
                if (avatarBone == null) {
                    Debug.LogError("Failed to find " + model.bonePathOnAvatar + " bone on avatar. Skipping armature link.");
                    return links;
                }
            }
            
            Debug.Log("Avatar Link is linking " + propBone + " to " + avatarBone);
            var checkStack = new Stack<Tuple<GameObject, GameObject>>();
            checkStack.Push(Tuple.Create(propBone, avatarBone));
            links.mergeBones.Push(Tuple.Create(propBone, avatarBone));

            while (checkStack.Count > 0) {
                var check = checkStack.Pop();
                foreach (Transform child in check.Item1.transform) {
                    var childPropBone = child.gameObject;
                    var childAvatarBone = check.Item2.transform.Find(childPropBone.name)?.gameObject;
                    if (childAvatarBone == null) {
                        links.reparent.Push(Tuple.Create(childPropBone, check.Item2));
                    } else {
                        links.mergeBones.Push(Tuple.Create(childPropBone, childAvatarBone));
                    }
                }
            }

            return links;
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
