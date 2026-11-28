using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Menu {
    internal static class LogSelectionReferencesMenuItem {
        private const string Title = "Log References";
        private const string ProjectMenu = "Assets/VRCFury/" + Title;

        private abstract class Reflection : ReflectionHelper {
            public static readonly Type ProjectBrowser = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            public static readonly PropertyInfo UseFolderSelection =
                ProjectBrowser?.VFProperty("useTreeViewSelectionInsteadOfMainSelection");
            public static readonly FieldInfo SearchFilter = ProjectBrowser?.VFField("m_SearchFilter");
            public static readonly PropertyInfo Folders = SearchFilter?.FieldType.VFProperty("folders");
            public static readonly FieldInfo LastProjectBrowser = ProjectBrowser?.VFStaticField("s_LastInteractedProjectBrowser");
        }

        [MenuItem(ProjectMenu, true)]
        private static bool ValidateProject() => GetProjectSelection().Any(EditorUtility.IsPersistent);

        [VFInit]
        private static void InitHierarchyMenu() {
            MenuItems.AddHierarchyCommand(Title, obj => !EditorUtility.IsPersistent(obj), RunHierarchy);
        }

        private static Object[] GetProjectSelection() {
            var selection = Selection.objects;
            if (!ReflectionHelper.IsReady<Reflection>()) return selection;

            var browser = EditorWindow.focusedWindow;
            if (browser == null) browser = Reflection.LastProjectBrowser.GetValue(null) as EditorWindow;
            if (browser == null || !Reflection.ProjectBrowser.IsInstanceOfType(browser)) return selection;
            if (!(bool)Reflection.UseFolderSelection.GetValue(browser) && selection.Length > 0) return selection;

            // Two-column Project browsers keep the selected folders in their search filter.
            var searchFilter = Reflection.SearchFilter.GetValue(browser);
            if (searchFilter == null) return selection;
            var folders = (string[])Reflection.Folders.GetValue(searchFilter);
            if (folders == null) return selection;
            return folders.Where(AssetDatabase.IsValidFolder)
                .Select(AssetDatabase.LoadMainAssetAtPath).Where(folder => folder != null).ToArray();
        }

        [MenuItem(ProjectMenu)]
        private static void RunProject() =>
            Run(GetProjectSelection().Where(EditorUtility.IsPersistent).ToArray());

        private static void RunHierarchy(GameObject clicked) {
            var selected = Selection.gameObjects.Where(obj => !EditorUtility.IsPersistent(obj)).ToArray();
            Run(selected.Contains(clicked) ? selected : new[] { clicked });
        }

        private static void Run(Object[] selection) {
            var outgoing = new List<string>();
            var incoming = new List<string>();
            void Report(bool isOutgoing, string message) {
                if (isOutgoing) outgoing.Add(message);
                else incoming.Add(message);
            }

            var completed = Scan(
                selection,
                Report,
                (path, progress) => EditorUtility.DisplayCancelableProgressBar(Title, path, progress)
            );
            var names = selection.Select(obj => obj is GameObject go && !EditorUtility.IsPersistent(go)
                ? go.asVf().GetDebugPath() : AssetDatabase.GetAssetPath(obj)).Distinct();
            var lines = outgoing.Concat(incoming);
            if (!completed) lines = lines.Append("Search canceled.");
            else if (outgoing.Count + incoming.Count == 0) lines = lines.Append("No external references found.");
            VFLog.Info($"{outgoing.Count + incoming.Count} external references for {string.Join(", ", names)}:\n"
                + string.Join("\n", lines));
        }

        internal static bool Scan(
            Object[] selection,
            Action<bool, string> report,
            Func<string, float, bool> cancel = null
        ) {
            void ReportReference(bool sourceSelected, string source, string target) {
                report(sourceSelected, sourceSelected ? $"{source} -> {target}" : $"{target} <- {source}");
            }

            var assetPaths = selection.Where(EditorUtility.IsPersistent)
                .Select(AssetDatabase.GetAssetPath).Where(path => !string.IsNullOrEmpty(path)).ToHashSet();
            var folders = assetPaths.Where(AssetDatabase.IsValidFolder).Select(path => path + "/").ToArray();
            bool IsSelectedPath(string path) => !string.IsNullOrEmpty(path)
                && (assetPaths.Contains(path) || folders.Any(folder => path.StartsWith(folder, StringComparison.Ordinal)));

            var selectedObjects = selection.OfType<GameObject>().Where(obj => !EditorUtility.IsPersistent(obj))
                .SelectMany(obj => obj.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToHashSet();
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt);
            if (prefabStage != null) scenes = scenes.Append(prefabStage.scene);
            var openScenePaths = scenes.Where(scene => scene.isLoaded).Select(scene => scene.path)
                .Where(path => !string.IsNullOrEmpty(path)).ToHashSet();
            if (prefabStage != null) openScenePaths.Add(prefabStage.assetPath);

            foreach (var scene in scenes.Where(scene => scene.isLoaded).Distinct()) {
                var scenePath = prefabStage != null && scene == prefabStage.scene ? prefabStage.assetPath : scene.path;
                var sceneSelected = IsSelectedPath(scenePath);
                var sceneName = string.IsNullOrEmpty(scenePath) ? scene.name : scenePath;

                foreach (var root in scene.GetRootGameObjects()) {
                    foreach (var component in root.GetComponentsInChildren<UnityEngine.Component>(true)) {
                        if (component == null) continue;
                        var sourceSelected = sceneSelected || selectedObjects.Contains(component.gameObject);
                        if (component is Transform) {
                            if (PrefabUtility.IsAnyPrefabInstanceRoot(component.gameObject)) {
                                var prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject);
                                if (!string.IsNullOrEmpty(prefab) && IsSelectedPath(prefab) != sourceSelected) {
                                    ReportReference(sourceSelected, $"{sceneName} / {component.owner().GetDebugPath()}", prefab);
                                }
                            }
                            continue;
                        }

                        MutableManager.ForEachChildObjectReference(component, (property, target, _) => {
                            var targetObject = target is UnityEngine.Component targetComponent
                                ? targetComponent.gameObject
                                : target as GameObject;
                            var path = AssetDatabase.GetAssetPath(target);
                            var targetSelected = targetObject != null
                                && (selectedObjects.Contains(targetObject) || (targetObject.scene == scene && sceneSelected)
                                    || IsSelectedPath(targetObject.scene.path));
                            if ((targetSelected || IsSelectedPath(path)) == sourceSelected) return;
                            var targetName = !string.IsNullOrEmpty(path)
                                ? path
                                : (targetObject != null ? targetObject.asVf().GetDebugPath() : target.name);
                            ReportReference(
                                sourceSelected,
                                $"{sceneName} / {component.owner().GetDebugPath()} ({component.GetType().Name}.{property})",
                                targetName
                            );
                        });
                    }
                }
            }

            if (assetPaths.Count == 0) return true;
            // The dependency index includes saved scenes without opening them.
            var paths = AssetDatabase.GetAllAssetPaths();
            try {
                for (var i = 0; i < paths.Length; i++) {
                    var path = paths[i];
                    if (i % 128 == 0 && cancel?.Invoke(path, (float)i / paths.Length) == true) return false;
                    if (openScenePaths.Contains(path) || AssetDatabase.IsValidFolder(path)) continue;
                    var sourceSelected = IsSelectedPath(path);
                    foreach (var dependency in AssetDatabase.GetDependencies(path, recursive: false)) {
                        if (IsSelectedPath(dependency) != sourceSelected) ReportReference(sourceSelected, path, dependency);
                    }
                }
                return true;
            } finally {
                if (cancel != null) EditorUtility.ClearProgressBar();
            }
        }
    }
}
