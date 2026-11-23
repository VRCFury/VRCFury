using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Exceptions;
using Object = UnityEngine.Object;

namespace VF.Utils {
    /**
     * Records a validated Hips correction for the armature cache and the build.
     */
    internal sealed class HumanoidHipsCorrection {
        // Unity's humanoid skeleton starts at the parent of the mapped Hips.
        private const int OldHips = 1;
        private const int NewHips = 2;

        public readonly VFGameObject hips;
        private readonly Avatar source;
        private readonly int mappingIndex;
        private readonly string mappedHipsName;
        private readonly string hipsName;

        private HumanoidHipsCorrection(Avatar source, VFGameObject hips, int mappingIndex, string mappedHipsName) {
            this.source = source;
            this.hips = hips;
            this.mappingIndex = mappingIndex;
            this.mappedHipsName = mappedHipsName;
            hipsName = hips.name;
        }

        public string FailureMessage =>
            $"Your avatar's Hip bone is incorrectly set to '{mappedHipsName}' in its rig definition instead of '{hipsName}',"
            + " and VRCFury failed to patch it automatically.";

        public static HumanoidHipsCorrection Find(
            SerializedObject avatar,
            VFGameObject root,
            VFGameObject mappedHips,
            VFGameObject spine,
            IReadOnlyDictionary<long, string> paths
        ) {
            var hips = spine.parent;
            if (hips == null || hips.parent != mappedHips) return null;
            var source = (Avatar)avatar.targetObject;
            if (!source.isValid || !source.isHuman) return null;

            var humanBones = source.humanDescription.human;
            if (humanBones == null) return null;
            var hipsHumanName = HumanTrait.BoneName[(int)HumanBodyBones.Hips];
            var index = Array.FindIndex(humanBones, bone => bone.humanName == hipsHumanName);
            var name = hips.name;
            if (index < 0 || humanBones[index].boneName != mappedHips.name
                || humanBones.Any(bone => bone.boneName == name)) return null;

            var rig = new Rig(avatar);
            if (rig.mapping == null || index >= rig.mapping.arraySize || rig.bones == null
                || rig.bones.arraySize <= (int)HumanBodyBones.Spine
                || rig.leftHand == null || rig.rightHand == null || rig.ids == null || rig.nodes == null || rig.poses == null
                || rig.toFull == null || rig.toHuman == null || rig.ids.arraySize <= NewHips
                || rig.ids.arraySize != rig.nodes.arraySize || rig.ids.arraySize != rig.poses.arraySize
                || rig.ids.arraySize != rig.toFull.arraySize) return null;
            if (rig.bones.GetArrayElementAtIndex((int)HumanBodyBones.Hips).intValue != OldHips
                || !paths.TryGetValue(rig.ids.GetArrayElementAtIndex(NewHips).longValue, out var path) || path != hips.GetPath(root)
                || paths.Values.Count(p => p == name || p.EndsWith("/" + name, StringComparison.Ordinal)) != 1)
                return null;

            var spineIndex = rig.bones.GetArrayElementAtIndex((int)HumanBodyBones.Spine).intValue;
            if (spineIndex <= NewHips || spineIndex >= rig.nodes.arraySize || rig.Parent(spineIndex).intValue != NewHips)
                return null;

            // Check the saved topology even if a mapped bone is absent from the scene.
            for (var i = 0; i < rig.nodes.arraySize; i++) {
                var parent = rig.Parent(i).intValue;
                if (i <= NewHips ? parent != i - 1 : parent < NewHips || parent >= i) return null;
            }
            foreach (var table in new[] { rig.bones, rig.leftHand, rig.rightHand }) {
                for (var i = table == rig.bones ? 1 : 0; i < table.arraySize; i++) {
                    var bone = table.GetArrayElementAtIndex(i).intValue;
                    if (bone >= 0 && (bone <= NewHips || bone >= rig.nodes.arraySize)) return null;
                }
            }
            if (rig.Axes(OldHips).intValue < 0 || rig.Axes(NewHips).intValue != -1) return null;

            return new HumanoidHipsCorrection(source, hips, index, mappedHips.name);
        }

        public Avatar CreateAvatar(Avatar current) {
            if (current != source || hips == null || hips.name != hipsName) {
                throw new VRCFBuilderException("The humanoid rig changed after VRCFury detected an incorrect Hips mapping.");
            }
            var repaired = Object.Instantiate(source);
            var success = false;
            try {
                using var avatar = new SerializedObject(repaired);
                var rig = new Rig(avatar);
                rig.mapping.GetArrayElementAtIndex(mappingIndex).FindPropertyRelative("m_BoneName").stringValue = hipsName;
                rig.bones.GetArrayElementAtIndex((int)HumanBodyBones.Hips).intValue = NewHips;

                // Reading a humanoid pose uses the joint-axis assignment on Hips.
                rig.Axes(NewHips).intValue = rig.Axes(OldHips).intValue;
                rig.Axes(OldHips).intValue = -1;

                // Drop the old root so the new Hips lands in slot 1. Keep the local pose values
                // and axis data of the surviving nodes.
                for (var i = 1; i < rig.nodes.arraySize; i++) rig.Parent(i).intValue--;
                foreach (var table in new[] { rig.bones, rig.leftHand, rig.rightHand, rig.toHuman }) {
                    for (var i = 0; i < table.arraySize; i++) {
                        var index = table.GetArrayElementAtIndex(i);
                        if (index.intValue >= 0) index.intValue--;
                    }
                }
                foreach (var array in new[] { rig.ids, rig.nodes, rig.poses, rig.toFull }) array.DeleteArrayElementAtIndex(0);

                if (!avatar.ApplyModifiedPropertiesWithoutUndo() || !repaired.isValid || !repaired.isHuman) {
                    throw new VRCFBuilderException(
                        $"Unity rejected the Avatar after VRCFury remapped Hips to '{hipsName}'."
                        + " Correct the Hips mapping in the model's import settings."
                    );
                }
                repaired.name = source.name + " (Corrected Hips)";
                success = true;
                return repaired;
            } finally {
                if (!success) Object.DestroyImmediate(repaired);
            }
        }

        private sealed class Rig {
            public readonly SerializedProperty mapping;
            public readonly SerializedProperty bones;
            public readonly SerializedProperty leftHand;
            public readonly SerializedProperty rightHand;
            public readonly SerializedProperty ids;
            public readonly SerializedProperty nodes;
            public readonly SerializedProperty poses;
            public readonly SerializedProperty toFull;
            public readonly SerializedProperty toHuman;

            public Rig(SerializedObject avatar) {
                var rig = avatar.FindProperty("m_Avatar");
                var human = rig?.FindPropertyRelative("m_Human.data");
                var skeleton = human?.FindPropertyRelative("m_Skeleton.data");
                mapping = avatar.FindProperty("m_HumanDescription.m_Human");
                bones = human?.FindPropertyRelative("m_HumanBoneIndex");
                leftHand = human?.FindPropertyRelative("m_LeftHand.data.m_HandBoneIndex");
                rightHand = human?.FindPropertyRelative("m_RightHand.data.m_HandBoneIndex");
                ids = skeleton?.FindPropertyRelative("m_ID");
                nodes = skeleton?.FindPropertyRelative("m_Node");
                poses = human?.FindPropertyRelative("m_SkeletonPose.data.m_X");
                toFull = rig?.FindPropertyRelative("m_HumanSkeletonIndexArray");
                toHuman = rig?.FindPropertyRelative("m_HumanSkeletonReverseIndexArray");
            }

            public SerializedProperty Parent(int index) => nodes.GetArrayElementAtIndex(index).FindPropertyRelative("m_ParentId");
            public SerializedProperty Axes(int index) => nodes.GetArrayElementAtIndex(index).FindPropertyRelative("m_AxesId");
        }
    }
}
