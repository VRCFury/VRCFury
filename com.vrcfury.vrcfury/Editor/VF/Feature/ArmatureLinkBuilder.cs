using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Feature {

    public class ArmatureLinkBuilder : FeatureBuilder<ArmatureLink> {
        [FeatureBuilderAction(FeatureOrder.ArmatureLinkBuilder)]
        public void Apply() {
            if (model.propBone == null) {
                Debug.LogWarning("Root bone is null on armature link.");
                return;
            }

            var mover = allBuildersInRun.OfType<ObjectMoveBuilder>().First();
            
            var links = GetLinks();
            if (links == null) {
                return;
            }

            var linkMode = GetLinkMode();
            var keepBoneOffsets = GetKeepBoneOffsets(linkMode);

            var (_, _, scalingFactor) = GetScalingFactor(links);

            Debug.Log("Detected scaling factor: " + scalingFactor);
            var scalingRequired = scalingFactor < 0.99 || scalingFactor > 1.01;
            
            if (linkMode == ArmatureLink.ArmatureLinkMode.SkinRewrite) {

                if (scalingRequired || keepBoneOffsets) {
                    var bonesInProp = links.propMain
                        .GetSelfAndAllChildren()
                        .ToImmutableHashSet();
                    var skinsUsingBonesInProp = avatarObject
                        .GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()
                        .Where(skin => skin.sharedMesh)
                        .Where(skin =>
                            bonesInProp.Contains(skin.rootBone) || skin.bones.Any(b => bonesInProp.Contains(b)));
                    foreach (var skin in skinsUsingBonesInProp) {
                        skin.sharedMesh = mutableManager.MakeMutable(skin.sharedMesh);
                        VRCFuryEditorUtils.MarkDirty(skin);

                        var mesh = skin.sharedMesh;
                        mesh.bindposes = Enumerable.Zip(skin.bones, mesh.bindposes, (a,b) => (a,b))
                            .Select(boneAndBindPose => {
                                VFGameObject bone = boneAndBindPose.a;
                                var bindPose = boneAndBindPose.b;
                                if (bone == null) return bindPose;
                                var mergedTo = links.mergeBones
                                    .Where(m => m.Item1 == bone)
                                    .Select(m => m.Item2)
                                    .FirstOrDefault();
                                if (!mergedTo) return bindPose;
                                if (keepBoneOffsets) {
                                    bindPose = mergedTo.worldToLocalMatrix * bone.localToWorldMatrix * bindPose;
                                } else if (scalingRequired) {
                                    bindPose = Matrix4x4.Scale(new Vector3(scalingFactor, scalingFactor, scalingFactor)) * bindPose;
                                }
                                return bindPose;
                            }) 
                            .ToArray();
                        VRCFuryEditorUtils.MarkDirty(mesh);
                    }
                }

                // First, move over all the "new children objects" that aren't bones
                foreach (var reparent in links.reparent) {
                    var objectToMove = reparent.Item1;
                    var newParent = reparent.Item2;

                    // Move the object
                    mover.Move(
                        objectToMove,
                        newParent,
                        "vrcf_" + uniqueModelNum + "_" + objectToMove.name,
                        worldPositionStays: keepBoneOffsets
                    );

                    if (!keepBoneOffsets) {
                        objectToMove.transform.localScale *= scalingFactor;
                        objectToMove.transform.localPosition *= scalingFactor;
                    }

                    // Because we're adding new children, we need to ensure they are ignored by any existing physbones on the avatar.
                    PhysboneUtils.RemoveFromPhysbones(objectToMove.transform, true);
                }

                // Now, update all the skinned meshes in the prop to use the avatar's bone objects
                var boneMapping = new Dictionary<Transform, Transform>();
                foreach (var mergeBone in links.mergeBones) {
                    var propBone = mergeBone.Item1;
                    var avatarBone = mergeBone.Item2;
                    FailIfComponents(propBone);
                    UpdatePhysbones(propBone, avatarBone);
                    UpdatePhysboneColliders(propBone, avatarBone, scalingRequired, scalingFactor, keepBoneOffsets);
                    UpdateConstraints(propBone, avatarBone);
                    boneMapping[propBone.transform] = avatarBone.transform;
                    mover.AddDirectRewrite(propBone, avatarBone);
                }
                foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                    if (skin.rootBone != null) {
                        if (boneMapping.TryGetValue(skin.rootBone, out var newRootBone)) {
                            var b = skin.localBounds;
                            b.center = new Vector3(
                                b.center.x * skin.rootBone.lossyScale.x / newRootBone.lossyScale.x,
                                b.center.y * skin.rootBone.lossyScale.y / newRootBone.lossyScale.y,
                                b.center.z * skin.rootBone.lossyScale.z / newRootBone.lossyScale.z
                            );
                            b.extents = new Vector3(
                                b.extents.x * skin.rootBone.lossyScale.x / newRootBone.lossyScale.x,
                                b.extents.y * skin.rootBone.lossyScale.y / newRootBone.lossyScale.y,
                                b.extents.z * skin.rootBone.lossyScale.z / newRootBone.lossyScale.z
                            );
                            skin.localBounds = b;
                            
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
                    propBone.Destroy();
                }
            } else if (linkMode == ArmatureLink.ArmatureLinkMode.MergeAsChildren || linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot) {
                var rootOnly = linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot;
                // Otherwise, we move all the prop bones into their matching avatar bones (as children)
                foreach (var mergeBone in links.mergeBones) {
                    var propBone = mergeBone.Item1;
                    var avatarBone = mergeBone.Item2;
                    if (rootOnly) {
                        if (propBone != model.propBone) {
                            continue;
                        }
                    } else {
                        FailIfComponents(propBone);
                        UpdatePhysbones(propBone, avatarBone);
                    }

                    // Move the object
                    var p = propBone.GetComponent<ParentConstraint>();
                    if (p != null) Object.DestroyImmediate(p);
                    mover.Move(
                        propBone,
                        avatarBone,
                        "vrcf_" + uniqueModelNum + "_" + propBone.name
                    );
                    if (!keepBoneOffsets) {
                        propBone.transform.localPosition = Vector3.zero;
                        propBone.transform.localRotation = Quaternion.identity;
                        propBone.transform.localScale = Vector3.one * scalingFactor;
                    }

                    // Because we're adding new children, we need to ensure they are ignored by any existing physbones on the avatar.
                    PhysboneUtils.RemoveFromPhysbones(propBone.transform, true);
                }
            } else if (linkMode == ArmatureLink.ArmatureLinkMode.ParentConstraint) {
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
                    if (keepBoneOffsets) {
                        Matrix4x4 inverse = Matrix4x4.TRS(avatarBone.transform.position, avatarBone.transform.rotation, new Vector3(1,1,1)).inverse;
                        p.SetTranslationOffset(0, inverse.MultiplyPoint3x4(p.transform.position));
                        p.SetRotationOffset(0, (Quaternion.Inverse(avatarBone.transform.rotation) * p.transform.rotation).eulerAngles);
                    }
                }
            }
        }

        private (float, float, float) GetScalingFactor(Links links) {
            var avatarMainScale = Math.Abs(links.avatarMain.transform.lossyScale.x);
            var propMainScale = Math.Abs(links.propMain.transform.lossyScale.x);
            var scalingFactor = model.skinRewriteScalingFactor;

            if (scalingFactor <= 0) {
                scalingFactor = propMainScale / avatarMainScale;
                if (model.scalingFactorPowersOf10Only) {
                    var log = Math.Log10(scalingFactor);
                    double Mod(double a, double n) => (a % n + n) % n;
                    log = (Mod(log, 1) > 0.75) ? Math.Ceiling(log) : Math.Floor(log);
                    scalingFactor = (float)Math.Pow(10, log);
                }
            }

            return (avatarMainScale, propMainScale, scalingFactor);
        }

        private void FailIfComponents(GameObject propBone) {
            foreach (var c in propBone.GetComponents<UnityEngine.Component>()) {
                if (c == null || c is Transform) {
                } else if (c is ParentConstraint) {
                    Object.DestroyImmediate(c);
                } else {
                    var path = clipBuilder.GetPath(propBone);
                    throw new VRCFBuilderException(
                        "Prop bone " + path + " contains a " + c.GetType().Name + " component" +
                        " which would be lost during Armature Link because the bone is being merged." +
                        " If this component needs to be kept, it should be moved to a child object.");
                }
            }
        }
        
        private void UpdateConstraints(GameObject propBone, GameObject avatarBone) {
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<IConstraint>()) {
                UpdateConstraint(propBone, avatarBone, c);
            }
        }

        private void UpdateConstraint(GameObject propBone, GameObject avatarBone, IConstraint constraint) {
            List<ConstraintSource> sources = new List<ConstraintSource>();
            constraint.GetSources(sources);
            var changed = false;
            for (var i = 0; i < sources.Count; i++) {
                if (sources[i].sourceTransform == propBone.transform) {
                    var newSource = sources[i];
                    newSource.sourceTransform = avatarBone.transform;
                    sources[i] = newSource;
                    changed = true;
                }
            }
            if (changed) {
                constraint.SetSources(sources);
            }
            // TODO: Update the rest offsets if the bone moved as a result of the merge
        }

        private void UpdatePhysbones(GameObject propBone, GameObject avatarBone) {
            foreach (var physbone in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBone>()) {
                var root = physbone.GetRootTransform();
                if (propBone.transform == root) {
                    if (model.physbonesOnAvatarBones) {
                        physbone.rootTransform = avatarBone.transform;
                    } else {
                        var physbonePath = clipBuilder.GetPath(physbone.gameObject);
                        throw new VRCFBuilderException(
                            "Physbone " + physbonePath + " points to a bone that is going to" +
                            " stop existing because it is being merged into the avatar using Armature Link." +
                            " If this physbone needs to exist, it should be placed on a new child object of the linked bone.");
                    }
                }
            }
        }
        
        private void UpdatePhysboneColliders(GameObject propBone, GameObject avatarBone, bool scalingRequired, float scalingFactor, bool keepBoneOffsets) {
            foreach (var collider in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneCollider>()) {
                var root = collider.GetRootTransform();
                if (propBone.transform == root) {
                    if (scalingRequired || keepBoneOffsets) {
                        var childBone = new GameObject("vrcf_" + uniqueModelNum + "_" + propBone.name);
                        if (scalingRequired) {
                            childBone.transform.localScale = Vector3.one * scalingFactor;
                        }
                        childBone.transform.SetParent(avatarBone.transform, false);
                        if (keepBoneOffsets) {
                            childBone.transform.position = propBone.transform.position;
                            childBone.transform.rotation = propBone.transform.rotation;
                            var lossyScale = propBone.transform.lossyScale;
                            childBone.transform.localScale *= Mathf.Max(lossyScale.x, lossyScale.y, lossyScale.z);
                        }
                        collider.rootTransform = childBone.transform;
                    } else {
                        collider.rootTransform = avatarBone.transform;
                    }
                }
            }
        }

        private ArmatureLink.ArmatureLinkMode GetLinkMode() {
            if (model.linkMode == ArmatureLink.ArmatureLinkMode.Auto) {
                var usesBonesFromProp = false;
                var propRoot = model.propBone;
                if (propRoot != null) {
                    foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                        if (skin.transform.IsChildOf(propRoot.transform)) continue;
                        usesBonesFromProp |= skin.rootBone && skin.rootBone.IsChildOf(propRoot.transform);
                        usesBonesFromProp |= skin.bones.Any(bone => bone && bone.IsChildOf(propRoot.transform));
                    }
                }

                return usesBonesFromProp
                    ? ArmatureLink.ArmatureLinkMode.SkinRewrite
                    : ArmatureLink.ArmatureLinkMode.ReparentRoot;
            }

            return model.linkMode;
        }

        private bool GetKeepBoneOffsets(ArmatureLink.ArmatureLinkMode linkMode) {
            if (model.keepBoneOffsets2 == ArmatureLink.KeepBoneOffsets.Auto) {
                return linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot;
            }
            return model.keepBoneOffsets2 == ArmatureLink.KeepBoneOffsets.Yes;
        }

        private class Links {
            // These are stacks, because it's convenient, and we want to iterate over them in reverse order anyways
            // because when operating on the vrc clone, we delete game objects as we process them, and we want to
            // delete the children first.

            public VFGameObject propMain;
            public VFGameObject avatarMain;
            
            // left=bone in prop | right=bone in avatar
            public readonly Stack<Tuple<VFGameObject, VFGameObject>> mergeBones
                = new Stack<Tuple<VFGameObject, VFGameObject>>();
            
            // left=object to move | right=new parent
            public readonly Stack<Tuple<VFGameObject, VFGameObject>> reparent
                = new Stack<Tuple<VFGameObject, VFGameObject>>();
        }

        private Links GetLinks() {
            VFGameObject propBone = model.propBone;
            if (propBone == null) return null;

            VFGameObject avatarBone = null;

            if (string.IsNullOrWhiteSpace(model.bonePathOnAvatar)) {
                try {
                    avatarBone = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, model.boneOnAvatar);
                } catch (Exception) {
                    foreach (var fallback in model.fallbackBones) {
                        avatarBone = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, fallback);
                        if (avatarBone) break;
                    }
                    if (!avatarBone) {
                        throw;
                    }
                }
            } else {
                avatarBone = avatarObject.transform.Find(model.bonePathOnAvatar)?.gameObject;
                if (avatarBone == null) {
                    throw new VRCFBuilderException(
                        "ArmatureLink failed to find " + model.bonePathOnAvatar + " bone on avatar.");
                }
            }

            if (avatarBone == propBone) {
                throw new VRCFBuilderException(
                    "The object dragged into Armature Link should not be a bone from the avatar's armature." +
                    " If you are linking clothes, be sure to drag in the main bone from the clothes' armature instead!");
            }

            var removeBoneSuffix = model.removeBoneSuffix;
            if (string.IsNullOrWhiteSpace(model.removeBoneSuffix)) {
                var avatarBoneName = avatarBone.name;
                var propBoneName = propBone.name;
                if (propBoneName.Contains(avatarBoneName) && propBoneName != avatarBoneName) {
                    removeBoneSuffix = propBoneName.Replace(avatarBoneName, "");
                }
            }
            
            var links = new Links();

            var checkStack = new Stack<Tuple<VFGameObject, VFGameObject>>();
            checkStack.Push(Tuple.Create(propBone, avatarBone));
            links.mergeBones.Push(Tuple.Create(propBone, avatarBone));

            while (checkStack.Count > 0) {
                var check = checkStack.Pop();
                foreach (var childPropBone in check.Item1.Children()) {
                    var searchName = childPropBone.name;
                    if (!string.IsNullOrWhiteSpace(removeBoneSuffix)) {
                        searchName = searchName.Replace(removeBoneSuffix, "");
                    }
                    var childAvatarBone = check.Item2.Find(searchName);
                    // Hack for Rexouium model, which added ChestUp bone at some point and broke a ton of old props
                    if (!childAvatarBone) {
                        childAvatarBone = check.Item2.Find("ChestUp/" + searchName);
                    }
                    if (childAvatarBone) {
                        var marshmallowChild = GetMarshmallowChild(childAvatarBone);
                        if (marshmallowChild != null) childAvatarBone = marshmallowChild;
                    }
                    if (childAvatarBone != null) {
                        links.mergeBones.Push(Tuple.Create(childPropBone, childAvatarBone));
                        checkStack.Push(Tuple.Create(childPropBone, childAvatarBone));
                    } else {
                        links.reparent.Push(Tuple.Create(childPropBone, check.Item2));
                    }
                }
            }

            links.propMain = propBone;
            links.avatarMain = avatarBone;

            return links;
        }

        // Marshmallow PB unity package inserts fake bones in the armature, breaking our link.
        // Detect if this happens, and return the proper child bone instead.
        private static VFGameObject GetMarshmallowChild(VFGameObject orig) {
            var scaleConstraint = orig.GetComponent<ScaleConstraint>();
            if (scaleConstraint == null) return null;
            if (scaleConstraint.sourceCount != 1) return null;
            var source = scaleConstraint.GetSource(0);
            if (source.sourceTransform == null) return null;
            var scaleTargetInMarshmallow = source.sourceTransform
                .asVf()
                .GetSelfAndAllParents()
                .Any(t => t.name.ToLower().Contains("marshmallow"));
            if (!scaleTargetInMarshmallow) return null;
            var child = orig.transform.Find(orig.name);
            if (!child) return null;
            return child.gameObject;
        }

        public override string GetEditorTitle() {
            return "Armature Link";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.Info(
                "This feature will link an armature in a prop to the armature on the avatar base." +
                " It can also be used to link a single object in the prop to a certain bone on the avatar's armature."));

            container.Add(VRCFuryEditorUtils.WrappedLabel("Link From:",
                style => style.unityFontStyleAndWeight = FontStyle.Bold));
            container.Add(VRCFuryEditorUtils.WrappedLabel(
                "For clothing, this should be the Hips bone in the clothing's Armature (or the 'main' bone if it doesn't have Hips).\n" +
                "For non-clothing objects (things that you just want to re-parent), this should be the object you want moved."));
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("propBone")));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.WrappedLabel("Link To:",
                style => style.unityFontStyleAndWeight = FontStyle.Bold));
            var rootBoneLabelWhenSkin = VRCFuryEditorUtils.WrappedLabel(
                "Select the bone that matches the one you selected in the clothing above.");
            var rootBoneLabelWhenReparent = VRCFuryEditorUtils.WrappedLabel(
                "Select the bone you want to attach this object to.");
            rootBoneLabelWhenReparent.style.display = DisplayStyle.None;
            container.Add(rootBoneLabelWhenSkin);
            container.Add(rootBoneLabelWhenReparent);
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("boneOnAvatar")));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            
            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            };
            container.Add(adv);
            
            adv.Add(VRCFuryEditorUtils.WrappedLabel("Link Mode:"));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("(Skin Rewrite) Rewrites skinned meshes to use avatar's own bones. Excellent performance, but breaks some clothing."));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("(Merge as Children) Makes prop bones into children of the avatar's bones. Medium performance, but often works when Skin Rewrite doesn't."));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("(Reparent Root) The prop object is moved into the avatar's bone. No other merging takes place."));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("(Bone Constraint) Adds a parent constraint to every prop bone, linking it to the avatar bone. Awful performance, pretty much never use this."));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("(Auto) Selects Skin Rewrite if a mesh uses bones from the prop armature, or Reparent Root otherwise."));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("linkMode"), formatEnum: str => {
                if (str == ArmatureLink.ArmatureLinkMode.SkinRewrite.ToString()) {
                    return "Skin Rewrite";
                } else if (str == ArmatureLink.ArmatureLinkMode.MergeAsChildren.ToString()) {
                    return "Merge as Children";
                } else if (str == ArmatureLink.ArmatureLinkMode.ReparentRoot.ToString()) {
                    return "Reparent Root";
                } else if (str == ArmatureLink.ArmatureLinkMode.ParentConstraint.ToString()) {
                    return "Bone Constraint";
                } else if (str == ArmatureLink.ArmatureLinkMode.Auto.ToString()) {
                    return "Auto";
                }

                return str;
            }));
            
            adv.Add(new VisualElement { style = { paddingTop = 10 } });
            adv.Add(VRCFuryEditorUtils.WrappedLabel("Remove bone suffix/prefix:"));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("If set, this substring will be removed from all bone names in the prop. This is useful for props where the artist added " +
                                                    "something like _PropName to the end of every bone, breaking AvatarLink in the process. If empty, the suffix will be predicted " +
                                                    "based on the difference between the name of the given root bones."));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("removeBoneSuffix")));
            
            adv.Add(new VisualElement { style = { paddingTop = 10 } });
            adv.Add(VRCFuryEditorUtils.WrappedLabel("String path to bone on avatar:"));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("If provided, humanoid bone dropdown will be ignored."));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("bonePathOnAvatar")));

            adv.Add(new VisualElement { style = { paddingTop = 10 } });
            adv.Add(VRCFuryEditorUtils.WrappedLabel("Keep bone offsets:"));
            adv.Add(VRCFuryEditorUtils.WrappedLabel(
                "If no, linked bones will be rigidly locked to the transform of the corresponding avatar bone."));
            adv.Add(VRCFuryEditorUtils.WrappedLabel(
                "If yes, prop bones will maintain their initial offset to the corresponding avatar bone. This is unusual."));
            adv.Add(VRCFuryEditorUtils.WrappedLabel(
                "If auto, offsets will be kept only if Reparent Root link mode is used."));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("keepBoneOffsets2")));

            adv.Add(new VisualElement { style = { paddingTop = 10 } });
            adv.Add(VRCFuryEditorUtils.WrappedLabel("Allow prop physbones to target avatar bone transforms (unusual):"));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("If checked, physbones in the prop pointing to bones on the avatar will be updated " +
                                                    "to point to the corresponding bone on the base armature. This is extremely unusual. Don't use this " +
                                                    "unless you know what you are doing."));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("physbonesOnAvatarBones")));
            
            adv.Add(new VisualElement { style = { paddingTop = 10 } });
            
            adv.Add(VRCFuryEditorUtils.WrappedLabel("Fallback bones:"));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("If the given bone cannot be found on the avatar, these bones will also be attempted before failing."));
            adv.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("fallbackBones")));
            
            adv.Add(new VisualElement { style = { paddingTop = 10 } });
            
            adv.Add(VRCFuryEditorUtils.WrappedLabel("Skin rewrite scaling factor:"));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("(Will automatically detect scaling factor if 0)"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("skinRewriteScalingFactor")));
            
            adv.Add(new VisualElement { style = { paddingTop = 10 } });
            
            adv.Add(VRCFuryEditorUtils.WrappedLabel("Restrict automatic scaling factor to powers of 10:"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("scalingFactorPowersOf10Only")));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                if (avatarObject == null) {
                    return "Avatar descriptor is missing";
                }

                var linkMode = GetLinkMode();
                rootBoneLabelWhenReparent.style.display = linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot ? DisplayStyle.Flex : DisplayStyle.None;
                rootBoneLabelWhenSkin.style.display = linkMode != ArmatureLink.ArmatureLinkMode.ReparentRoot ? DisplayStyle.Flex : DisplayStyle.None;
                
                var links = GetLinks();
                var keepBoneOffsets = GetKeepBoneOffsets(linkMode);
                var text = new List<string>();
                var (avatarMainScale, propMainScale, scalingFactor) = GetScalingFactor(links);
                text.Add("Merging to bone: " + 
                                   AnimationUtility.CalculateTransformPath(links.avatarMain.transform, avatarObject.transform));
                text.Add("Link Mode: " + linkMode);
                text.Add("Keep Bone Offsets: " + keepBoneOffsets);
                if (!keepBoneOffsets) {
                    text.Add("Prop root bone scale: " + propMainScale);
                    text.Add("Avatar root bone scale: " + avatarMainScale);
                    text.Add("Scaling factor: " + scalingFactor);
                }
                if (linkMode != ArmatureLink.ArmatureLinkMode.ReparentRoot && links.reparent.Count > 0) {
                    text.Add(
                        "These bones do not have a match on the avatar and will be added as new children: \n" +
                        string.Join("\n",
                            links.reparent.Select(b =>
                                "* " + AnimationUtility.CalculateTransformPath(b.Item1.transform,
                                    model.propBone.transform))));
                }

                return string.Join("\n", text);
            }));

            return container;
        }
    }
}
