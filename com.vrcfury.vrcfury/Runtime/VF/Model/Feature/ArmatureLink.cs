using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class ArmatureLink : NewFeatureModel {
        public enum ArmatureLinkMode {
            SkinRewrite,
            MergeAsChildren,
            ParentConstraint,
            ReparentRoot,
            Auto,
        }

        public enum KeepBoneOffsets {
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

        public ArmatureLinkMode linkMode = ArmatureLinkMode.Auto;
        public GameObject propBone;
        public List<LinkTo> linkTo = new List<LinkTo>() { new LinkTo() };
        public KeepBoneOffsets keepBoneOffsets2 = KeepBoneOffsets.Auto;
        public string removeBoneSuffix;
        public float skinRewriteScalingFactor = 0;
        public bool scalingFactorPowersOf10Only = true;
        public bool removeParentConstraints = true;
        public string forceMergedName = "";
        [NonSerialized] public Func<bool> onlyIf;
        
        // legacy
        [Obsolete] public bool useOptimizedUpload;
        [Obsolete] public bool useBoneMerging;
        [Obsolete] public bool keepBoneOffsets;
        [Obsolete] public bool physbonesOnAvatarBones;
        [Obsolete] public HumanBodyBones boneOnAvatar;
        [Obsolete] public string bonePathOnAvatar;
        [Obsolete] public List<HumanBodyBones> fallbackBones = new List<HumanBodyBones>();
        
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
            return false;
#pragma warning restore 0612
        }
        public override int GetLatestVersion() {
            return 6;
        }
    }
}