#if VRCSDK_HAS_FACE_TRACKING
using System.Collections.Generic;
using System.Linq;
using VF.Exceptions;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Utils {
    internal static class FaceTrackingUtils {
        private const int FaceTrackingCount = (int)VRCAvatarDescriptor.FaceTrackingKey.Max;
        private const int VisemeCount = (int)VRC_AvatarDescriptor.Viseme.Count;
        private const int MaxFaceTrackingMeshes = 16;

        public static void EnsureEnabled(VRCAvatarDescriptor avatar) {
            if (avatar.enableFaceTracking) return;

            avatar.enableFaceTracking = true;
            avatar.useAllTrackingKeys = false;
            avatar.faceTrackingMeshes = new List<UnityEngine.SkinnedMeshRenderer>();
            avatar.faceTrackingBlendshapesSetList = new List<VRCAvatarDescriptor.FaceTrackingBlendshapesSet>();
            avatar.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior>();
        }

        public static void EnsureMesh(VRCAvatarDescriptor avatar, UnityEngine.SkinnedMeshRenderer mesh) {
            if (mesh == null) return;
            if (avatar.faceTrackingMeshes == null)
                avatar.faceTrackingMeshes = new List<UnityEngine.SkinnedMeshRenderer>();
            if (avatar.faceTrackingMeshes.Count > MaxFaceTrackingMeshes)
                throw new VRCFBuilderException($"Face Tracking supports at most {MaxFaceTrackingMeshes} meshes.");
            if (avatar.faceTrackingMeshes.Contains(mesh)) return;
            if (avatar.faceTrackingMeshes.Count == MaxFaceTrackingMeshes)
                throw new VRCFBuilderException($"Face Tracking supports at most {MaxFaceTrackingMeshes} meshes.");
            avatar.faceTrackingMeshes.Add(mesh);
        }

        public static void EnsureVisemesEnabled(VRCAvatarDescriptor avatar) {
            EnsureEnabled(avatar);

            var shouldImportLegacyVisemes = avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
            if (avatar.lipSync != VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking) {
                foreach (var set in GetBlendshapeSets(avatar).Where(set => set != null))
                    ClearMappings(set, true, 0, VisemeCount);
                avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking;
            }

            var visemeMesh = avatar.VisemeSkinnedMesh;
            if (shouldImportLegacyVisemes && visemeMesh != null) {
                EnsureMesh(avatar, visemeMesh);
                var visemeNames = (avatar.VisemeBlendShapes ?? Enumerable.Empty<string>())
                    .Select(name => name ?? "")
                    .Concat(Enumerable.Repeat("", VisemeCount))
                    .Take(VisemeCount)
                    .ToArray();
                var set = AddBlendshapeSet(avatar, "_vrcf_upgraded_visemes");
                for (var i = 0; i < VisemeCount; i++)
                    SetMapping(set, i, true, visemeNames[i]);
            }

            avatar.Dirty();
        }

        public static void EnsureEyelidsEnabled(VRCAvatarDescriptor avatar) {
            EnsureEnabled(avatar);

            var wasEyeLookEnabled = avatar.enableEyeLook;
            var shouldImportLegacyEyelids = wasEyeLookEnabled
                && avatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes;
            var shouldImportLegacyEyelidGaze = shouldImportLegacyEyelids
                && avatar.customEyeLookSettings.eyeGazeMode == VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation;
            if (!wasEyeLookEnabled) {
                avatar.customEyeLookSettings.eyeGazeMode = VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation;
                avatar.customEyeLookSettings.leftEye = null;
                avatar.customEyeLookSettings.rightEye = null;
                avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;
            } else if (shouldImportLegacyEyelidGaze) {
                var firstGazeKey = (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookOutRight;
                var gazeKeyCount = (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownLeft - firstGazeKey + 1;
                foreach (var set in GetBlendshapeSets(avatar).Where(set => set != null))
                    ClearMappings(set, false, firstGazeKey, gazeKeyCount);
                avatar.customEyeLookSettings.eyeGazeMode = VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.Both;
            }
            avatar.enableEyeLook = true;
            if (avatar.customEyeLookSettings.eyelidType != VRCAvatarDescriptor.EyelidType.FaceTracking) {
                foreach (var set in GetBlendshapeSets(avatar).Where(set => set != null)) {
                    ClearMappings(set, false, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedRight, 1);
                    ClearMappings(set, false, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, 1);
                }
                avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.FaceTracking;
            }

            var eyelidMesh = avatar.customEyeLookSettings.eyelidsSkinnedMesh;
            var eyelidBlendshapes = avatar.customEyeLookSettings.eyelidsBlendshapes;
            string GetLegacyEyelidBlendshapeName(int state) {
                if (!shouldImportLegacyEyelids || eyelidMesh == null || eyelidBlendshapes == null || eyelidBlendshapes.Length <= state)
                    return null;
                return eyelidMesh.GetBlendshapeName(eyelidBlendshapes[state]);
            }

            var blinkName = GetLegacyEyelidBlendshapeName(0);
            var lookingUpName = shouldImportLegacyEyelidGaze ? GetLegacyEyelidBlendshapeName(1) : null;
            var lookingDownName = shouldImportLegacyEyelidGaze ? GetLegacyEyelidBlendshapeName(2) : null;
            if (blinkName != null || lookingUpName != null || lookingDownName != null) {
                EnsureMesh(avatar, eyelidMesh);
                var set = AddBlendshapeSet(avatar, "_vrcf_upgraded_eyelids");
                if (blinkName != null) {
                    SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedRight, false, blinkName);
                    SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, false, blinkName);
                }
                if (lookingUpName != null) {
                    SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpRight, false, lookingUpName);
                    SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft, false, lookingUpName);
                }
                if (lookingDownName != null) {
                    SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownRight, false, lookingDownName);
                    SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownLeft, false, lookingDownName);
                }
            }
            avatar.Dirty();
        }

        private static void SetMapping(
            VRCAvatarDescriptor.FaceTrackingBlendshapesSet set,
            int index,
            bool visemes,
            string blendshapeName = "",
            float multiplier = 1f
        ) {
            EnsureMappingArrays(set, visemes);
            if (visemes) {
                set.visemeBlendshapeNames[index] = blendshapeName ?? "";
                set.visemeBlendshapeMultipliers[index] = multiplier;
            } else {
                set.faceTrackingBlendshapeNames[index] = blendshapeName ?? "";
                set.faceTrackingBlendshapeMultipliers[index] = multiplier;
            }
        }

        private static void EnsureMappingArrays(
            VRCAvatarDescriptor.FaceTrackingBlendshapesSet set,
            bool visemes
        ) {
            var count = visemes ? VisemeCount : FaceTrackingCount;
            if (visemes) {
                set.visemeBlendshapeNames = EnsureNames(set.visemeBlendshapeNames, count);
                set.visemeBlendshapeMultipliers = EnsureMultipliers(set.visemeBlendshapeMultipliers, count);
            } else {
                set.faceTrackingBlendshapeNames = EnsureNames(set.faceTrackingBlendshapeNames, count);
                set.faceTrackingBlendshapeMultipliers = EnsureMultipliers(set.faceTrackingBlendshapeMultipliers, count);
            }
        }

        private static string[] EnsureNames(string[] names, int count) {
            if (names?.Length == count && names.All(name => name != null)) return names;
            return (names ?? Enumerable.Empty<string>())
                .Select(name => name ?? "")
                .Concat(Enumerable.Repeat("", count))
                .Take(count)
                .ToArray();
        }

        private static float[] EnsureMultipliers(float[] multipliers, int count) {
            if (multipliers?.Length == count) return multipliers;
            return (multipliers ?? Enumerable.Empty<float>())
                .Concat(Enumerable.Repeat(1f, count))
                .Take(count)
                .ToArray();
        }

        public static void SetVisemeMapping(
            VRCAvatarDescriptor.FaceTrackingBlendshapesSet set,
            int viseme,
            string blendshapeName,
            float multiplier = 1f
        ) {
            SetMapping(set, viseme, true, blendshapeName, multiplier);
        }

        public static void SetFaceTrackingMapping(
            VRCAvatarDescriptor.FaceTrackingBlendshapesSet set,
            int key,
            string blendshapeName,
            float multiplier = 1f
        ) {
            SetMapping(set, key, false, blendshapeName, multiplier);
        }

        private static void ClearMappings(
            VRCAvatarDescriptor.FaceTrackingBlendshapesSet set,
            bool visemes,
            int first,
            int count
        ) {
            for (var i = first; i < first + count; i++)
                SetMapping(set, i, visemes);
        }

        private static List<VRCAvatarDescriptor.FaceTrackingBlendshapesSet> GetBlendshapeSets(VRCAvatarDescriptor avatar) {
            if (avatar.faceTrackingBlendshapesSetList == null)
                avatar.faceTrackingBlendshapesSetList = new List<VRCAvatarDescriptor.FaceTrackingBlendshapesSet>();
            return avatar.faceTrackingBlendshapesSetList;
        }

        public static VRCAvatarDescriptor.FaceTrackingBlendshapesSet AddBlendshapeSet(
            VRCAvatarDescriptor avatar,
            string setName = ""
        ) {
            var existingNames = GetBlendshapeSets(avatar)
                .Where(existingSet => existingSet != null)
                .Select(existingSet => existingSet.setName ?? "")
                .ToHashSet();
            setName = setName ?? "";
            var originalName = setName;
            for (var suffix = 1; existingNames.Contains(setName); suffix++)
                setName = originalName + " " + suffix;

            var set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet {
                setName = setName,
                setWeight = 1,
                faceTrackingBlendshapeNames = Enumerable.Repeat("", FaceTrackingCount).ToArray(),
                faceTrackingBlendshapeMultipliers = Enumerable.Repeat(1f, FaceTrackingCount).ToArray(),
                visemeBlendshapeNames = Enumerable.Repeat("", VisemeCount).ToArray(),
                visemeBlendshapeMultipliers = Enumerable.Repeat(1f, VisemeCount).ToArray()
            };
            GetBlendshapeSets(avatar).Add(set);
            avatar.Dirty();
            return set;
        }
    }
}
#endif
