using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor.UI;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VF.Model.Feature {
    [Serializable]
    internal class ArmatureLink : NewFeatureModel {
        [Obsolete] public enum ArmatureLinkMode {
            SkinRewrite,
            MergeAsChildren,
            ParentConstraint,
            ReparentRoot,
            Auto,
        }

        [Obsolete] public enum KeepBoneOffsets {
            Auto,
            Yes,
            No
        }

        [Serializable]
        public class LinkTo {
            public bool useBone = true;
            public HumanBodyBones bone = HumanBodyBones.Hips;
            public bool useObj = false;
            public GameObject obj = null;
            public string offset = "";
        }

        public GameObject propBone;
        public List<LinkTo> linkTo = new List<LinkTo>() { new LinkTo() };
        public string removeBoneSuffix;
        public bool removeParentConstraints = true;
        public string forceMergedName = "";
        [NonSerialized] public Func<bool> onlyIf;
        public bool forceOneWorldScale = false;
        
        // Merging
        public bool recursive = false;

        // Alignment
        public bool alignPosition = false;
        public bool alignRotation = false;
        public bool alignScale = false;
        public bool autoScaleFactor = true;
        public bool scalingFactorPowersOf10Only = true;
        public float skinRewriteScalingFactor = 1;

        // legacy
        [Obsolete] public bool useOptimizedUpload;
        [Obsolete] public bool useBoneMerging;
        [Obsolete] public bool keepBoneOffsets;
        [Obsolete] public bool physbonesOnAvatarBones;
        [Obsolete] public HumanBodyBones boneOnAvatar;
        [Obsolete] public string bonePathOnAvatar;
        [Obsolete] public List<HumanBodyBones> fallbackBones = new List<HumanBodyBones>();
        [Obsolete] public KeepBoneOffsets keepBoneOffsets2 = KeepBoneOffsets.Auto;
        [Obsolete] public ArmatureLinkMode linkMode = ArmatureLinkMode.Auto;
        
        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (useBoneMerging) {
                    linkMode = ArmatureLinkMode.SkinRewrite;
                } else {
                    linkMode = ArmatureLinkMode.MergeAsChildren;
                }
            }
            if (fromVersion < 2) {
                skinRewriteScalingFactor = 1;
            }
            if (fromVersion < 3) {
                keepBoneOffsets2 = keepBoneOffsets ? KeepBoneOffsets.Yes : KeepBoneOffsets.No;
            }
            if (fromVersion < 4) {
                if (linkMode != ArmatureLinkMode.SkinRewrite) {
                    skinRewriteScalingFactor = 0;
                }
            }
            if (fromVersion < 5) {
                if (linkMode == ArmatureLinkMode.MergeAsChildren) {
                    scalingFactorPowersOf10Only = false;
                }
            }
            if (fromVersion < 6) {
                linkTo.Clear();
                if (string.IsNullOrWhiteSpace(bonePathOnAvatar)) {
                    linkTo.Add(new LinkTo { bone = boneOnAvatar });
                    foreach (var fallback in fallbackBones) {
                        linkTo.Add(new LinkTo { bone = fallback });
                    }
                } else {
                    linkTo.Add(new LinkTo { useBone = false, useObj = false, offset = bonePathOnAvatar });
                }
            }
            if (fromVersion < 7) {
                if (linkMode == ArmatureLinkMode.Auto) {
                    if (propBone != null && HasExternalSkinBoneReference(propBone.transform)) {
                        recursive = true;
                    } else {
                        recursive = false;
                    }
                } else if (linkMode == ArmatureLinkMode.ReparentRoot) {
                    recursive = false;
                } else {
                    recursive = true;
                }
                if (keepBoneOffsets2 == KeepBoneOffsets.Auto) {
                    alignPosition = alignRotation = alignScale = recursive;
                } else {
                    alignPosition = alignRotation = alignScale = (keepBoneOffsets2 == KeepBoneOffsets.No);
                }
                if (skinRewriteScalingFactor <= 0) {
                    skinRewriteScalingFactor = 1;
                    autoScaleFactor = recursive;
                } else {
                    autoScaleFactor = false;
                }
            }
            return false;
#pragma warning restore 0612
        }
        public override int GetLatestVersion() {
            return 7;
        }

        public static bool HasExternalSkinBoneReference(Transform obj) {
            if (obj == null) return false;
            var avatarDescriptor = obj.GetComponentInParent<VRCAvatarDescriptor>();
            var avatarObj = avatarDescriptor != null ? avatarDescriptor.transform : obj.root; 
            foreach (var skin in avatarObj.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (skin == null) continue;
                if (skin.transform.IsChildOf(obj)) continue;
                if (skin.rootBone != null && skin.rootBone.IsChildOf(obj)) return true;
                if (skin.bones.Any(bone => bone != null && bone.IsChildOf(obj))) return true;
            }
            return false;
        }

        // Provide export function 
        public List<(GameObject, HumanBodyBones, string)> GetLinkTargets() {
            var outList = new List<(GameObject, HumanBodyBones, string)>();
            foreach (LinkTo link in linkTo) {
                outList.Add((link.obj, link.bone, link.offset));
            }

            return outList;
        }
    }
}
