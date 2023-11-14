﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Feature {

    public class ArmatureLinkBuilder : FeatureBuilder<ArmatureLink> {
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly FindAnimatedTransformsService findAnimatedTransformsService;

        [FeatureBuilderAction(FeatureOrder.ArmatureLinkBuilder)]
        public void Apply() {
            if (model.propBone == null) {
                Debug.LogWarning("Root bone is null on armature link.");
                return;
            }

            var links = GetLinks();
            if (links == null) {
                return;
            }

            var linkMode = GetLinkMode();
            
            // Lock bones to avatar positions
            {
                var keepBoneOffsets = GetKeepBoneOffsets(linkMode);
                if (linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot) {
                    if (!keepBoneOffsets) {
                        links.propMain.worldPosition = links.avatarMain.worldPosition;
                        links.propMain.worldRotation = links.avatarMain.worldRotation;
                        links.propMain.worldScale = links.avatarMain.worldScale;
                    }
                } else if (!keepBoneOffsets) {
                    var (_, _, scalingFactor) = GetScalingFactor(links);
                    Debug.Log("Detected scaling factor: " + scalingFactor);
                    foreach (var (propBone, avatarBone) in links.mergeBones.Reverse()) {
                        propBone.worldPosition = avatarBone.worldPosition;
                        propBone.worldRotation = avatarBone.worldRotation;
                        propBone.worldScale = avatarBone.worldScale * scalingFactor;
                    }
                }
            }

            if (linkMode == ArmatureLink.ArmatureLinkMode.SkinRewrite || linkMode == ArmatureLink.ArmatureLinkMode.MergeAsChildren || linkMode == ArmatureLink.ArmatureLinkMode.ParentConstraint) {
                var anim = findAnimatedTransformsService.Find();
                // Some artists do a dumb thing and put a physbone on the clothing's hips (for things like a skirt), but don't
                // ignore any transforms (which would cause our merger to avoid merging things like... the avatar's arms)
                // We fix this by ignoring types of animations on those bones
                var avatarHumanoidBones = VRCFArmatureUtils.GetAllBones(avatarObject).ToImmutableHashSet();
                foreach (var (propBone, avatarBone) in links.mergeBones) {
                    if (avatarHumanoidBones.Contains(avatarBone)) {
                        anim.positionIsAnimated.Remove(propBone);
                        anim.rotationIsAnimated.Remove(propBone);
                        anim.physboneChild.Remove(propBone);
                        anim.physboneRoot.Remove(propBone);
                    }
                }

                var doNotMerge = new HashSet<Transform>();
                doNotMerge.UnionWith(anim.positionIsAnimated);
                doNotMerge.UnionWith(anim.rotationIsAnimated);
                doNotMerge.UnionWith(anim.physboneChild);
                // Recursively add all children
                doNotMerge.UnionWith(doNotMerge.ToArray().SelectMany(t => t.asVf().GetSelfAndAllChildren().Select(o => o.transform)));

                var doNotRebindSkins = new HashSet<Transform>();
                doNotRebindSkins.UnionWith(anim.scaleIsAnimated);
                doNotRebindSkins.UnionWith(anim.physboneRoot); // (physbone roots can rotate)
                doNotRebindSkins.UnionWith(doNotMerge);

                var debugLog = "";
                foreach (var (propBone,avatarBone) in links.mergeBones) {
                    if (doNotRebindSkins.Contains(propBone)) {
                        debugLog += propBone.GetPath(links.propMain) + ": " + string.Join(",", anim.GetDebugSources(propBone)) + "\n";
                    }
                }
                if (debugLog != "") {
                    Debug.LogWarning(
                        "These bones would have been merged, but are not because they were impacted by animations:\n" +
                        debugLog);
                }
                
                var rootName = GetRootName(links.propMain);
                
                var skinRewriteMapping = new Dictionary<Transform, Transform>();
                foreach (var (propBone, avatarBone) in links.mergeBones) {
                    skinRewriteMapping[propBone.transform] = avatarBone.transform;
                }

                foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                    // Update skins to use bones and bind poses from the original avatar
                    if (skin.bones.Any(b => b != null && skinRewriteMapping.ContainsKey(b))) {
                        if (skin.sharedMesh) {
                            skin.sharedMesh = MutableManager.MakeMutable(skin.sharedMesh);
                            var mesh = skin.sharedMesh;
                            mesh.bindposes = Enumerable.Zip(skin.bones, mesh.bindposes, (a,b) => (a,b))
                                .Select(boneAndBindPose => {
                                    VFGameObject bone = boneAndBindPose.a;
                                    var bindPose = boneAndBindPose.b;
                                    if (bone == null) return bindPose;
                                    if (doNotRebindSkins.Contains(bone)) return bindPose;
                                    if (skinRewriteMapping.TryGetValue(bone, out var mergedTo)) {
                                        return mergedTo.worldToLocalMatrix * bone.localToWorldMatrix * bindPose;
                                    }
                                    return bindPose;
                                }) 
                                .ToArray();
                            VRCFuryEditorUtils.MarkDirty(mesh);
                        }

                        skin.bones = skin.bones
                            .Select(b => {
                                if (b == null) return b;
                                if (doNotRebindSkins.Contains(b)) return b;
                                if (skinRewriteMapping.TryGetValue(b, out var to)) return to;
                                return b;
                            })
                            .ToArray();
                        
                        VRCFuryEditorUtils.MarkDirty(skin);
                    }
                    
                    // Update skin to use root bone from the original avatar (updating bounds if needed)
                    {
                        var oldRootBone = HapticUtils.GetMeshRoot(skin);
                        if (skinRewriteMapping.TryGetValue(oldRootBone, out var newRootBone) && !doNotRebindSkins.Contains(oldRootBone)) {
                            var b = skin.localBounds;
                            b.center = new Vector3(
                                b.center.x * oldRootBone.lossyScale.x / newRootBone.lossyScale.x,
                                b.center.y * oldRootBone.lossyScale.y / newRootBone.lossyScale.y,
                                b.center.z * oldRootBone.lossyScale.z / newRootBone.lossyScale.z
                            );
                            b.extents = new Vector3(
                                b.extents.x * oldRootBone.lossyScale.x / newRootBone.lossyScale.x,
                                b.extents.y * oldRootBone.lossyScale.y / newRootBone.lossyScale.y,
                                b.extents.z * oldRootBone.lossyScale.z / newRootBone.lossyScale.z
                            );
                            skin.localBounds = b;

                            skin.rootBone = newRootBone;
                        }
                    }
                }

                // Move over all the old components / children from the old location to a new child
                var animLink = new VFMultimap<VFGameObject, VFGameObject>();
                foreach (var (propBone, avatarBone) in links.mergeBones) {
                    if (doNotMerge.Contains(propBone) && doNotMerge.Contains(propBone.parent) && propBone != links.propMain) {
                        continue;
                    }

                    // Rip out parent constraints, since they were likely there from an old pre-vrcfury merge process
                    foreach (var c in propBone.GetComponents<ParentConstraint>()) {
                        Object.DestroyImmediate(c);
                    }

                    // If the transform isn't used and contains no children, we can just throw it away
                    if (!IsTransformUsed(propBone)) {
                        propBone.Destroy();
                        continue;
                    }

                    var animatedParents = new List<VFGameObject>();
                    {
                        var o = propBone.parent;
                        var parents = new List<VFGameObject>();
                        while (o != null && o != avatarObject && !avatarBone.IsChildOf(o)) {
                            parents.Add(o);
                            o = o.parent;
                        }
                        parents.Reverse();
                        foreach (var parent in parents) {
                            if (anim.activated.Contains(parent) || !parent.active) {
                                animatedParents.Add(parent);
                            }
                        }
                    }

                    // Move it on over
                    var newName = $"[VF{uniqueModelNum}] {propBone.name} from {rootName}";
                    if (anim.physboneChild.Contains(propBone)) {
                        newName += " (Child of PhysBone)";
                    } else if (anim.positionIsAnimated.Contains(propBone)) {
                        newName += " (Animated Position)";
                    } else if (anim.physboneRoot.Contains(propBone)) {
                        newName += " (Root of Physbone)";
                    } else if (anim.rotationIsAnimated.Contains(propBone)) {
                        newName += " (Animated Rotation)";
                    } else if (anim.scaleIsAnimated.Contains(propBone)) {
                        newName += " (Animated Scale)";
                    } else if (propBone.Children().Any()) {
                        newName += " (Added Children)";
                    } else if (propBone.GetComponents<UnityEngine.Component>().Length > 1) {
                        newName += " (Added Components)";
                    } else {
                        newName += " (Referenced Externally)";
                    }

                    if (animatedParents.Count == 0) {
                        mover.Move(propBone, avatarBone, newName);
                    } else {
                        var current = GameObjects.Create(newName, avatarBone);
                        foreach (var a in animatedParents) {
                            current = GameObjects.Create($"Toggle From {a.name}", current);
                            current.active = a.active;
                            animLink.Put(a, current);
                        }
                        mover.Move(propBone, current, "Merged Object");
                    }
                }
                
                // Rewrite animations that turn off parents
                foreach (var clip in manager.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                    foreach (var binding in clip.GetFloatBindings()) {
                        if (binding.type != typeof(GameObject)) continue;
                        var transform = avatarObject.Find(binding.path).transform;
                        if (transform == null) continue;
                        foreach (var other in animLink.Get(transform)) {
                            var b = binding;
                            b.path = other.GetPath(avatarObject);
                            clip.SetFloatCurve(b, clip.GetFloatCurve(binding));
                        }
                    }
                }
            } else if (linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot) {
                var propBone = links.propMain;
                var avatarBone = links.avatarMain;
                foreach (var c in propBone.GetComponents<ParentConstraint>()) {
                    Object.DestroyImmediate(c);
                }
                mover.Move(
                    propBone,
                    avatarBone,
                    $"[VF{uniqueModelNum}] {propBone.name}"
                );
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

        private bool IsTransformUsed(Transform transform) {
            if (transform.childCount > 0) return true;
            if (transform.GetComponents<UnityEngine.Component>().Length > 1) return true;
            
            foreach (var s in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                if (s.bones.Contains(transform)) return true;
                if (s.rootBone == transform) return true;
            }
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<IConstraint>()) {
                if (Enumerable.Range(0, c.sourceCount)
                    .Select(i => c.GetSource(i))
                    .Any(source => source.sourceTransform == transform)
                ) {
                    return true;
                }
            }

            if (avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneBase>()
                .Any(b => b.GetRootTransform() == transform)) {
                return true;
            }
            if (avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneColliderBase>()
                .Any(b => b.GetRootTransform() == transform)) {
                return true;
            }
            if (avatarObject.GetComponentsInSelfAndChildren<ContactBase>()
                .Any(b => b.GetRootTransform() == transform)) {
                return true;
            }

            return false;
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

        private string GetRootName(Transform rootBone) {
            if (rootBone == null) return "Unknown";

            var isBone = false;
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                isBone |= skin.rootBone == rootBone;
                isBone |= skin.bones.Contains(rootBone);
            }
            isBone |= rootBone.name.ToLower().Trim() == "armature";

            if (isBone) return GetRootName(rootBone.parent);

            return rootBone.name;
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
            public readonly Stack<(VFGameObject, VFGameObject)> mergeBones
                = new Stack<(VFGameObject, VFGameObject)>();
            
            // left=object to move | right=new parent
            public readonly Stack<(VFGameObject, VFGameObject)> reparent
                = new Stack<(VFGameObject, VFGameObject)>();
        }

        private Links GetLinks() {
            VFGameObject propBone = model.propBone;
            if (propBone == null) return null;

            foreach (var b in VRCFArmatureUtils.GetAllBones(avatarObject)) {
                if (b.IsChildOf(propBone)) {
                    throw new VRCFBuilderException(
                        "Link From is part of the avatar's armature." +
                        " The object dragged into Armature Link should not be a bone from the avatar's armature." +
                        " If you are linking clothes, be sure to drag in the main bone from the clothes' armature instead!");
                }
            }

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

            var removeBoneSuffix = model.removeBoneSuffix;
            if (string.IsNullOrWhiteSpace(model.removeBoneSuffix)) {
                var avatarBoneName = avatarBone.name;
                var propBoneName = propBone.name;
                if (propBoneName.Contains(avatarBoneName) && propBoneName != avatarBoneName) {
                    removeBoneSuffix = propBoneName.Replace(avatarBoneName, "");
                }
            }
            
            var links = new Links();

            var checkStack = new Stack<(VFGameObject, VFGameObject)>();
            checkStack.Push((propBone, avatarBone));
            links.mergeBones.Push((propBone, avatarBone));

            while (checkStack.Count > 0) {
                var (checkPropBone, checkAvatarBone) = checkStack.Pop();
                foreach (var childPropBone in checkPropBone.Children()) {
                    var searchName = childPropBone.name;
                    if (!string.IsNullOrWhiteSpace(removeBoneSuffix)) {
                        searchName = searchName.Replace(removeBoneSuffix, "");
                    }
                    var childAvatarBone = checkAvatarBone.Find(searchName);
                    // Hack for Rexouium model, which added ChestUp bone at some point and broke a ton of old props
                    if (!childAvatarBone) {
                        childAvatarBone = checkAvatarBone.Find("ChestUp/" + searchName);
                    }
                    if (childAvatarBone) {
                        var marshmallowChild = GetMarshmallowChild(childAvatarBone);
                        if (marshmallowChild != null) childAvatarBone = marshmallowChild;
                    }

                    if (childAvatarBone != null) {
                        links.mergeBones.Push((childPropBone, childAvatarBone));
                        checkStack.Push((childPropBone, childAvatarBone));
                    } else {
                        links.reparent.Push((childPropBone, checkAvatarBone));
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
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("linkMode")));
            
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
                if (links == null) {
                    return "No valid link target found";
                }
                var keepBoneOffsets = GetKeepBoneOffsets(linkMode);
                var text = new List<string>();
                var (avatarMainScale, propMainScale, scalingFactor) = GetScalingFactor(links);
                text.Add($"Merging to bone: {links.avatarMain.GetPath(avatarObject)}");
                text.Add($"Link Mode: {linkMode}");
                text.Add($"Keep Bone Offsets: {keepBoneOffsets}");
                if (!keepBoneOffsets) {
                    text.Add($"Prop root bone scale: {propMainScale}");
                    text.Add($"Avatar root bone scale: {avatarMainScale}");
                    text.Add($"Scaling factor: {scalingFactor}");
                }
                if (linkMode != ArmatureLink.ArmatureLinkMode.ReparentRoot && links.reparent.Count > 0) {
                    text.Add(
                        "These bones do not have a match on the avatar and will be added as new children: \n" +
                        string.Join("\n",
                            links.reparent.Select(b =>
                                "* " + b.Item1.GetPath(links.propMain))));
                }

                return string.Join("\n", text);
            }));

            return container;
        }
    }
}
