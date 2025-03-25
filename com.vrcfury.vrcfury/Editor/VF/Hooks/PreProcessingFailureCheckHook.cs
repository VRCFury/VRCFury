using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * This hook detects if the vrc pre-processing hooks have failed, and cleans up the
     * gameobject if that happens. We can't do this in our own play mode handler, because it's possible
     * that our IVRCSDKPreprocessAvatarCallback may be called by someone else before us, like Av3Emu.
     */
    internal static class PreProcessingFailureCheckHook {
        private static readonly HashSet<VFGameObject> failed = new HashSet<VFGameObject>();
        
        private static void CheckForFailure() {
            // If the uploading object still exists at this point, it means the preprocess hooks failed
            // somewhere between calling our start hook and our end hook.
            if (!Application.isPlaying) return;
            foreach (var obj in failed.NotNull()) {
                var failMarker = GameObjects.Create($"{obj.name} (Preprocess hooks failed)");
                SceneManager.MoveGameObjectToScene(failMarker, obj.scene);
                obj.Destroy();
            }
            failed.Clear();
        }
        
        internal class FailureCheckStart : VrcfAvatarPreprocessor {
            protected override int order => int.MinValue;
            protected override void Process(VFGameObject obj) {
                failed.Add(obj);
                EditorApplication.delayCall += CheckForFailure;
            }
        }

        internal class FailureCheckEnd : VrcfAvatarPreprocessor {
            protected override int order => int.MaxValue;
            protected override void Process(VFGameObject obj) {
                failed.Remove(obj);
            }
        }
    }
}
