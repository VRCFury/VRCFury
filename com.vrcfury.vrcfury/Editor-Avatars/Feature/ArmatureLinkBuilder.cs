using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Hooks;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("Armature Link")]
    internal class ArmatureLinkBuilder : FeatureBuilder<ArmatureLink> {

        internal static VisualElement LinkTargetsEditor(SerializedProperty list, bool showOffsetPath = false) {
            var options = new ObjectReferenceDrawer.Options {
                showOffsetPath = showOffsetPath || Enumerable.Range(0, list.arraySize)
                    .Any(i => !string.IsNullOrEmpty(list.GetArrayElementAtIndex(i).FindPropertyRelative("offset").stringValue))
            };
            VisualElement RenderTarget(SerializedProperty entry, int index) {
                var target = new VisualElement();
                if (index > 0) target.Add(VRCFuryEditorUtils.WrappedLabel($"Fallback #{index}"));
                target.Add(ObjectReferenceDrawer.CreateEditor(entry, options));
                return target;
            }
            return VRCFuryEditorUtils.RefreshOnChange(() => list.arraySize == 1
                ? RenderTarget(list.GetArrayElementAtIndex(0), 0)
                : VRCFuryEditorUtils.List(list,
                    onPlus: () => ObjectReferenceDrawer.AddEntry(list, options),
                    renderItem: RenderTarget), list);
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, ArmatureLink model, VFGameObject avatarObject) {
            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.Info(
                "This feature will attach a prop (with or without an armature) to the avatar." +
                " If 'Link From' is an armature matching the avatar's, the armatures will be merged and the extra bones will not count toward performance rank."));
            
            Action UpdateVisibleSettings = null;
            
            var propBoneProp = prop.FindPropertyRelative("propBone");
            var lastPropBone = propBoneProp.objectReferenceValue as GameObject;
            container.Add(VRCFuryEditorUtils.Prop(
                propBoneProp,
                label: "Link From (Prop / Clothing)",
                tooltip: "For clothing, this should be the Hips bone in the clothing's Armature (or the 'main' bone if it doesn't have Hips).\n" +
                         "For non-clothing objects (things that you just want to re-parent), this should be the object you want moved."
            ).MarginBottom(10));
            container.Add(VRCFuryEditorUtils.OnChange(propBoneProp, () => {
                var newValue = propBoneProp.objectReferenceValue as GameObject;
                if (lastPropBone != newValue) {
                    UpdateOnLinkFromChange(model, lastPropBone, newValue);
                    prop.serializedObject.Update();
                    lastPropBone = newValue;
                }
            }));

            container.Add(VRCFuryEditorUtils.WrappedLabel("Link To:"));
            var linkToList = prop.FindPropertyRelative("linkTo");
            var showOffsetPath = model.linkTo.Any(entry => !string.IsNullOrEmpty(entry.offset));
            container.Add(VRCFuryEditorUtils.RefreshOnTrigger(() => LinkTargetsEditor(linkToList, showOffsetPath),
                prop.serializedObject, out var refreshTargets).MarginBottom(10));

            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            }.AddTo(container);

            var matching = VRCFuryEditorUtils.Section("Search / Matching").AddTo(adv);
            var fallbackOption = new VisualElement().AddTo(matching);
            fallbackOption.Add(new Button(() => ObjectReferenceDrawer.AddEntry(linkToList, new ObjectReferenceDrawer.Options())) {
                text = "Add Fallback Link To"
            });
            fallbackOption.Add(VRCFuryEditorUtils.Info("Adds another target to try if earlier targets aren't found. Bone targets already automatically fall back to similar bones if the exact bone isn't found."));
            var offsetOption = new VisualElement().AddTo(matching);
            var addOffsetPath = new Button { text = "Add Offset Path" };
            offsetOption.Add(addOffsetPath);
            offsetOption.SetVisible(!showOffsetPath);
            addOffsetPath.clicked += () => {
                showOffsetPath = true;
                refreshTargets();
                offsetOption.SetVisible(false);
            };
            offsetOption.Add(VRCFuryEditorUtils.Info("Shows an Offset Path field for each target, letting you select a child relative to the chosen bone or object. Most setups don't need an offset."));

            var recursiveProp = prop.FindPropertyRelative("recursive");
            matching.Add(VRCFuryEditorUtils.Prop(
                recursiveProp,
                label: "Recursive",
                tooltip: "If enabled, child objects with matching object names on the avatar will also be linked",
                onChange: () => UpdateVisibleSettings()
            ).MarginBottom(10));

            matching.Add(VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("removeBoneSuffix"),
                label: "Ignore name suffix/prefix",
                tooltip: "If set, this substring will be ignored when matching object names against the avatar. This is useful for props where the artist added " +
                         "something like _PropName to the end of every bone. If empty, the suffix will be predicted " +
                         "based on the difference between the name of the given root bones."
            ));

            var alignment = VRCFuryEditorUtils.Section("Transform Alignment", "Snap merged objects to the existing transform on the avatar").AddTo(adv);

            alignment.Add(VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("alignPosition"),
                label: "Align Position"
            ));
            alignment.Add(VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("alignRotation"),
                label: "Align Rotation"
            ));

            var lockScaleOptions = new VisualElement();
            var lockScaleProp = prop.FindPropertyRelative("alignScale");
            alignment.Add(VRCFuryEditorUtils.Prop(
                lockScaleProp,
                label: "Align Scale",
                onChange: () => UpdateVisibleSettings()
            ).PaddingBottom(5));
            
            var autoScaleFactorProp = prop.FindPropertyRelative("autoScaleFactor");

            var powersOfTen = VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("scalingFactorPowersOf10Only"),
                label: "Restrict multiplier to powers of 10"
            );

            var multiplier = VRCFuryEditorUtils.BetterProp(
                prop.FindPropertyRelative("skinRewriteScalingFactor"),
                label: "Multiplier"
            );
            
            var autoMultiplier = VRCFuryEditorUtils.Prop(
                autoScaleFactorProp,
                label: "Automatic Scale Multiplier",
                tooltip: "" +
                         "When checked, the scale multiplier will be set to the Link From object's world scale divided by the Link To object's world scale." +
                         " When combined with 'Restrict to powers of 10', this will automatically correct for clothes which have been incorrectly exported with the wrong scale units.",
                onChange: () => UpdateVisibleSettings()
            );
            
            lockScaleOptions.Add(autoMultiplier.PaddingBottom(5));
            lockScaleOptions.Add(powersOfTen);
            lockScaleOptions.Add(multiplier);
            lockScaleOptions
                .PaddingLeft(10)
                .AddTo(alignment);
            
            var superFoldout = new Foldout {
                text = "Super Advanced Options",
                value = false
            }.AddTo(adv);
            var super = VRCFuryEditorUtils.Section("Super Advanced Options", "Danger, changing may break things").AddTo(superFoldout);
            
            super.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("removeParentConstraints"), "Remove parent constraints from merged objects", forceLabelOnOwnLine: true));
            
            super.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("forceMergedName"),
                "Force Merged Name",
                tooltip: "This box allows you to force the name of the object at the merged target location." +
                         " This is useful if you want to forcefully merge a replacement MMD Body onto the base avatar." +
                         " (Paired with Advanced Link Target Mode pointing to the avatar root)." +
                         " BEWARE: If you use this, offset animations and toggles for the merged object WILL NOT WORK."));
            
            super.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("forceOneWorldScale"),
                "Force world scale to 1,1,1",
                tooltip: "After linking, forces the world scale of the root object to 1,1,1."));

            var hackWarningWrapper = new VisualElement();
            VRCFuryEditorUtils.WrappedLabel(
                "These clothes are designed for a different version of your avatar's rig. You may" +
                " have downloaded the wrong version of the clothes for your avatar version, or the clothes may not be designed for your avatar." +
                " Contact the clothing creator, and see if they have a proper version of the clothing for your rig.\n\n" +
                "VRCFury will attempt to merge it anyways, but the affected areas may have slight clipping issues.\n"
            ).AddTo(hackWarningWrapper);
            var hackWarningList = VRCFuryEditorUtils.WrappedLabel("").AddTo(hackWarningWrapper);
            var hackWarning = VRCFuryEditorUtils.Warn(hackWarningWrapper);
            hackWarning.SetVisible(false);
            container.Add(hackWarning);

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
                
                hackWarning.SetVisible(false);
                if (avatarObject == null) {
                    return "Avatar descriptor is missing";
                }

                var links = ArmatureLinkService.GetLinks(
                    model,
                    avatarObject,
                    VRCFObjectPathCache.GetPerFrame(avatarObject),
                    VRCFArmatureCache.GetPerFrame(avatarObject)
                );
                if (links == null) {
                    return "No valid link target found";
                }

                if (links.hacksUsed.Any()) {
                    hackWarningList.text = links.hacksUsed.OrderBy(a => a).Join("\n");
                    hackWarning.SetVisible(true);
                }

                var text = new List<string>();
                text.Add($"Merging to bone: {links.avatarMain.GetPath(avatarObject)}");
                if (model.alignScale) {
                    var (avatarMainScale, propMainScale, scalingFactor) = ArmatureLinkService.GetScalingFactor(model, links);
                    text.Add($"Prop root bone scale: {propMainScale}");
                    text.Add($"Avatar root bone scale: {avatarMainScale}");
                    text.Add($"Scaling factor: {scalingFactor}");
                }
                if (links.unmergedChildren.Count > 0) {
                    text.Add(
                        "These bones do not have a match on the avatar and will be added as new children: \n" +
                        links.unmergedChildren.Select(b =>
                            "* " + b.Item1.GetPath(links.propMain)
                        ).Join('\n'));
                }

                return text.Join('\n');
            }));

            UpdateVisibleSettings = () => {
                lockScaleOptions.SetVisible(lockScaleProp.boolValue);
                if (recursiveProp.boolValue) {
                    autoMultiplier.SetVisible(true);
                    powersOfTen.SetVisible(autoScaleFactorProp.boolValue);
                    multiplier.SetVisible(!autoScaleFactorProp.boolValue);
                } else {
                    autoMultiplier.SetVisible(false);
                    powersOfTen.SetVisible(false);
                    multiplier.SetVisible(true);
                }
            };
            UpdateVisibleSettings();

            return container;
        }
        

        [CanBeNull]
        public static VFGameObject GuessLinkFrom(VFGameObject componentObject) {
            // Try finding the hips following the same path they are on the avatar
            {
                var avatarObject = componentObject.GetAvatarRoot();
                if (componentObject == avatarObject) return null;
                var avatarHips = VRCFArmatureCache.GetPerFrame(avatarObject).FindBoneOnArmatureOrNull(HumanBodyBones.Hips);
                if (avatarHips != null) {
                    var pathToAvatarHips = avatarHips.GetPath(avatarObject);
                    var foundHips = componentObject.Find(pathToAvatarHips);
                    if (foundHips != null) return foundHips;
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

        public static void UpdateOnLinkFromChange(ArmatureLink model, [CanBeNull] VFGameObject before, [CanBeNull] VFGameObject after) {
            if (after == null) return;
            var skinAfter = ArmatureLink.HasExternalSkinBoneReference(after);
            if (before == null || ArmatureLink.HasExternalSkinBoneReference(before) != skinAfter) {
                model.alignPosition = model.alignRotation = model.alignScale = skinAfter;
                model.recursive = skinAfter;
                model.autoScaleFactor = true;
                model.scalingFactorPowersOf10Only = true;
                model.skinRewriteScalingFactor = 1;
            }
        }
    }
}
