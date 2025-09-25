using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Menu;
#if VRC_NEW_PUBLIC_SDK
using VRC.SDK3A.Editor;
#endif
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal static class WhenBlueprintIdReadyHook {
        private static readonly List<Action> callbacks = new List<Action>();

#if VRC_NEW_PUBLIC_SDK
        [InitializeOnLoadMethod]
        private static void Init() {
            VRCSdkControlPanel.OnSdkPanelEnable += (_, _2) => {
                if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder)) {
                    builder.OnSdkBuildStart += (_3, _4) => callbacks.Clear();
                    builder.OnSdkUploadFinish += (_3, _4) => callbacks.Clear();
                    builder.OnSdkUploadSuccess += (_3, _4) => {
                        foreach (var c in callbacks) {
                            VRCFExceptionUtils.ErrorDialogBoundary(c);
                        }
                        callbacks.Clear();
                    };
                }
            };
        }
#endif

        public static void Add(Action c) {
            callbacks.Add(c);
        }
        
        private class VrcPreuploadHook : VrcfAvatarPreprocessor {
            protected override int order => int.MinValue;

            protected override void Process(VFGameObject _) {
                callbacks.Clear();
            }
        }
    }
}
