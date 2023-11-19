using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VF.Menu;

namespace VF {

    [InitializeOnLoad]
    public static class VRCFAutoUpload {

        static VRCFAutoUpload() {
            EditorApplication.update += OnUpdate;
        }

        private static bool hooked;

        private static void OnUpdate() {
            if (!EditorApplication.isPlaying) {
                hooked = false;
                return;
            }
            if (!AutoUploadMenuItem.Get()) return;
            if (hooked) return;

            // Bail if this is first upload or avatar info not yet loaded
            var imageUploadToggleObject = GameObject.Find("ImageUploadToggle");
            if (imageUploadToggleObject == null) {
                // Missing img upload toggle
                return;
            }
            var imageUploadToggle = imageUploadToggleObject.GetComponent<Toggle>();
            var firstTimeUploadOrNotLoaded = imageUploadToggle.isOn;
            if (firstTimeUploadOrNotLoaded) {
                return;
            }

            // Accept Terms
            var toggleWarrantObject = GameObject.Find("ToggleWarrant");
            if (toggleWarrantObject == null) {
                return;
            }
            var toggleWarrant = toggleWarrantObject.GetComponent<Toggle>();
            toggleWarrant.isOn = true;
            toggleWarrant.onValueChanged.Invoke(true);

            // Click Upload
            var uploadButtonObject = GameObject.Find("UploadButton");
            if (uploadButtonObject == null) {
                return;
            }
            hooked = true;
            var button = uploadButtonObject.GetComponent<Button>();
            button.onClick.Invoke();
        }
    }

}
