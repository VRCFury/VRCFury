using System;
using UnityEditor;

namespace VF.Utils {
    internal static class EditorChanges {
        public static long Version { get; private set; }
        public static event Action OnChanged;

        public static void Changed() {
            Version++;
            OnChanged?.Invoke();
        }

        [VFInit]
        private static void Init() {
            EditorApplication.hierarchyChanged += Changed;
            EditorApplication.projectChanged += Changed;
            Undo.undoRedoPerformed += Changed;
            EditorApplication.playModeStateChanged += _ => Changed();
            EditorApplication.focusChanged += _ => Changed();
            ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream stream) => {
                if (stream.length > 0) Changed();
            };
        }
    }
}
