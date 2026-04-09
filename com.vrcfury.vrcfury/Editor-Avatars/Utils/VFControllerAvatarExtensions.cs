using System;
using System.Linq;
using UnityEditor;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Utils {
    internal static class VFControllerAvatarExtensions {

        private static bool _rewriteCopyDriverSources = true;
        /**
         * This should be used if you're rewriting something to an AAP, since copy drivers can't read from the AAP
         */
        public static void doNotRewriteCopyDriverSources(Action with) {
            _rewriteCopyDriverSources = false;
            try {
                with();
            } finally {
                _rewriteCopyDriverSources = true;
            }
        }

        [InitializeOnLoadMethod]
		private static void Init() {
            VFController.onRewriteParameters = (affectsLayers, includeWrites, rewriteParam) => {
                foreach (var b in affectsLayers.SelectMany(layer => layer.allBehaviours)) {
                    // VRCAvatarParameterDriver
                    if (b is VRCAvatarParameterDriver oldB) {
                        foreach (var p in oldB.parameters) {
                            if (includeWrites) {
                                p.name = rewriteParam(p.name);
                            }
#if VRCSDK_HAS_DRIVER_COPY
                            if (p.type == VRC_AvatarParameterDriver.ChangeType.Copy && _rewriteCopyDriverSources) {
                                p.source = rewriteParam(p.source);
                            }
#endif
                        }

                        b.Dirty();
                    }

                    // VRCAnimatorPlayAudio
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
                    if (b is VRCAnimatorPlayAudio audio) {
                        audio.ParameterName = rewriteParam(audio.ParameterName);
                    }
#endif
                }
            };

            VFController.onRewriteClips = (controller, rewriter) => {
                // Rewrite VRCAnimatorPlayAudio
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
                foreach (var audio in controller.layers.SelectMany(l => l.allBehaviours).OfType<VRCAnimatorPlayAudio>()) {
                    audio.SourcePath = rewriter.RewritePath(audio.SourcePath);
                }
#endif
            };
        }
    }
}
