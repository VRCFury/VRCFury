using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {

    internal class ArmatureLinkBuilder : FeatureBuilder<ArmatureLink> {

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
            
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("removeParentConstraints"), "Remove parent constraints from merged objects"));
            
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

                var linkMode = ArmatureLinkService.GetLinkMode(model, avatarObject);
                var keepBoneOffsets = ArmatureLinkService.GetKeepBoneOffsets(model, linkMode);
                var links = ArmatureLinkService.GetLinks(model, linkMode, avatarObject);
                if (links == null) {
                    return "No valid link target found";
                }

                if (links.chestUpHack != ArmatureLinkService.ChestUpHack.None) {
                    chestUpWarning.SetVisible(true);
                }

                var text = new List<string>();
                var (avatarMainScale, propMainScale, scalingFactor) = ArmatureLinkService.GetScalingFactor(model, links, linkMode);
                text.Add($"Merging to bone: {links.avatarMain.GetPath(avatarObject)}");
                text.Add($"Link Mode: {linkMode}");
                text.Add($"Keep Bone Offsets: {keepBoneOffsets}");
                if (!keepBoneOffsets) {
                    text.Add($"Prop root bone scale: {propMainScale}");
                    text.Add($"Avatar root bone scale: {avatarMainScale}");
                    text.Add($"Scaling factor: {scalingFactor}");
                }
                if (links.unmergedChildren.Count > 0) {
                    text.Add(
                        "These bones do not have a match on the avatar and will be added as new children: \n" +
                        string.Join("\n",
                            links.unmergedChildren.Select(b =>
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
