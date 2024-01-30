using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * This hook detects if the vrc pre-processing hooks have failed, and cleans up the
     * gameobject if that happens. We can't do this in our own play mode handler, because it's possible
     * that our IVRCSDKPreprocessAvatarCallback may be called by someone else before us, like Av3Emu.
     */
    public static class PreProcessingFailureCheckHook {
        private static VFGameObject uploadingObject = null;
        
        private static void CheckForFailure() {
            // If the uploading object still exists at this point, it means the preprocess hooks failed
            // somewhere between calling our start hook and our end hook.
            if (!Application.isPlaying) return;
            if (uploadingObject == null) return;
            var failMarker = new GameObject($"{uploadingObject.name} (Preprocess hooks failed)");
            SceneManager.MoveGameObjectToScene(failMarker, uploadingObject.scene);
            uploadingObject.Destroy();
        }
        
        internal class FailureCheckStart : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => int.MinValue;
            public bool OnPreprocessAvatar(GameObject obj) {
                uploadingObject = obj;
                EditorApplication.delayCall += CheckForFailure;
                return true;
            }
        }

        internal class FailureCheckEnd : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => int.MaxValue;
            public bool OnPreprocessAvatar(GameObject obj) {
                uploadingObject = null;
                return true;
            }
        }
    }
}
