using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Menu;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal static class WhenBlueprintIdReadyHook {
        private static readonly List<Action> callbacks = new List<Action>();

#if VRC_NEW_PUBLIC_SDK
        [InitializeOnLoadMethod]
        private static void Init() {
            VRCSdkControlPanel.OnSdkPanelEnable += (_, _) => {
                if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder)) {
                    builder.OnSdkBuildStart += (_, _) => callbacks.Clear();
                    builder.OnSdkUploadFinish += (_, _) => callbacks.Clear();
                    builder.OnSdkUploadSuccess += (_, _) => {
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
        
        private class VrcPreuploadHook : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => int.MinValue;

            public bool OnPreprocessAvatar(GameObject _vrcCloneObject) {
                callbacks.Clear();
                return true;
            }
        }
    }
}
