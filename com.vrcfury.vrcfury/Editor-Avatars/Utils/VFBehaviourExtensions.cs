using System;
using UnityEngine;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Utils {
    /**
     * Registers avatar SDK behavior handling for controller loading, rewriting, and saving.
     */
    internal static class VFBehaviourExtensions {
        [VFInit]
        private static void Init() {
            VFBehaviour.rewriteParameters = RewriteParameters;
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
            VFBehaviour.getObjectPath = behaviour => (behaviour as VRCAnimatorPlayAudio)?.SourcePath;
            VFBehaviour.setObjectPath = (behaviour, path) => {
                if (behaviour is VRCAnimatorPlayAudio audio) audio.SourcePath = path;
            };
            VFBehaviour.getReferencedAssets = behaviour => (behaviour as VRCAnimatorPlayAudio)?.Clips;
#endif
        }

        private static void RewriteParameters(
            StateMachineBehaviour behaviour,
            Func<string, string> rewrite,
            bool includeWrites
        ) {
            if (behaviour is VRCAvatarParameterDriver driver && driver.parameters != null) {
                foreach (var parameter in driver.parameters) {
                    if (parameter == null) continue;
                    if (includeWrites && !string.IsNullOrEmpty(parameter.name)) {
                        parameter.name = rewrite(parameter.name);
                    }
#if VRCSDK_HAS_DRIVER_COPY
                    if (VFParameterRewriteSettings.ShouldRewriteCopyDriverSources
                        && parameter.type == VRC_AvatarParameterDriver.ChangeType.Copy
                        && !string.IsNullOrEmpty(parameter.source)) {
                        parameter.source = rewrite(parameter.source);
                    }
#endif
                }
            }
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
            if (behaviour is VRCAnimatorPlayAudio audio && !string.IsNullOrEmpty(audio.ParameterName)) {
                audio.ParameterName = rewrite(audio.ParameterName);
            }
#endif
#if VRCSDK_HAS_FACE_TRACKING
            if (behaviour is VRC_AnimatorFaceTrackingBlendshapesSet faceTracking && faceTracking.blendSets != null) {
                foreach (var set in faceTracking.blendSets) {
                    if (set?.type == VRC_AnimatorFaceTrackingBlendshapesSet.SourceType.Parameter
                        && !string.IsNullOrEmpty(set.sourceParameter)) {
                        set.sourceParameter = rewrite(set.sourceParameter);
                    }
                }
            }
#endif
        }
    }
}
