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
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace VF.Feature {

    public class ArmatureLinkBuilder : FeatureBuilder<ArmatureLink> {
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly FindAnimatedTransformsService findAnimatedTransformsService;
        [VFAutowired] private readonly FakeHeadService fakeHead;

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
            var keepBoneOffsets = GetKeepBoneOffsets(linkMode);

            var (_, _, scalingFactor) = GetScalingFactor(links, linkMode);
            Debug.Log("Detected scaling factor: " + scalingFactor);

            var anim = findAnimatedTransformsService.Find();
            var avatarHumanoidBones = VRCFArmatureUtils.GetAllBones(avatarObject).ToImmutableHashSet();

            var doNotReparent = new HashSet<Transform>();
            // We still reparent scale-animated things, because some users take advantage of this to "scale to 0" every bone
            doNotReparent.UnionWith(anim.positionIsAnimated.Children());
            doNotReparent.UnionWith(anim.rotationIsAnimated.Children());
            doNotReparent.UnionWith(anim.physboneRoot.Children()); // Physbone roots are the same as rotation being animated
            doNotReparent.UnionWith(anim.physboneChild); // Physbone children can't be reparented, because they must remain as children of the physbone root

            // Expand the list to include all transitive children
            doNotReparent.UnionWith(doNotReparent.AllChildren().ToArray());

            var rootName = GetRootName(links.propMain);

            // Move over all the old components / children from the old location to a new child
            var animLink = new VFMultimapList<VFGameObject, VFGameObject>();
            foreach (var (propBone, avatarBone) in links.mergeBones) {
                bool ShouldReparent() {
                    if (propBone == links.propMain) return true;
                    if (linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot) return false;
                    if (avatarHumanoidBones.Contains(avatarBone)) return true;
                    if (doNotReparent.Contains(propBone)) return false;
                    return true;
                }
                bool ShouldReuseBone() {
                    if (linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot) return false; // See note below why we don't do this for ReparentRoot
                    if (anim.positionIsAnimated.Contains(propBone)) return false;
                    if (anim.rotationIsAnimated.Contains(propBone)) return false;
                    if (anim.scaleIsAnimated.Contains(propBone)) return false;
                    if (anim.physboneRoot.Contains(propBone)) return false;
                    if (anim.physboneChild.Contains(propBone)) return false;
                    return true;
                }

                if (!ShouldReparent()) {
                    continue;
                }

                // Rip out parent constraints, since they were likely there from an old pre-vrcfury merge process
                foreach (var c in propBone.GetComponents<ParentConstraint>()) {
                    Object.DestroyImmediate(c);
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
                var newName = $"[VF{uniqueModelNum}] {propBone.name}";
                if (propBone.name != rootName) newName += $" from {rootName}";

                var addedObject = GameObjects.Create(newName, avatarBone, useTransformFrom: propBone);
                var current = addedObject;

                foreach (var a in animatedParents) {
                    current = GameObjects.Create($"Toggle From {a.name}", current);
                    current.active = a.active;
                    animLink.Put(a, current);
                }

                var transformAnimated =
                    anim.positionIsAnimated.Contains(propBone)
                    || anim.rotationIsAnimated.Contains(propBone)
                    || anim.scaleIsAnimated.Contains(propBone);
                if (transformAnimated) {
                    current = GameObjects.Create("Original Parent (Retained for transform animation)", current, propBone.parent);

                    // In a weird edge case, sometimes people mark all their clothing bones with an initial scale of 0,
                    // to mark them as initially "hidden". In this case, we need to make sure that the transform maintainer
                    // doesn't just permanently set the scale to 0.
                    if (current.localScale.x == 0 || current.localScale.y == 0 || current.localScale.z == 0) {
                        current.localScale = Vector3.one;
                    }
                }

                mover.Move(propBone, current, "Original Object", defer: true);
                
                if (!keepBoneOffsets) {
                    addedObject.worldPosition = avatarBone.worldPosition;
                    addedObject.worldRotation = avatarBone.worldRotation;
                    addedObject.worldScale = avatarBone.worldScale * scalingFactor;
                }

                if (ShouldReuseBone()) {
                    RewriteSkins(propBone, avatarBone);
                }

                if (fakeHead.IsEligible(propBone)) {
                    fakeHead.MarkEligible(addedObject);
                }

                // If the transform isn't used and contains no children, we can just throw it away
                var keepReasons = GetUsageReasons(propBone);
                if (keepReasons.Count == 0) {
                    addedObject.Destroy();
                    continue;
                }

                keepReasons.UnionWith(anim.GetDebugSources(propBone));
                var debugInfo = addedObject.AddComponent<VRCFuryDebugInfo>();
                debugInfo.debugInfo = "VRCFury did not fully merge this object because:\n";
                debugInfo.debugInfo += string.Join("\n", keepReasons.OrderBy(a => a));
            }

            mover.ApplyDeferred();

            // Rewrite animations that turn off parents
            foreach (var clip in manager.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                foreach (var binding in clip.GetFloatBindings()) {
                    if (binding.type != typeof(GameObject)) continue;
                    var transform = avatarObject.Find(binding.path).transform;
                    if (transform == null) continue;
                    foreach (var other in animLink.Get(transform)) {
                        if (other == null) continue; // it got deleted because the propBone wasn't used
                        var b = binding;
                        b.path = other.GetPath(avatarObject);
                        clip.SetFloatCurve(b, clip.GetFloatCurve(binding));
                    }
                }
            }
        }

        private void RewriteSkins(Transform fromBone, Transform toBone) {
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                // Update skins to use bones and bind poses from the original avatar
                if (skin.bones.Contains(fromBone)) {
                    var mesh = skin.GetMutableMesh();
                    if (mesh != null) {
                        mesh.bindposes = Enumerable.Zip(skin.bones, mesh.bindposes, (a,b) => (a,b))
                            .Select(boneAndBindPose => {
                                VFGameObject bone = boneAndBindPose.a;
                                var bindPose = boneAndBindPose.b;
                                if (bone != fromBone) return bindPose;
                                return toBone.worldToLocalMatrix * bone.localToWorldMatrix * bindPose;
                            }) 
                            .ToArray();
                    }

                    skin.bones = skin.bones
                        .Select(b => b == fromBone ? toBone : b)
                        .ToArray();
                    VRCFuryEditorUtils.MarkDirty(skin);
                }
            }
        }

        private (float, float, float) GetScalingFactor(Links links, ArmatureLink.ArmatureLinkMode linkMode) {
            var avatarMainScale = Math.Abs(links.avatarMain.transform.lossyScale.x);
            var propMainScale = Math.Abs(links.propMain.transform.lossyScale.x);
            var scalingFactor = model.skinRewriteScalingFactor;

            if (scalingFactor <= 0) {
                if (linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot) {
                    scalingFactor = 1;
                } else {
                    scalingFactor = propMainScale / avatarMainScale;
                    if (model.scalingFactorPowersOf10Only) {
                        var log = Math.Log10(scalingFactor);
                        double Mod(double a, double n) => (a % n + n) % n;
                        log = (Mod(log, 1) > 0.75) ? Math.Ceiling(log) : Math.Floor(log);
                        scalingFactor = (float)Math.Pow(10, log);
                    }
                }
            }

            return (avatarMainScale, propMainScale, scalingFactor);
        }

        private HashSet<string> GetUsageReasons(Transform transform) {
            var reasons = new HashSet<string>();

            if (transform.childCount > 0) {
                reasons.Add("Added children");
            }
            if (transform.GetComponents<UnityEngine.Component>().Length > 1) {
                reasons.Add("Added components");
            }

            foreach (var s in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                if (s.bones.Contains(transform)) {
                    reasons.Add("Bone in " + GetPath(s));
                }
                if (s.rootBone == transform) {
                    reasons.Add("Root bone in " + GetPath(s));
                }
            }
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<IConstraint>()) {
                if (Enumerable.Range(0, c.sourceCount)
                    .Select(i => c.GetSource(i))
                    .Any(source => source.sourceTransform == transform)
                ) {
                    reasons.Add("Target of constraint " + GetPath(c));
                }
            }
            foreach (var b in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneBase>()) {
                if (b.GetRootTransform() == transform) {
                    reasons.Add("Target of physbone " + GetPath(b));
                }
            }
            foreach (var b in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneColliderBase>()) {
                if (b.GetRootTransform() == transform) {
                    reasons.Add("Target of collider " + GetPath(b));
                }
            }
            foreach (var b in avatarObject.GetComponentsInSelfAndChildren<ContactBase>()) {
                if (b.GetRootTransform() == transform) {
                    reasons.Add("Target of contact " + GetPath(b));
                }
            }

            return reasons;
        }

        private string GetPath(object o) {
            if (o is UnityEngine.Component c) {
                return c.owner().GetPath(avatarObject);
            }
            return "";
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
        
        private void GetDragTarget() {
            var bonePathOnAvatarDragTarget = model.bonePathOnAvatarDragTarget;
            if (bonePathOnAvatarDragTarget != null) {
                var pathTarget = (VFGameObject)bonePathOnAvatarDragTarget;
                // Checking if the drag target is a bone on the avatar or attached to one
                try {
                    var humanoidBone = VRCFArmatureUtils.FindClosestBoneOrException(avatarObject, pathTarget);
                    // Found a bone attached to the prop, check if it's the drag target
                    var boneObject = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, humanoidBone);

                    if (boneObject.GetPath(avatarObject) == pathTarget.GetPath(avatarObject)) {
                        model.boneOnAvatar = humanoidBone;
                        model.bonePathOnAvatar = null;
                        model.bonePathOnAvatarDragTarget = null;
                    } else {
                        model.boneOnAvatar = humanoidBone;
                        model.bonePathOnAvatar = pathTarget.GetPath(boneObject);
                        model.bonePathOnAvatarDragTarget = null;
                    }
                } catch {
                    // Object wasn't attached to a humanoid bone! So just set the path absolutely.
                    model.bonePathOnAvatar = pathTarget.GetPath(avatarObject);
                    model.bonePathOnAvatarDragTarget = null;
                }
            }
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

            try {
                avatarBone = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, model.boneOnAvatar);
            } catch (Exception) {
                foreach (var fallback in model.fallbackBones) {
                    avatarBone = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, fallback);
                    if (avatarBone) break;
                }
                if (!avatarBone) {
                    throw new VRCFBuilderException(
                        "Failed to find " + model.boneOnAvatar + " bone on avatar. " +
                        "Did you rename one of your avatar's bones on accident?");
                }
            }
            
            if (!string.IsNullOrWhiteSpace(model.bonePathOnAvatar)) {
                // Check for path within humanoid bone
                // if nothing is there, check from avatar root instead
                if (avatarBone?.transform.Find(model.bonePathOnAvatar)?.gameObject) {
                    avatarBone = avatarBone?.transform.Find(model.bonePathOnAvatar)?.gameObject;
                } else {
                    avatarBone = avatarObject.transform.Find(model.bonePathOnAvatar)?.gameObject;
                }

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

            container.Add(VRCFuryEditorUtils.WrappedLabel("Link From:").Bold());
            container.Add(VRCFuryEditorUtils.WrappedLabel(
                "For clothing, this should be the Hips bone in the clothing's Armature (or the 'main' bone if it doesn't have Hips).\n" +
                "For non-clothing objects (things that you just want to re-parent), this should be the object you want moved."));
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("propBone")));

            container.Add(new VisualElement { style = { paddingTop = 10 } });
            container.Add(VRCFuryEditorUtils.WrappedLabel("Link To:").Bold());
            var rootBoneLabelWhenSkin = VRCFuryEditorUtils.WrappedLabel(
                "Select the bone that matches the one you selected in the clothing above.");
            var rootBoneLabelWhenReparent = VRCFuryEditorUtils.WrappedLabel(
                "Select the bone you want to attach this object to.");
            rootBoneLabelWhenReparent.SetVisible(false);
            container.Add(rootBoneLabelWhenSkin);
            container.Add(rootBoneLabelWhenReparent);
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("boneOnAvatar")));

            container.Add(VRCFuryEditorUtils.WrappedLabel(
                "Or drag in the object/bone directly here:"));
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("bonePathOnAvatarDragTarget")));
            container.Add(VRCFuryEditorUtils.WrappedLabel("Extra path to bone:"));
            container.Add(VRCFuryEditorUtils.WrappedLabel("Path from specified bone (or avatar root) to non-humanoid child bone."));
            container.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("bonePathOnAvatar")));
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

                GetDragTarget();
                
                var linkMode = GetLinkMode();
                rootBoneLabelWhenReparent.SetVisible(linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot);
                rootBoneLabelWhenSkin.SetVisible(linkMode != ArmatureLink.ArmatureLinkMode.ReparentRoot);
                
                var links = GetLinks();
                if (links == null) {
                    return "No valid link target found";
                }
                var keepBoneOffsets = GetKeepBoneOffsets(linkMode);
                
                var text = new List<string>();
                var (avatarMainScale, propMainScale, scalingFactor) = GetScalingFactor(links, linkMode);
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
