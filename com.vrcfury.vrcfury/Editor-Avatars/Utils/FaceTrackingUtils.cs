#if VRCSDK_HAS_FACE_TRACKING
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using VF.Exceptions;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using FtSet = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingBlendshapesSet;
using FtBehavior = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingParameterBehavior;
using FtBehaviourControl = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.BlendSet;

namespace VF.Utils {
    internal static class FaceTrackingUtils {
        public const int FaceTrackingCount = (int)VRCAvatarDescriptor.FaceTrackingKey.Max;
        public const int VisemeCount = (int)VRC_AvatarDescriptor.Viseme.Count;
        // SDK editor limits are private/internal or inline literals in the referenced drawers.
        public const int MaxFaceTrackingMeshes = 16; // FaceTrackingMappingValidator.MaxFaceTrackingMeshes
        public const int MaxParameterBehaviorInputs = 32; // FaceTrackingMappingValidator.MaxParameterBehaviorInputs
        public const int MaxBlendshapeSets = 128; // AvatarDescriptorEditor3.MaxBlendshapesSets
        public const int MaxParameterBehaviors = 128; // FaceTrackingDrawerUtility.MaxParameterBehaviors
        public const int MaxSetControls = 64; // VRCAnimatorFaceTrackingBlendshapesSetEditor.MaxBlendSetsPerBehavior
        public const int MaxSetNameLength = 100; // FaceTrackingBlendshapesSetDrawer
        private static readonly HashSet<string> VisemeInputs = Enumerable.Range(0, VisemeCount)
            .Select(i => VRCAvatarDescriptor.GetFaceTrackingVisemeInputName((VRC_AvatarDescriptor.Viseme)i)).ToHashSet();
        private static readonly HashSet<string> HardwareInputs = Enumerable.Range(0, FaceTrackingCount)
            .Where(i => !VRCAvatarDescriptor.IsBasicFaceTrackingKey(i))
            .Select(i => ((VRCAvatarDescriptor.FaceTrackingKey)i).ToString()).ToHashSet();

        private static IEnumerable<string> Inputs(FtBehavior settings) =>
            settings.combineMode == VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom
                ? Regex.Split(settings.customExpression ?? "", @"\W+")
                : (settings.inputTrackingKeyNames ?? Array.Empty<string>()).Take(MaxParameterBehaviorInputs);

        public static bool UsesVisemeInputs(FtBehavior settings) => Inputs(settings).Any(VisemeInputs.Contains);
        public static bool UsesHardwareInputs(FtBehavior settings) => Inputs(settings).Any(HardwareInputs.Contains);

        public static (SkinnedMeshRenderer mesh, string[] names) GetLegacyVisemes(VRCAvatarDescriptor avatar) {
            if (avatar.lipSync != VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape || avatar.VisemeSkinnedMesh == null)
                return (null, Array.Empty<string>());
            var names = (avatar.VisemeBlendShapes ?? Enumerable.Empty<string>())
                .Select(name => name ?? "")
                .Concat(Enumerable.Repeat("", VisemeCount)).Take(VisemeCount).ToArray();
            return (avatar.VisemeSkinnedMesh, names);
        }

        public static (SkinnedMeshRenderer mesh, string[] names) GetLegacyEyelids(VRCAvatarDescriptor avatar) {
            var settings = avatar.customEyeLookSettings;
            var mesh = avatar.enableEyeLook && settings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                ? settings.eyelidsSkinnedMesh : null;
            var names = Enumerable.Range(0, 3).Select(state => {
                if (mesh == null
                    || settings.eyelidsBlendshapes == null || state >= settings.eyelidsBlendshapes.Length
                    || (state != 0 && settings.eyeGazeMode != VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation))
                    return "";
                return mesh.GetBlendshapeName(settings.eyelidsBlendshapes[state]) ?? "";
            }).ToArray();
            return (mesh, names);
        }

        public static void EnsureMesh(IList<SkinnedMeshRenderer> meshes, SkinnedMeshRenderer mesh) {
            if (mesh == null) return;
            var seen = new HashSet<SkinnedMeshRenderer>();
            for (var i = 0; i < meshes.Count; i++) {
                if (meshes[i] == null || !seen.Add(meshes[i])) meshes.RemoveAt(i--);
            }
            if (meshes.Count > MaxFaceTrackingMeshes)
                throw new VRCFBuilderException($"Face Tracking supports at most {MaxFaceTrackingMeshes} meshes.");
            if (meshes.Contains(mesh)) return;
            if (meshes.Count == MaxFaceTrackingMeshes)
                throw new VRCFBuilderException($"Face Tracking supports at most {MaxFaceTrackingMeshes} meshes.");
            meshes.Add(mesh);
        }

        // Shared by import renaming and final one-to-many expansion. Never mutate the input controls.
        public static List<FtBehaviourControl> RewriteSetReferences(List<FtBehaviourControl> controls, Func<string, IEnumerable<string>> rewrite) {
            if (controls == null) return null;
            var output = new List<FtBehaviourControl>();
            foreach (var control in controls) {
                if (control == null) {
                    output.Add(null);
                    continue;
                }
                foreach (var name in rewrite(control.blendshapesSetName ?? "") ?? new[] { control.blendshapesSetName }) {
                    var copy = UnitySerializationUtils.CloneSerializable(control);
                    copy.blendshapesSetName = name;
                    output.Add(copy);
                }
            }
            if (output.Count > MaxSetControls)
                throw new VRCFBuilderException($"Face Tracking supports at most {MaxSetControls} set controls per animator behavior.");
            return output;
        }


        public static FtSet CreateBlendshapeSet(string name) => new FtSet {
            setName = name,
            setWeight = 1,
            faceTrackingBlendshapeNames = Enumerable.Repeat("", FaceTrackingCount).ToArray(),
            faceTrackingBlendshapeMultipliers = Enumerable.Repeat(1f, FaceTrackingCount).ToArray(),
            visemeBlendshapeNames = Enumerable.Repeat("", VisemeCount).ToArray(),
            visemeBlendshapeMultipliers = Enumerable.Repeat(1f, VisemeCount).ToArray()
        };
    }
}
#endif
