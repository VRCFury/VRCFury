using System;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class EditorChanges {
        public static long Version { get; private set; }
        // Geometry caches can check their own transform dependencies without rebuilding
        // for every unrelated move. Other subscribers still receive all changes.
        public static long NonTransformVersion { get; private set; }
        public static event Action OnChanged;

        public static void Changed() => Changed(false);

        internal static void Changed(bool transformsOnly) {
            Version++;
            if (!transformsOnly) NonTransformVersion++;
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
                if (stream.length == 0) return;
                var transformsOnly = true;
                for (var i = 0; i < stream.length; i++) {
                    if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectOrComponentProperties) {
                        transformsOnly = false;
                        break;
                    }
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);
                    if (!(EditorUtility.InstanceIDToObject(change.instanceId) is Transform)) {
                        transformsOnly = false;
                        break;
                    }
                }
                Changed(transformsOnly);
            };
        }
    }
}
