using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
#if VRCSDK_HAS_FACE_TRACKING
using VF.Builder;
using VRC.SDKBase;
#endif

namespace VF.Service {
    [VFService]
    internal class RemoveLegacyFaceTrackingService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        private readonly Lazy<Dictionary<Renderer, (bool visemes, bool eyelids)>> handled;

        public RemoveLegacyFaceTrackingService() {
            handled = new Lazy<Dictionary<Renderer, (bool visemes, bool eyelids)>>(() => {
                var result = new Dictionary<Renderer, (bool visemes, bool eyelids)>();
#if VRCSDK_HAS_FACE_TRACKING
                // Inspect all components, including controllers whose FT merge has not run yet.
                foreach (var full in globals.allFeaturesInRun.OfType<FullController>()) {
                    var sets = (full.faceTrackingBlendshapeSets ?? new()).Where(set => set != null).ToArray();
                    var behaviors = (full.faceTrackingParameterBehaviors ?? new()).Where(behavior => behavior != null).ToArray();
                    foreach (var entry in full.faceTrackingMeshes ?? new()) {
                        var renderer = entry.Resolve(avatar.gameObject, objectPaths, armatureCache);
                        if (renderer == null) continue;
                        var previous = result.GetOrDefault(renderer);
                        result[renderer] = (previous.visemes || sets.Any(FaceTrackingUtils.UsesVisemeInputs)
                                || behaviors.Any(FaceTrackingUtils.UsesVisemeInputs),
                            previous.eyelids || sets.Any(FaceTrackingUtils.UsesEyelidInputs)
                                || behaviors.Any(FaceTrackingUtils.UsesEyelidInputs));
                    }
                }
#endif
                return result;
            });
        }

        public (bool visemes, bool eyelids) GetHandled(Renderer renderer) =>
            renderer != null ? handled.Value.GetOrDefault(renderer) : default;
#if VRCSDK_HAS_FACE_TRACKING
        [VFAutowired] private readonly VRCFObjectPathCache objectPaths;
        [VFAutowired] private readonly VRCFArmatureCache armatureCache;
#endif

        [FeatureBuilderAction(FeatureOrder.RemoveLegacyFaceTracking)]
        public void Apply() {
            if (globals.allFeaturesInRun.OfType<RemoveBlinking>().Any())
                avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;
#if VRCSDK_HAS_FACE_TRACKING
            var eyes = avatar.customEyeLookSettings;
            if (avatar.enableEyeLook && eyes.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && GetHandled(eyes.eyelidsSkinnedMesh).eyelids)
                avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;
            if (GetHandled(avatar.VisemeSkinnedMesh).visemes
                && (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                    || avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape))
                avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly;
#endif
        }
    }
}
