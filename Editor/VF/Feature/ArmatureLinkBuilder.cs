using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
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

    public class ArmatureLinkBuilder : FeatureBuilder<ArmatureLink> {
        private Dictionary<string, string> clipMappings = new Dictionary<string, string>();

        public static void Constrain(GameObject obj, GameObject target, bool keepOffset = false) {
            var p = obj.GetComponent<ParentConstraint>();
            if (p != null) Object.DestroyImmediate(p);
            p = obj.AddComponent<ParentConstraint>();
            p.AddSource(new ConstraintSource() {
                sourceTransform = target.transform,
                weight = 1
            });
            p.weight = 1;
            p.constraintActive = true;
            p.locked = true;
            if (keepOffset) {
                Matrix4x4 inverse = Matrix4x4.TRS(target.transform.position, target.transform.rotation, new Vector3(1,1,1)).inverse;
                p.SetTranslationOffset(0, inverse.MultiplyPoint3x4(p.transform.position));
                p.SetRotationOffset(0, (Quaternion.Inverse(target.transform.rotation) * p.transform.rotation).eulerAngles);
            }
        }

        [FeatureBuilderAction(FeatureOrder.ArmatureLinkBuilder)]
        public void Apply() {
            if (model.propBone == null) {
                Debug.LogWarning("Root bone is null on armature link.");
                return;
            }
            
            var links = GetLinks();
            if (model.useBoneMerging) {

                // First, move over all the "new children objects" that aren't bones
                foreach (var reparent in links.reparent) {
                    var objectToMove = reparent.Item1;
                    var newParent = reparent.Item2;
                    var oldPath = clipBuilder.GetPath(objectToMove);

                    // Move the object
                    objectToMove.name = "vrcf_" + uniqueModelNum + "_" + objectToMove.name;
                    objectToMove.transform.SetParent(newParent.transform);
                    
                    // Because we're adding new children, we need to ensure they are ignored by any existing physbones on the avatar.
                    RemoveFromPhysbones(objectToMove);
                    
                    // Remember how we need to rewrite animations later
                    var newPath = clipBuilder.GetPath(objectToMove);
                    clipMappings.Add(oldPath, newPath);
                }

                // Now, update all the skinned meshes in the prop to use the avatar's bone objects
                var boneMapping = new Dictionary<Transform, Transform>();
                foreach (var mergeBone in links.mergeBones) {
                    var propBone = mergeBone.Item1;
                    var avatarBone = mergeBone.Item2;
                    boneMapping[propBone.transform] = avatarBone.transform;
                    var oldPath = clipBuilder.GetPath(propBone);
                    var newPath = clipBuilder.GetPath(avatarBone);
                    clipMappings.Add(oldPath, newPath);
                }
                foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                    if (skin.rootBone != null) {
                        if (boneMapping.TryGetValue(skin.rootBone, out var newRootBone)) {
                            skin.rootBone = newRootBone;
                        }
                    }
                    var bones = skin.bones;
                    for (var i = 0; i < bones.Length; i++) {
                        if (bones[i] != null) {
                            if (boneMapping.TryGetValue(bones[i], out var newBone)) {
                                bones[i] = newBone;
                            }
                        }
                    }
                    skin.bones = bones;
                }
                foreach (var mergeBone in links.mergeBones) {
                    var propBone = mergeBone.Item1;
                    Object.DestroyImmediate(propBone);
                }
            } else {
                // Otherwise, we move all the prop bones into their matching avatar bones (as children)
                foreach (var mergeBone in links.mergeBones) {
                    var propBone = mergeBone.Item1;
                    var avatarBone = mergeBone.Item2;
                    var oldPath = clipBuilder.GetPath(propBone);
                    
                    // Move the object
                    var p = propBone.GetComponent<ParentConstraint>();
                    if (p != null) Object.DestroyImmediate(p);
                    propBone.name = "vrcf_" + uniqueModelNum + "_" + propBone.name;
                    propBone.transform.SetParent(avatarBone.transform);
                    if (!model.keepBoneOffsets) {
                        propBone.transform.localPosition = Vector3.zero;
                        propBone.transform.localRotation = Quaternion.identity;
                    }
                    
                    // Because we're adding new children, we need to ensure they are ignored by any existing physbones on the avatar.
                    RemoveFromPhysbones(propBone);
                    
                    // Remember how we need to rewrite animations later
                    var newPath = clipBuilder.GetPath(propBone);
                    clipMappings.Add(oldPath, newPath);
                }
            }
        }

        private void RemoveFromPhysbones(GameObject obj) {
            foreach (var physbone in avatarObject.GetComponentsInChildren<VRCPhysBone>(true)) {
                var root = physbone.GetRootTransform();
                if (obj.transform.IsChildOf(root)) {
                    physbone.ignoreTransforms.Add(obj.transform);
                }
            }
        }

        [FeatureBuilderAction(FeatureOrder.ArmatureLinkBuilderFixAnimations)]
        public void FixAnimations() {
            foreach (var controller in manager.GetAllTouchedControllers()) {
                for (var layerId = 0; layerId < controller.GetRaw().layers.Length; layerId++) {
                    var layer = controller.GetRaw().layers[layerId];
                    AnimatorIterator.ForEachClip(layer, (clip, setClip) => {
                        void ensureMutable() {
                            if (!VRCFuryAssetDatabase.IsVrcfAsset(clip)) {
                                var newClip = manager.GetClipStorage().NewClip(clip.name);
                                clipBuilder.CopyWithAdjustedPrefixes(clip, newClip);
                                clip = newClip;
                                setClip(clip);
                            }
                        }

                        foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                            var newPath = RewriteClipPath(binding.path);
                            if (newPath != null) {
                                var b = binding;
                                b.path = newPath;
                                ensureMutable();
                                AnimationUtility.SetEditorCurve(clip, b,
                                    AnimationUtility.GetEditorCurve(clip, binding));
                                AnimationUtility.SetEditorCurve(clip, binding, null);
                            }
                        }

                        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                            var newPath = RewriteClipPath(binding.path);
                            if (newPath != null) {
                                var b = binding;
                                b.path = newPath;
                                ensureMutable();
                                AnimationUtility.SetObjectReferenceCurve(clip, b,
                                    AnimationUtility.GetObjectReferenceCurve(clip, binding));
                                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                            }
                        }
                    });
                    controller.ModifyMask(layerId, mask => {
                        for (var i = 0; i < mask.transformCount; i++) {
                            var oldPath = mask.GetTransformPath(i);
                            var newPath = RewriteClipPath(oldPath);
                            if (newPath != null && oldPath != newPath) {
                                mask.SetTransformPath(i, newPath);
                            }
                        }
                    });
                }
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

        private static FieldInfo parentNameField = 
            typeof(SkeletonBone).GetField("parentName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private Links GetLinks() {
            var links = new Links();

            var propBone = model.propBone;
            if (propBone == null) return links;

            GameObject avatarBone;
            if (string.IsNullOrWhiteSpace(model.bonePathOnAvatar)) {
                var animator = avatarObject.GetComponent<Animator>();
                if (!animator) {
                    throw new VRCFBuilderException(
                        "ArmatureLink found no humanoid animator on avatar.");
                }
                avatarBone = animator.GetBoneTransform(model.boneOnAvatar)?.gameObject;
                if (avatarBone == null) {
                    throw new VRCFBuilderException(
                        "ArmatureLink failed to find " + model.boneOnAvatar + " bone on avatar.");
                }
                // Unity tries to find the root bone BY NAME, which often might be the wrong one. It might even be the
                // one in the prop. So we need to find it ourself with better logic.
                var skeleton = animator.avatar.humanDescription.skeleton;
                bool DoesBoneMatch(GameObject obj, SkeletonBone bone) {
                    if (bone.name != obj.name) return false;
                    var boneParentName = (string)parentNameField.GetValue(bone);
                    if (boneParentName != obj.transform.parent.name) return false;
                    return true;
                }
                bool IsProbablyInSkeleton(GameObject obj) {
                    if (obj == null) return false;
                    if (obj == avatarObject) return true;
                    if (!skeleton.Any(b => DoesBoneMatch(obj, b))) return false;
                    return IsProbablyInSkeleton(obj.transform.parent.gameObject);
                }
                var eligibleAvatarBones = avatarObject.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == avatarBone.name)
                    .Select(t => t.gameObject)
                    .Where(IsProbablyInSkeleton)
                    .ToList();
                if (eligibleAvatarBones.Count == 0) {
                    Debug.LogWarning("ArmatureLink found a matching bone, but it wasn't in the skeleton. Maybe broken?");
                } else if (eligibleAvatarBones.Count == 1) {
                    avatarBone = eligibleAvatarBones[0];
                } else {
                    throw new VRCFBuilderException(
                        "ArmatureLink found multiple possible matching " + model.boneOnAvatar + " bones on avatar.");
                }
            } else {
                avatarBone = avatarObject.transform.Find(model.bonePathOnAvatar)?.gameObject;
                if (avatarBone == null) {
                    Debug.LogError("Failed to find " + model.bonePathOnAvatar + " bone on avatar. Skipping armature link.");
                    return links;
                }
            }

            var checkStack = new Stack<Tuple<GameObject, GameObject>>();
            checkStack.Push(Tuple.Create(propBone, avatarBone));
            links.mergeBones.Push(Tuple.Create(propBone, avatarBone));

            while (checkStack.Count > 0) {
                var check = checkStack.Pop();
                foreach (Transform child in check.Item1.transform) {
                    var childPropBone = child.gameObject;
                    var searchName = childPropBone.name;
                    if (!string.IsNullOrWhiteSpace(model.removeBoneSuffix)) {
                        searchName = searchName.Replace(model.removeBoneSuffix, "");
                    }
                    var childAvatarBone = check.Item2.transform.Find(searchName)?.gameObject;
                    // Hack for Rexouium model, which added ChestUp bone at some point and broke a ton of old props
                    if (childAvatarBone == null) {
                        childAvatarBone = check.Item2.transform.Find("ChestUp/" + searchName)?.gameObject;
                    }
                    if (childAvatarBone != null) {
                        links.mergeBones.Push(Tuple.Create(childPropBone, childAvatarBone));
                        checkStack.Push(Tuple.Create(childPropBone, childAvatarBone));
                    } else {
                        links.reparent.Push(Tuple.Create(childPropBone, check.Item2));
                    }
                }
            }

            return links;
        }
        
        private string RewriteClipPath(string path) {
            foreach (var pair in clipMappings) {
                if (path.StartsWith(pair.Key + "/") || path == pair.Key) {
                    return pair.Value + path.Substring(pair.Key.Length);
                }
            }
            return null;
        }

        public override string GetEditorTitle() {
            return "Armature Link";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            container.Add(VRCFuryEditorUtils.WrappedLabel("Root bone in the prop:"));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("propBone")));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.WrappedLabel("Path to corresponding root bone from root of avatar:"));
                container.Add(VRCFuryEditorUtils.WrappedLabel("(If full string path is given, humanoid bone dropdown will be ignored)"));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("boneOnAvatar")));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("bonePathOnAvatar")));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.WrappedLabel("Keep bone offsets:"));
                container.Add(VRCFuryEditorUtils.WrappedLabel("If false, linked bones will be rigidly locked to the transform of the corresponding avatar bone." +
                                                              " If true, prop bones will maintain their initial offset to the corresponding avatar bone. This is unusual."));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("keepBoneOffsets")));
            
            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.WrappedLabel("Use Bone Merging (fragile):"));
                container.Add(VRCFuryEditorUtils.WrappedLabel("If true, skinned meshes in this prop will be modified during upload to use the actual bones from the avatar armature." +
                                                              " This means that your prop's bones will not count toward the avatar's bone count (more efficient)." +
                                                              " Beware that this may cause the prop mesh to be broken in game if the armature (including fbx export settings) does not match the avatar's EXACTLY."));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("useBoneMerging")));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.WrappedLabel("Remove bone suffix/prefix:"));
                container.Add(VRCFuryEditorUtils.WrappedLabel("If set, this substring will be removed from all bone names in the prop. This is useful for props where the artist added " +
                                                              "something like _PropName to the end of every bone, breaking AvatarLink in the process."));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("removeBoneSuffix")));

            return container;
        }
    }
}
