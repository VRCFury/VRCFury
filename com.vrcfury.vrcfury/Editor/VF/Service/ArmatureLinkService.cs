using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;
using Random = System.Random;

namespace VF.Service {
    [VFService]
    internal class ArmatureLinkService {
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly FindAnimatedTransformsService findAnimatedTransformsService;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ControllersService controllers;
        
        [FeatureBuilderAction(FeatureOrder.ArmatureLink)]
        public void Apply() {
            var builders = globals.allBuildersInRun.OfType<ArmatureLinkBuilder>().ToList();

            var anim = findAnimatedTransformsService.Find();
            var avatarHumanoidBones = VRCFArmatureUtils.GetAllBones(avatarObject).Values.ToImmutableHashSet();

            var doNotReparent = new HashSet<VFGameObject>();
            // We still reparent scale-animated things, because some users take advantage of this to "scale to 0" every bone
            doNotReparent.UnionWith(anim.positionIsAnimated.Children());
            doNotReparent.UnionWith(anim.rotationIsAnimated.Children());
            doNotReparent.UnionWith(anim.physboneRoot.Children()); // Physbone roots are the same as rotation being animated
            doNotReparent.UnionWith(anim.physboneChild); // Physbone children can't be reparented, because they must remain as children of the physbone root

            var pruneCheck = new HashSet<VFGameObject>();
            var saveDebugInfo = !IsActuallyUploadingHook.Get();
            
            var animLink = new VFMultimapList<VFGameObject, VFGameObject>();

            foreach (var builder in builders) {
                try {
                    ApplyOne(
                        builder.model,
                        avatarObject,
                        saveDebugInfo,
                        avatarHumanoidBones,
                        anim,
                        doNotReparent,
                        mover,
                        pruneCheck,
                        animLink
                    );
                } catch (Exception e) {
                    var path = builder.featureBaseObject.GetPath(avatarObject);
                    throw new ExceptionWithCause($"Failed to build ArmatureLink from {path}", e);
                }
            }

            mover.ApplyDeferred();
            
            // Clean up objects that don't need to exist anymore
            // (this should happen before toggle rewrites, so we don't have to add toggles for a ton of things that won't exist anymore)
            var usedReasons = GetUsageReasons(avatarObject);
            var attachDebugInfoTo = new HashSet<VFGameObject>();
            foreach (var obj in pruneCheck) {
                if (obj == null) continue;
                attachDebugInfoTo.UnionWith(obj.GetSelfAndAllChildren());
                if (!usedReasons.ContainsKey(obj)) obj.Destroy();
            }

            // Rewrite animations that turn off parents
            foreach (var clip in controllers.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                foreach (var binding in clip.GetFloatBindings()) {
                    if (binding.type != typeof(GameObject)) continue;
                    var transform = avatarObject.Find(binding.path);
                    if (transform == null) continue;
                    foreach (var other in animLink.Get(transform)) {
                        if (other == null) continue; // it got deleted because the propBone wasn't used
                        var b = binding;
                        b.path = other.GetPath(avatarObject);
                        clip.SetCurve(b, clip.GetFloatCurve(binding));
                    }
                }
            }

            if (saveDebugInfo) {
                foreach (var obj in attachDebugInfoTo) {
                    if (obj == null) continue;
                    if (usedReasons.ContainsKey(obj)) {
                        var debugInfo = obj.AddComponent<VRCFuryDebugInfo>();
                        debugInfo.debugInfo =
                            "VRCFury Armature Link did not clean up this object because it is still used:\n";
                        debugInfo.debugInfo += usedReasons.Get(obj).OrderBy(a => a).Join('\n');
                    }
                }
            }
        }

        private static void ApplyOne(
            ArmatureLink model,
            VFGameObject avatarObject,
            bool saveDebugInfo,
            ISet<VFGameObject> avatarHumanoidBones,
            FindAnimatedTransformsService.AnimatedTransforms anim,
            ISet<VFGameObject> doNotReparent,
            ObjectMoveService mover,
            ISet<VFGameObject> pruneCheck,
            VFMultimapList<VFGameObject, VFGameObject> animLink
        ) {
            if (model.onlyIf != null && !model.onlyIf.Invoke()) {
                return;
            }
            
            if (model.propBone == null) {
                Debug.LogWarning("Root bone is null on armature link.");
                return;
            }
            
            Debug.Log("Armature Linking " + model.propBone.asVf().GetPath(avatarObject));

            var linkMode = GetLinkMode(model, avatarObject);
            var links = GetLinks(model, linkMode, avatarObject);
            if (links == null) {
                return;
            }

            var keepBoneOffsets = GetKeepBoneOffsets(model, linkMode);

            var (_, _, scalingFactor) = GetScalingFactor(model, links, linkMode);

            var rootName = GetRootName(links.propMain, avatarObject);

            var didNotReparent = new HashSet<VFGameObject>();

            // Move over all the old components / children from the old location to a new child
            foreach (var (propBone, avatarBone) in links.mergeBones) {
                VRCFuryDebugInfo debugInfo = null;
                if (saveDebugInfo) {
                    debugInfo = propBone.AddComponent<VRCFuryDebugInfo>();
                }
                void AddDebugInfo(string text) {
                    if (debugInfo != null) debugInfo.debugInfo += text + "\n\n";
                }
                AddDebugInfo($"" +
                             $"VRCFury Armature Link Debug Info\n" +
                             $"Aramature link root: {links.propMain.GetPath(avatarObject, true)} -> {links.avatarMain.GetPath(avatarObject, true)}\n" +
                             $"This object: {propBone.GetPath(avatarObject, true)} -> {avatarBone.GetPath(avatarObject, true)}");

                var animSources = anim.GetDebugSources(propBone);
                if (animSources.Count > 0) {
                    AddDebugInfo("This object is animated:\n" + animSources.OrderBy(a => a).Join('\n'));
                }

                bool ShouldReparent() {
                    if (propBone == links.propMain) {
                        AddDebugInfo("This object was forced to link because it is the root of the armature link");
                        return true;
                    }
                    if (avatarHumanoidBones.Contains(avatarBone)) {
                        AddDebugInfo("This object was forced to link because it is a humanoid bone on the avatar");
                        return true;
                    }
                    if (doNotReparent.Contains(propBone)) {
                        AddDebugInfo("This object was not linked because a parent object was animated or part of a physbone (check the parent's debug info)");
                        didNotReparent.Add(propBone);
                        return false;
                    }
                    if (propBone.GetSelfAndAllParents().Any(parent => didNotReparent.Contains(parent))) {
                        AddDebugInfo("This object was not linked because a parent object was animated or part of a physbone (check the parent's debug info)");
                        return false;
                    }
                    return true;
                }
                bool ShouldReuseBone() {
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
                if (model.removeParentConstraints) {
                    foreach (var c in propBone.GetConstraints().Where(c => c.IsParent())) {
                        c.Destroy();
                        AddDebugInfo(
                            "An existing parent constraint component was removed, because it was probably a leftover from before Armature Link");
                    }
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
                        if (anim.activated.Contains(parent) || !parent.active || animLink.ContainsValue(parent)) {
                            animatedParents.Add(parent);
                        }
                    }
                }

                // Move it on over

                VFGameObject addedObject;
                if (!string.IsNullOrWhiteSpace(model.forceMergedName) && linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot) {
                    // Special logic for force naming
                    var exists = avatarBone.Find(model.forceMergedName);
                    if (exists != null) {
                        throw new Exception(
                            $"Aramture link was asked to move an object to a destination with the forced name" +
                            $" '{exists.GetPath(avatarObject)}', but that object already exists at the destination.");
                    }
                    mover.Move(propBone, avatarBone, model.forceMergedName, defer: true);
                    addedObject = propBone;
                    AddDebugInfo($"Forcefully named {model.forceMergedName} by Armature Link Force Naming." +
                                 $" Note that this may break toggles or offset animations for this object!");
                } else {
                    var newName = $"[VF{new Random().Next(100,999)}] {propBone.name}";
                    if (propBone.name != rootName) newName += $" from {rootName}";
                    addedObject = GameObjects.Create(newName, avatarBone, useTransformFrom: propBone);
                    var current = addedObject;

                    foreach (var parent in animatedParents) {
                        // If this animated parent come from a toggle created during another armature link,
                        // We have to follow the link back to find the original toggle source
                        var original = animLink
                            .Where(pair => pair.Value == parent)
                            .Select(pair => pair.Key)
                            .DefaultIfEmpty(parent)
                            .First();
                        current = GameObjects.Create($"Toggle From {original.name}", current);
                        current.active = original.active;
                        animLink.Put(original, current);
                        AddDebugInfo($"A toggle wrapper object was added to maintain the animated toggle of {original.name}");
                    }

                    var transformAnimated =
                        anim.positionIsAnimated.Contains(propBone)
                        || anim.rotationIsAnimated.Contains(propBone)
                        || anim.scaleIsAnimated.Contains(propBone)
                        || propBone.GetConstraints().Any();
                    if (transformAnimated) {
                        current = GameObjects.Create("Original Parent (Retained for transform animation)", current, propBone.parent);

                        // In a weird edge case, sometimes people mark all their clothing bones with an initial scale of 0,
                        // to mark them as initially "hidden". In this case, we need to make sure that the transform maintainer
                        // doesn't just permanently set the scale to 0.
                        if (current.localScale.x == 0 || current.localScale.y == 0 || current.localScale.z == 0) {
                            current.localScale = Vector3.one;
                        }
                        AddDebugInfo($"Detected that this object's transform is animated, so a wrapper object was added to keep its original parent transform");
                    }

                    mover.Move(propBone, current, "Original Object", defer: true);
                }
                
                if (!keepBoneOffsets) {
                    addedObject.worldPosition = avatarBone.worldPosition;
                    addedObject.worldRotation = avatarBone.worldRotation;
                    addedObject.worldScale = avatarBone.worldScale * scalingFactor;
                    AddDebugInfo($"Keep offsets is set to NO, so this object was snapped to its parent's transform");
                }
                if (model.forceOneWorldScale) {
                    addedObject.worldScale = Vector3.one;
                }

                if (ShouldReuseBone()) {
                    RewriteSkins(propBone, avatarBone, avatarObject);
                }

                pruneCheck.Add(addedObject);
            }
        }

        private static void RewriteSkins(VFGameObject fromBone, VFGameObject toBone, VFGameObject avatarObject) {
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                // Update skins to use bones and bind poses from the original avatar
                if (skin.bones.Contains((Transform)fromBone)) {
                    var mesh = skin.GetMutableMesh("Needed to change bone bind-poses for Armature Link to re-use bones on base armature");
                    if (mesh != null) {
                        mesh.bindposes = skin.bones.Zip(mesh.bindposes, (a,b) => (a,b))
                            .Select(boneAndBindPose => {
                                var bone = boneAndBindPose.a.asVf();
                                var bindPose = boneAndBindPose.b;
                                if (bone != fromBone) return bindPose;
                                return toBone.worldToLocalMatrix * bone.localToWorldMatrix * bindPose;
                            }) 
                            .ToArray();
                    }

                    skin.bones = skin.bones
                        .Select(b => b == fromBone ? (Transform)toBone : b)
                        .ToArray();
                    VRCFuryEditorUtils.MarkDirty(skin);
                }

                // We never rewrite rootBone because of two reasons:
                // 1. It defines the origin of the bounds (which may rotate and be impossible to reproduce)
                // 2. It defines the origin for verts that are not weight painted (unusual, but really hard
                //    to fix because there is no "bind pose" for the root bone)
            }
        }

        public static (float, float, float) GetScalingFactor(ArmatureLink model, Links links, ArmatureLink.ArmatureLinkMode linkMode) {
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

        public static VFMultimapSet<VFGameObject,string> GetUsageReasons(VFGameObject avatarObject) {
            var reasons = new VFMultimapSet<VFGameObject,string>();

            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                var countExistanceAsUsage = true;
                var scanInternals = true;

                if (component is Transform) {
                    countExistanceAsUsage = false;
                    scanInternals = false;
                }
                if (component is VRCFuryDebugInfo) {
                    countExistanceAsUsage = false;
                    scanInternals = false;
                }
                if (component is IConstraint) {
                    countExistanceAsUsage = false;
                }

                if (countExistanceAsUsage) {
                    reasons.Put(component.owner(), $"Contains {component.GetType().Name} component");
                }

                if (scanInternals) {
                    var so = new SerializedObject(component);
                    var prop = so.GetIterator();
                    do {
                        if (prop.propertyPath.StartsWith("ignoreTransforms.Array")) {
                            // TODO: If we remove objects that are in these physbone ignoreTransforms arrays, we should
                            // probably also remove them from the array instead of just leaving it null
                            continue;
                        }
                        if (prop.propertyType == SerializedPropertyType.ObjectReference) {
                            VFGameObject target = null;
                            if (prop.objectReferenceValue is Transform t) target = t;
                            else if (prop.objectReferenceValue is GameObject g) target = g;
                            if (target != null && target.IsChildOf(avatarObject)) {
                                reasons.Put(target, prop.propertyPath + " in " + component.GetType().Name + " on " + component.owner().GetPath(avatarObject, true));
                            }
                        }
                    } while (prop.Next(true));
                }
            }

            foreach (var used in reasons.GetKeys().ToArray()) {
                foreach (var parent in used.GetSelfAndAllParents()) {
                    if (parent != used && parent.IsChildOf(avatarObject)) {
                        reasons.Put(parent, "A child object is used");
                    }
                }
            }

            return reasons;
        }

        public static ArmatureLink.ArmatureLinkMode GetLinkMode(ArmatureLink model, VFGameObject avatarObject) {
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

        private static string GetRootName(VFGameObject rootBone, VFGameObject avatarObject) {
            if (rootBone == null) return "Unknown";

            var isBone = false;
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                isBone |= skin.rootBone == rootBone;
                isBone |= skin.bones.Contains((Transform)rootBone);
            }
            isBone |= rootBone.name.ToLower().Trim() == "armature";

            if (isBone) return GetRootName(rootBone.parent, avatarObject);

            return rootBone.name;
        }

        public static bool GetKeepBoneOffsets(ArmatureLink model, ArmatureLink.ArmatureLinkMode linkMode) {
            if (model.keepBoneOffsets2 == ArmatureLink.KeepBoneOffsets.Auto) {
                return linkMode == ArmatureLink.ArmatureLinkMode.ReparentRoot;
            }
            return model.keepBoneOffsets2 == ArmatureLink.KeepBoneOffsets.Yes;
        }

        public enum ExtraBoneHack {
            None,
            ClothesHaveIt,
            AvatarHasIt,
            AvatarHasFake
        }

        public class Links {
            // These are stacks, because it's convenient, and we want to iterate over them in reverse order anyways
            // because when operating on the vrc clone, we delete game objects as we process them, and we want to
            // delete the children first.

            public VFGameObject propMain;
            public VFGameObject avatarMain;
            public ExtraBoneHack chestUpHack = ExtraBoneHack.None;
            public ExtraBoneHack topFut = ExtraBoneHack.None;
            
            // left=bone in prop | right=bone in avatar
            public readonly Stack<(VFGameObject, VFGameObject)> mergeBones
                = new Stack<(VFGameObject, VFGameObject)>();
            
            // left=object to move | right=new parent
            public readonly Stack<(VFGameObject, VFGameObject)> unmergedChildren
                = new Stack<(VFGameObject, VFGameObject)>();
        }

        public static VFGameObject GetProbableParent(ArmatureLink model, VFGameObject avatarObject, VFGameObject obj) {
            try {
                var linkFrom = model.propBone;
                if (linkFrom == null || !obj.IsChildOf(linkFrom)) return null;
                var links = GetLinks(model, GetLinkMode(model, avatarObject), avatarObject);
                return links.mergeBones
                    .Where(pair => pair.Item1 == obj)
                    .Select(pair => pair.Item2)
                    .FirstOrDefault();
            } catch (Exception) {
                return null;
            }
        }

        public static Links GetLinks(ArmatureLink model, ArmatureLink.ArmatureLinkMode linkMode, VFGameObject avatarObject) {
            VFGameObject propBone = model.propBone;
            if (propBone == null) return null;

            foreach (var b in VRCFArmatureUtils.GetAllBones(avatarObject).Values) {
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
                throw new Exception(exceptions.Join('\n'));
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
            links.mergeBones.Push((propBone, avatarBone));

            if (linkMode != ArmatureLink.ArmatureLinkMode.ReparentRoot) {
                var checkStack = new Stack<(VFGameObject, VFGameObject)>();
                checkStack.Push((propBone, avatarBone));
                while (checkStack.Count > 0) {
                    var (checkPropBone, checkAvatarBone) = checkStack.Pop();
                    foreach (var childPropBone in checkPropBone.Children()) {
                        var searchName = childPropBone.name;
                        if (!string.IsNullOrWhiteSpace(removeBoneSuffix)) {
                            searchName = searchName.Replace(removeBoneSuffix, "");
                        }
                        var childAvatarBone = checkAvatarBone.Find(searchName);

                        // Hack for Rexouium model, which added ChestUp bone at some point and broke a ton of old props
                        var recurseButDoNotLink = false;
                        if (childAvatarBone == null) {
                            // Clothes have ChestUp, but avatar does not?
                            if (childPropBone.name.Contains("ChestUp")) {
                                childAvatarBone = checkAvatarBone;
                                links.chestUpHack = ExtraBoneHack.ClothesHaveIt;
                                recurseButDoNotLink = true;
                            }
                            // Clothes have TopFut, but avatar does not?
                            if (childPropBone.name == "TopFut_L" || childPropBone.name == "TopFut_R") {
                                childAvatarBone = checkAvatarBone;
                                links.topFut = ExtraBoneHack.ClothesHaveIt;
                                recurseButDoNotLink = true;
                            }
                        }
                        if (childAvatarBone == null) {
                            childAvatarBone = checkAvatarBone.Find("ChestUp/" + searchName);
                            if (childAvatarBone != null) links.chestUpHack = ExtraBoneHack.AvatarHasIt;
                        }
                        if (childAvatarBone == null) {
                            childAvatarBone = checkAvatarBone.Find("TopFut_L/" + searchName);
                            if (childAvatarBone != null) links.topFut = ExtraBoneHack.AvatarHasIt;
                        }
                        if (childAvatarBone == null) {
                            childAvatarBone = checkAvatarBone.Find("TopFut_R/" + searchName);
                            if (childAvatarBone != null) links.topFut = ExtraBoneHack.AvatarHasIt;
                        }
                        if (childAvatarBone == null) {
                            // Clothes have real ChestUp, but avatar has ChestUp that is fake and empty?
                            // (happens on some versions of rex)
                            if (checkAvatarBone.name.Contains("ChestUp")) {
                                childAvatarBone = checkAvatarBone.parent.Find(searchName);
                                if (childAvatarBone != null) links.chestUpHack = ExtraBoneHack.AvatarHasFake;
                            }
                        }

                        if (childAvatarBone != null) {
                            var marshmallowChild = GetMarshmallowChild(childAvatarBone);
                            if (marshmallowChild != null) childAvatarBone = marshmallowChild;
                        }

                        if (childAvatarBone != null) {
                            if (!recurseButDoNotLink) {
                                links.mergeBones.Push((childPropBone, childAvatarBone));
                            }
                            checkStack.Push((childPropBone, childAvatarBone));
                        } else {
                            links.unmergedChildren.Push((childPropBone, checkAvatarBone));
                        }
                    }
                }
            }

            links.propMain = propBone;
            links.avatarMain = avatarBone;

            return links;
        }

        /**
         * Marshmallow PB unity package inserts fake bones in the armature, breaking our link.
         * Detect if this happens, and return the proper child bone instead.
         */
        private static VFGameObject GetMarshmallowChild(VFGameObject orig) {
            var scaleConstraint = orig.GetConstraints().FirstOrDefault(c => c.IsScale());
            if (scaleConstraint == null) return null;
            var sources = scaleConstraint.GetSources();
            if (sources.Length != 1) return null;
            var source = sources[0];
            if (source == null) return null;
            var scaleTargetInMarshmallow = source
                .GetSelfAndAllParents()
                .Any(t => t.name.ToLower().Contains("marshmallow"));
            if (!scaleTargetInMarshmallow) return null;
            return orig.Find(orig.name);
        }
    }
}
