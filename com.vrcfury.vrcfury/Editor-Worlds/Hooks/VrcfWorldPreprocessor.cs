using UnityEngine.SceneManagement;
using VF.Builder.Exceptions;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal abstract class VrcfWorldPreprocessor : IVRCSDKBuildRequestedCallback {
        public int callbackOrder => order == int.MinValue ? int.MinValue + 1 : order;
        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType) {
            if (requestedBuildType != VRCSDKRequestedBuildType.Scene) return true;
            var scene = SceneManager.GetActiveScene();
            return VRCFExceptionUtils.ErrorDialogBoundary(() => Process(scene));
        }

        protected abstract int order { get; }
        protected abstract void Process(Scene scene);
    }
}
