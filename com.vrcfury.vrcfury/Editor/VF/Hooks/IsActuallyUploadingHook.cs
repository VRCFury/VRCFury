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
            var preprocessFrame = stack
                .Select((frame, i) => (frame, i))
                .Where(f => f.frame.GetMethod().Name == "OnPreprocessAvatar" &&
                            (f.frame.GetMethod().DeclaringType?.FullName ?? "").StartsWith("VRC."))
                .Select(pair => pair.i)
                .DefaultIfEmpty(-1)
                .Last();
            if (preprocessFrame < 0) return false; // Not called through preprocessor hook
            if (preprocessFrame >= stack.Length - 1) return true;

            var callingClass = stack[preprocessFrame + 1].GetMethod().DeclaringType?.FullName;
            if (callingClass == null) return true;
            Debug.Log("Build was invoked by " + callingClass);
            return callingClass.StartsWith("VRC.");
        }

        public static bool Get() {
            return actuallyUploading;
        }
    }
}