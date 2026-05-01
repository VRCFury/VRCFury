using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VF.Utils;

namespace VF.Menu {
    internal static class SceneDirtyLoggerMenuItem {
        private const string EditorPref = "com.vrcfury.sceneDirtyLogger";

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, false);
        }

        [MenuItem(MenuItems.sceneDirtyLogger, priority = MenuItems.sceneDirtyLoggerPriority)]
        private static void Click() {
            var enabling = !Get();
            if (enabling) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "This will log to the console the cause for any time the scene is dirtied or an asset is saved.\n\n" +
                    "Are you sure?",
                    "Yes, enable logging",
                    "Cancel"
                );
                if (!ok) return;
            }

            EditorPrefs.SetBool(EditorPref, !Get());
        }

        [MenuItem(MenuItems.sceneDirtyLogger, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.sceneDirtyLogger, Get());
            return true;
        }

        //

        private static readonly Queue<string> RecentChanges = new Queue<string>();

        private static void Log(string msg) {
            Debug.LogWarning(
                $"[VRCFury Scene Dirty Logger] {msg}"
                + "\n\nRecent changes:\n" + string.Join("\n", RecentChanges)
                + "\n"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorSceneManager.sceneDirtied += scene => {
                if (!Get()) return;
                Log("Scene dirtied: " + scene.path);
            };

            Undo.postprocessModifications += mods => {
                if (!Get()) return mods;
                foreach (var mod in mods) {
                    var target = mod.currentValue?.target;
                    if (!IsSceneObject(target)) continue;
                    var path = mod.currentValue?.propertyPath;
                    Remember($"Undo modification: {target} :: {path}");
                }
                return mods;
            };

#if UNITY_2022_1_OR_NEWER
            ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream stream) => {
                if (!Get()) return;
                Remember($"ObjectChangeEvents: {stream.length} changes");
            };
#endif
        }

        public class Processor : UnityEditor.AssetModificationProcessor {
            private static string[] OnWillSaveAssets(string[] paths) {
                if (!Get()) return paths;
                Log("Assets saving:\n" + string.Join("\n", paths));
                return paths;
            }
        }

        private static void Remember(string msg) {
            RecentChanges.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg);
            while (RecentChanges.Count > 30) {
                RecentChanges.Dequeue();
            }
        }

        private static bool IsSceneObject(UnityEngine.Object obj) {
            if (obj is UnityEngine.Component c) return c.gameObject.scene.IsValid();
            if (obj is GameObject go) return go.scene.IsValid();
            return false;
        }
    }
}
