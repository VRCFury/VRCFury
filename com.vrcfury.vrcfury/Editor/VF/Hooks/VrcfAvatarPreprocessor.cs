using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Model;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal abstract class VrcfAvatarPreprocessor : IVRCSDKPreprocessAvatarCallback {
        // We don't run anything on MinValue because that's when StartCheck runs
        public int callbackOrder => order == int.MinValue ? int.MinValue + 1 : order;
        public bool OnPreprocessAvatar(GameObject obj) {
            // This is only here just in case the RunPreprocessorsOnlyOncePatch harmony patch didn't work (user running on a platform that doesn't support harmony)
            var go = (VFGameObject)obj;
            if (!RunPreprocessorsOnlyOncePatch.ShouldRunPreprocessors(go)) {
                Debug.LogWarning("Skipping " + GetType().FullName + " preprocessor because preprocessors already ran on this object");
                return true;
            }
            return VRCFExceptionUtils.ErrorDialogBoundary(() => Process(obj));
        }

        protected abstract int order { get; }
        protected abstract void Process(VFGameObject obj);
    }
}
