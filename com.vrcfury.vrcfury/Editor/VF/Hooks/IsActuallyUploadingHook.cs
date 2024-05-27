using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;

namespace VF.Hooks {
    public class IsActuallyUploadingHook : IVRCSDKPreprocessAvatarCallback {
        public int callbackOrder => int.MinValue;
        private static bool actuallyUploading = false;
        public bool OnPreprocessAvatar(GameObject obj) {
            EditorApplication.delayCall += () => actuallyUploading = false;
            actuallyUploading = DetermineIfActuallyUploading();
            return true;
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