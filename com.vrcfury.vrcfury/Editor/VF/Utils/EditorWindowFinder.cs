using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    /**
     * This exists because running Resources.FindObjectsOfTypeAll is really slow, and is otherwise
     * the only way to find active EditorWindows.
     */
    internal static class EditorWindowFinder {
        private static readonly HashSet<EditorWindow> activeWindows = new HashSet<EditorWindow>();

        [InitializeOnLoadMethod]
        private static void Init() {
            activeWindows.UnionWith(Resources.FindObjectsOfTypeAll<EditorWindow>());
            Scheduler.Schedule(() => {
                activeWindows.Add(EditorWindow.focusedWindow);
            }, 0);
            Scheduler.Schedule(() => {
                activeWindows.RemoveWhere(window => window == null);
            }, 1000);
        }

        public static IList<T> GetWindows<T>() where T : EditorWindow {
            return activeWindows.NotNull().OfType<T>().ToList();
        }
        
        public static IList<EditorWindow> GetWindows(Type type) {
            return activeWindows.NotNull().Where(type.IsInstanceOfType).ToList();
        }
    }
}
