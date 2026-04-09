using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Model;

namespace VF.Utils {
    internal static class DirtyUtils {
        [InitializeOnLoadMethod]
        private static void MakeMarkDirtyAvailableToRuntime() {
            VRCFury.markDirty = Dirty;
        }
        public static void Dirty(this Object obj) {
            EditorUtility.SetDirty(obj);

            // This shouldn't be needed in unity 2020+
            if (obj is GameObject go) {
                MarkSceneDirty(go.scene);
            } else if (obj is UnityEngine.Component c) {
                MarkSceneDirty(c.owner().scene);
            }
        }

        private static void MarkSceneDirty(Scene scene) {
            if (Application.isPlaying) return;
            if (scene == null) return;
            if (!scene.isLoaded) return;
            if (!scene.IsValid()) return;
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
