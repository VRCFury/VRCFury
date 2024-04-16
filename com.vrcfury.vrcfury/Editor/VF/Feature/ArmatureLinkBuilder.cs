using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
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
        [VFAutowired] private readonly ArmatureLinkHelperService armatureLinkHelper;

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

            var doNotReparent = new HashSet<VFGameObject>();
            // We still reparent scale-animated things, because some users take advantage of this to "scale to 0" every bone
            doNotReparent.UnionWith(anim.positionIsAnimated.Children());
            doNotReparent.UnionWith(anim.rotationIsAnimated.Children());
            doNotReparent.UnionWith(anim.physboneRoot.Children()); // Physbone roots are the same as rotation being animated
            doNotReparent.UnionWith(anim.physboneChild); // Physbone children can't be reparented, because they must remain as children of the physbone root

            // Expand the list to include all transitive children
            doNotReparent.UnionWith(doNotReparent.AllChildren().ToArray());

            var rootName = GetRootName(links.propMain);

            // Move over all the old components / children from the old location to a new child
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
                    var origName = armatureLinkHelper.GetOriginalName(a);
                    current = GameObjects.Create($"Toggle From {origName}", current);
                    armatureLinkHelper.SetOriginalName(current, origName);
                    current.active = a.active;
                    armatureLinkHelper.LinkEnableAnims(a, current);
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

                armatureLinkHelper.MarkAvailableForCleanup(addedObject);
            }

            mover.ApplyDeferred();
        }

        private void RewriteSkins(VFGameObject fromBone, VFGameObject toBone) {
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                // Update skins to use bones and bind poses from the original avatar
                if (skin.bones.Contains(fromBone.transform)) {
                    var mesh = skin.GetMutableMesh();
                    if (mesh != null) {
                        mesh.bindposes = Enumerable.Zip(skin.bones, mesh.bindposes, (a,b) => (a,b))
                            .Select(boneAndBindPose => {
                                var bone = boneAndBindPose.a.asVf();
                                var bindPose = boneAndBindPose.b;
                                if (bone != fromBone) return bindPose;
                                return toBone.worldToLocalMatrix * bone.localToWorldMatrix * bindPose;
                            }) 
                            .ToArray();
                    }

                    skin.bones = skin.bones
                        .Select(b => b == fromBone ? toBone.transform : b)
                        .ToArray();
                    VRCFuryEditorUtils.MarkDirty(skin);
                }
            }
        }

        private (float, float, float) GetScalingFactor(Links links, ArmatureLink.ArmatureLinkMode linkMode) {
            var avatarMainScale = Math.Abs(links.avatarMain.worldScale.x);
            var propMainScale = Math.Abs(links.propMain.worldScale.x);
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

        private ArmatureLink.ArmatureLinkMode GetLinkMode() {
            if (model.linkMode == ArmatureLink.ArmatureLinkMode.Auto) {
                var usesBonesFromProp = false;
                var propRoot = model.propBone.asVf();
                if (propRoot != null) {
                    foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                        if (skin.owner().IsChildOf(propRoot)) continue;
                        usesBonesFromProp |= skin.rootBone && skin.rootBone.asVf().IsChildOf(propRoot);
                        usesBonesFromProp |= skin.bones.Any(bone => bone && bone.asVf().IsChildOf(propRoot));
                    }
                }

                return usesBonesFromProp
                    ? ArmatureLink.ArmatureLinkMode.SkinRewrite
                    : ArmatureLink.ArmatureLinkMode.ReparentRoot;
            }

            return model.linkMode;
        }

        private string GetRootName(VFGameObject rootBone) {
            if (rootBone == null) return "Unknown";

            var isBone = false;
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                isBone |= skin.rootBone == rootBone;
                isBone |= skin.bones.Contains(rootBone.transform);
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

        private enum ChestUpHack {
            None,
            ClothesHaveChestUp,
            AvatarHasChestUp
        }

        private class Links {
            // These are stacks, because it's convenient, and we want to iterate over them in reverse order anyways
            // because when operating on the vrc clone, we delete game objects as we process them, and we want to
            // delete the children first.

            public VFGameObject propMain;
            public VFGameObject avatarMain;
            public ChestUpHack chestUpHack = ChestUpHack.None;
            
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

            if (!model.linkTo.Any()) {
                throw new Exception("'Link To' field is empty");
            }

            var exceptions = new List<string>();
            var avatarBone = model.linkTo.Select(to => {
                try {
                    VFGameObject obj;
                    if (to.useBone) {
                        obj = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, to.bone);
                    } else if (to.useObj) {
                        obj = to.obj;
                        if (obj == null) throw new Exception("'Link to' object does not exist");
                    } else {
                        obj = avatarObject;
                    }

                    if (!string.IsNullOrWhiteSpace(to.offset)) {
                        var path = obj.GetPath(avatarObject);
                        var finalPath = ClipRewriter.Join(path, to.offset);
                        obj = avatarObject.Find(finalPath);
                        if (obj == null) throw new Exception($"Failed to find object at path '{finalPath}'");
                    }
                    
                    // This is just here to ensure that the target is inside the avatar
                    obj.GetPath(avatarObject);

                    return obj;
                } catch (Exception e) {
                    exceptions.Add(e.Message);
                    return null;
                }
            }).NotNull().FirstOrDefault();

            if (avatarBone == null) {
                throw new Exception(string.Join("\n", exceptions));
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
                        if (childPropBone.name.Contains("ChestUp")) {
                            childAvatarBone = checkAvatarBone;
                            links.chestUpHack = ChestUpHack.ClothesHaveChestUp;
                        } else {
                            childAvatarBone = checkAvatarBone.Find("ChestUp/" + searchName);
                            if (childAvatarBone) links.chestUpHack = ChestUpHack.AvatarHasChestUp;
                        }
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
            return orig.Find(orig.name);
        }

        public override string GetEditorTitle() {
            return "Armature Link";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.Info(
                "This feature will attach a prop (with or without an armature) to the avatar." +
                " If 'Link From' is an armature matching the avatar's, the armatures will be merged and the extra bones will not count toward performance rank."));

            container.Add(VRCFuryEditorUtils.Prop(
                prop.FindPropertyRelative("propBone"),
                label: "Link From (Prop / Clothing)",
                tooltip: "For clothing, this should be the Hips bone in the clothing's Armature (or the 'main' bone if it doesn't have Hips).\n" +
                         "For non-clothing objects (things that you just want to re-parent), this should be the object you want moved."
            ).MarginBottom(10));

            container.Add(VRCFuryEditorUtils.WrappedLabel("Link To (Avatar):"));
            var linkToList = prop.FindPropertyRelative("linkTo");
            var linkToContainer = new VisualElement().MarginBottom(10);
            container.Add(linkToContainer);
            var simpleLinkToMode =
                linkToList.arraySize == 1
                && linkToList.GetArrayElementAtIndex(0).FindPropertyRelative("useBone").boolValue
                && !linkToList.GetArrayElementAtIndex(0).FindPropertyRelative("useObj").boolValue
                && string.IsNullOrWhiteSpace(linkToList.GetArrayElementAtIndex(0).FindPropertyRelative("offset").stringValue);
            VisualElement RenderLinkToList() {
                var output = new VisualElement();
                output.Add(VRCFuryEditorUtils.Info("If multiple targets are provided, the first valid target found on the avatar will be used."));
                var header = new VisualElement().Row();
                header.Add(VRCFuryEditorUtils.WrappedLabel("Target Object").FlexGrow(1));
                header.Add(VRCFuryEditorUtils.WrappedLabel("Offset Path").FlexGrow(1));
                output.Add(header);
                output.Add(new VisualElement().Row());
                void OnPlus() {
                    var menu = new GenericMenu();

                    void Reset(SerializedProperty newEntry) {
                        newEntry.FindPropertyRelative("useObj").boolValue = false;
                        newEntry.FindPropertyRelative("obj").objectReferenceValue = null;
                        newEntry.FindPropertyRelative("useBone").boolValue = false;
                        newEntry.FindPropertyRelative("bone").enumValueIndex = 0;
                        newEntry.FindPropertyRelative("offset").stringValue = "";
                    }
                    menu.AddItem(new GUIContent("Bone"), false, () => {
                        VRCFuryEditorUtils.AddToList(linkToList, entry => {
                            Reset(entry);
                            entry.FindPropertyRelative("useBone").boolValue = true;
                        });
                    });
                    menu.AddItem(new GUIContent("GameObject"), false, () => {
                        VRCFuryEditorUtils.AddToList(linkToList, entry => {
                            Reset(entry);
                            entry.FindPropertyRelative("useObj").boolValue = true;
                        });
                    });
                    menu.AddItem(new GUIContent("Avatar Root"), false, () => {
                        VRCFuryEditorUtils.AddToList(linkToList, entry => {
                            Reset(entry);
                        });
                    });
                    menu.ShowAsContext();
                }
                output.Add(VRCFuryEditorUtils.List(linkToList, onPlus: OnPlus));
                return output;
            }
            if (simpleLinkToMode) {
                linkToContainer.Add(VRCFuryEditorUtils.Prop(linkToList.GetArrayElementAtIndex(0).FindPropertyRelative("bone")));
            } else {
                linkToContainer.Add(RenderLinkToList());
            }

            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            };
            container.Add(adv);

            var matching = VRCFuryEditorUtils.Section("Search / Matching");
            adv.Add(matching);
            
            matching.Add(VRCFuryEditorUtils.Prop(
                prop.FindPropertyRelative("linkMode"),
                label: "Link Mode",
                tooltip: 
                "(Skin Rewrite) Attempt to merge children as well as root object\n" + 
                "(Reparent Root) The prop object is moved into the avatar's bone. No other merging takes place.\n" +
                "(Merge as Children) Deprecated. Same as Skin Rewrite.\n" +
                "(Bone Constraint) Deprecated. Same as Skin Rewrite.\n" +
                "(Auto) Selects Skin Rewrite if a mesh uses bones from the prop armature, or Reparent Root otherwise."
            ).MarginBottom(10));

            if (simpleLinkToMode) {
                var advancedLinkToButtonContainer = new VisualElement();
                matching.Add(advancedLinkToButtonContainer);
                advancedLinkToButtonContainer.Add(new Button(() => {
                    linkToContainer.Clear();
                    linkToContainer.Add(RenderLinkToList());
                    linkToContainer.Bind(prop.serializedObject);
                    advancedLinkToButtonContainer.Clear();
                }) { text = "Enable Advanced Link Target Mode"}.MarginBottom(5));
            }

            matching.Add(VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("removeBoneSuffix"),
                label: "Remove bone suffix/prefix",
                tooltip: "If set, this substring will be removed from all bone names in the prop. This is useful for props where the artist added " +
                         "something like _PropName to the end of every bone, breaking AvatarLink in the process. If empty, the suffix will be predicted " +
                         "based on the difference between the name of the given root bones."
            ));

            var alignment = VRCFuryEditorUtils.Section("Positioning and Alignment");
            adv.Add(alignment);

            alignment.Add(VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("keepBoneOffsets2"),
                label: "Keep bone offsets",
                tooltip:
                "If no, linked bones will be rigidly locked to the transform of the corresponding avatar bone.\n" +
                "If yes, prop bones will maintain their initial offset to the corresponding avatar bone. This is unusual.\n" +
                "If auto, offsets will be kept only if Reparent Root link mode is used."
            ));

            alignment.Add(VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("skinRewriteScalingFactor"),
                label: "Scaling factor override",
                tooltip: "If 0, scaling factor will automatically be detected using the difference in size between the root bones."
            ));

            alignment.Add(VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("scalingFactorPowersOf10Only"),
                label: "Restrict scaling factor to powers of 10"
            ));
            
            var chestUpWarning = VRCFuryEditorUtils.Warn(
                "These clothes are designed for an avatar with a different ChestUp configuration. You may" +
                " have downloaded the wrong version of the clothes for your avatar version, or the clothes may not be designed for your avatar." +
                " Contact the clothing creator, and see if they have a proper version of the clothing for your rig.\n\n" +
                "VRCFury will attempt to merge it anyways, but the chest area may not look correct.");
            chestUpWarning.SetVisible(false);
            container.Add(chestUpWarning);
            
            var hipsWarning = VRCFuryEditorUtils.Warn(
                "It appears this object is clothing with an Armature and Hips bone. If you are trying to link the clothing to your avatar," +
                " the Link From box should be the Hips object from this clothing, not this main object!");
            hipsWarning.SetVisible(false);
            container.Add(hipsWarning);

            container.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                hipsWarning.SetVisible(false);
                if (model.propBone != null) {
                    var hipsGuess = GuessLinkFrom(model.propBone);
                    if (hipsGuess != null && hipsGuess != model.propBone) {
                        hipsWarning.SetVisible(true);
                    }
                }
                
                chestUpWarning.SetVisible(false);
                if (avatarObject == null) {
                    return "Avatar descriptor is missing";
                }

                var linkMode = GetLinkMode();

                var links = GetLinks();
                if (links == null) {
                    return "No valid link target found";
                }

                if (links.chestUpHack != ChestUpHack.None) {
                    chestUpWarning.SetVisible(true);
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
        
        [CustomPropertyDrawer(typeof(ArmatureLink.LinkTo))]
        public class LinkToDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var output = new VisualElement().Row();
                VisualElement left;
                if (prop.FindPropertyRelative("useObj").boolValue) {
                    left = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
                } else if (prop.FindPropertyRelative("useBone").boolValue) {
                    left = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("bone"));
                } else {
                    left = VRCFuryEditorUtils.WrappedLabel("Avatar Root");
                }

                left.FlexBasis(0).FlexGrow(1);
                output.Add(left);
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("offset")).FlexBasis(0).FlexGrow(1));
                return output;
            }
        }

        [CanBeNull]
        public static VFGameObject GuessLinkFrom(VFGameObject componentObject) {
            // Try finding the hips following the same path they are on the avatar
            {
                var avatarObject = VRCAvatarUtils.GuessAvatarObject(componentObject);
                if (componentObject == avatarObject) return null;
                if (avatarObject != null) {
                    var avatarHips = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Hips);
                    if (avatarHips != null) {
                        var pathToAvatarHips = avatarHips.GetPath(avatarObject);
                        var foundHips = componentObject.Find(pathToAvatarHips);
                        if (foundHips != null) return foundHips;
                    }
                }
            }

            // Try finding the hips following normal naming conventions
            {
                var armatures = new List<VFGameObject>();
                if (componentObject.name.ToLower().Contains("armature") ||
                    componentObject.name.ToLower().Contains("skeleton")) {
                    armatures.Add(componentObject);
                }

                armatures.AddRange(componentObject
                    .Children()
                    .Where(child =>
                        child.name.ToLower().Contains("armature") || child.name.ToLower().Contains("skeleton")));

                var hips = armatures
                    .SelectMany(armature => armature.Children())
                    .FirstOrDefault(child => child.name.ToLower().Contains("hip"));
                if (hips != null) {
                    return hips;
                }
            }

            return componentObject;
        }
    }
}
