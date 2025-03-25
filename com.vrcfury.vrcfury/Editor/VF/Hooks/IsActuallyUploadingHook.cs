using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;

namespace VF.Hooks {
    internal class IsActuallyUploadingHook : VrcfAvatarPreprocessor {
        protected override int order => int.MinValue;
        private static bool actuallyUploading = false;
        protected override void Process(VFGameObject obj) {
            EditorApplication.delayCall += () => actuallyUploading = false;
            actuallyUploading = DetermineIfActuallyUploading();
        }

        private static bool DetermineIfActuallyUploading() {
            if (Application.isPlaying) return false;
            var stack = new StackTrace().GetFrames();
            if (stack == null) return true;
            var actuallyUploading = stack.Any(frame => (frame.GetMethod().DeclaringType?.FullName ?? "").Contains("VRC.SDK3.Builder.VRCAvatarBuilder"));
            return actuallyUploading;
        }

        public static bool Get() {
            return actuallyUploading;
        }
    }
}