using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Menu;
using VF.Utils;

namespace VF.Hooks {
    /**
     * Prevents people from importing bad things (like old versions of the VRCSDK or poiyomi)
     * from unitypackages when they are already installed in the project.
     */
    internal static class DoNotImportBadPackagesHook {
        private static readonly string WarningDialogTitle = "Asset Import Warning from VRCFury";
        
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type PackageImportWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.PackageImport");
            public static readonly FieldInfo m_ImportPackageItems = PackageImportWindow?.GetField("m_ImportPackageItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_Tree = PackageImportWindow?.GetField("m_Tree", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_TreeViewState = PackageImportWindow?.GetField("m_TreeViewState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly Type ImportPackageItem = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ImportPackageItem");
            public static readonly FieldInfo AssetPath = ImportPackageItem?.GetField("exportedAssetPath");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Scheduler.Schedule(Check, 0);
        }

        private static IList<string> WithDirectories(string path) {
            var output = new List<string>();
            while (!string.IsNullOrEmpty(path)) {
                output.Add(path.Replace('\\', '/'));
                path = Path.GetDirectoryName(path);
            }
            return output;
        }
        
        private static IList<string> GetVrcsdkPaths(ICollection<string> allPaths) {
            var output = new HashSet<string>();
            foreach (var path in new [] {
                "Packages/com.vrchat.core.vpm-resolver",
                "Packages/com.vrchat.base",
                "Packages/com.vrchat.avatars",
                "Packages/com.vrchat.worlds",
                "Assets/VRCSDK",
                "Assets/Plugins/VRCSDK"
            }) {
                if (allPaths.Contains(path)) output.Add(path);
            }
            return output.ToArray();
        }
 
        private static IList<string> GetPoiyomiPaths(ICollection<string> allPaths) {
            var output = new HashSet<string>();
            foreach (var path in new [] {
                "Packages/com.poiyomi.toon",
                "Assets/_PoiyomiShaders",
                "Assets/_PoiyomiToonShader"
            }) {
                if (allPaths.Contains(path)) output.Add(path);
            }
            foreach (var path in allPaths) {
                if (Path.GetFileName(path) == "poiToonPresets.txt") {
                    var parentPath = Path.GetDirectoryName(path);
                    if (parentPath != null && Path.GetFileName(parentPath).ToLower().Contains("poiyomi")) {
                        output.Add(parentPath.Replace('\\', '/'));
                    }
                }
            }
            return output.ToArray();
        }

        private static EditorWindow lastCheckedWindow = null;
        private static void Check() {
            var importWindow = EditorWindow.focusedWindow;
            if (!Reflection.PackageImportWindow.IsInstanceOfType(importWindow)) return;
            if (importWindow == lastCheckedWindow) return;
            lastCheckedWindow = importWindow;
            
            var items = Reflection.m_ImportPackageItems.GetValue(importWindow) as object[];
            if (items == null) return;

            var allProjectPaths = AssetDatabase.FindAssets("")
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(WithDirectories)
                .Distinct()
                .OrderBy(p => p)
                .ToArray();
            var allPackagePaths = items
                .Select(item => Reflection.AssetPath.GetValue(item) as string)
                .SelectMany(WithDirectories)
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            var vrcsdkProjectPaths = GetVrcsdkPaths(allProjectPaths);
            var poiProjectPaths = GetPoiyomiPaths(allProjectPaths);

            // Some poi plugins (dps) are allowed to import into the poi location even if it's already installed,
            // as long as they don't contain their own full shader files
            var vrcsdkPackagePaths = GetVrcsdkPaths(allPackagePaths);
            var poiPackagePaths = GetPoiyomiPaths(allPackagePaths);
            var packageIncludesPoiShaderFile = allPackagePaths.Any(packagePath => {
                return packagePath.EndsWith(".shader") && poiPackagePaths.Any(p => packagePath.StartsWith(p + "/"));
            });

            // Debug.Log(
            //     $"VRCF import window debugger:\n" +
            //     $"Project VRCSDK: {vrcsdkProjectPaths.Join(',')}\n" +
            //     $"Project Poi: {poiProjectPaths.Join(',')}\n" +
            //     $"Package VRCSDK: {vrcsdkPackagePaths.Join(',')}\n" +
            //     $"Package Poi: {poiPackagePaths.Join(',')}\n" +
            //     $"Package Poi shader: {packageIncludesPoiShaderFile}"
            // );

            var removedPoiFile = false;
            var removedVrcsdkFile = false;
            var newItems = items.Where(item => {
                var path = Reflection.AssetPath.GetValue(item) as string;
                if (path == null) return true;
                if (vrcsdkProjectPaths.Any()) {
                    var isVrcsdkFile = vrcsdkPackagePaths.Any(p => path == p || path.StartsWith(p + "/"));
                    if (isVrcsdkFile) {
                        removedVrcsdkFile = true;
                        return false;
                    }
                }
                if (poiProjectPaths.Any() && packageIncludesPoiShaderFile) {
                    var isPoiFile = poiPackagePaths.Any(p => path == p || path.StartsWith(p + "/"));
                    if (isPoiFile) {
                        removedPoiFile = true;
                        return false;
                    }
                }

                if (BlockScriptImportsMenuItem.Get()) {
                    if (path.EndsWith(".asmdef") || path.EndsWith(".dll") || path.EndsWith(".cs") || path.EndsWith(".shader") || path.EndsWith(".compute") || path.EndsWith(".cginc") || path.EndsWith(".hlsl") || path.EndsWith(".h") || path.EndsWith(".xml")) {
                        return false;
                    }
                }
                return true;
            }).ToArray();

            if (newItems.Length == items.Length) return;

            var removedCount = items.Length - newItems.Length;
            Debug.Log($"VRCF removed {removedCount} conflicting items from the import dialog");

            var arr = Array.CreateInstance(Reflection.ImportPackageItem, newItems.Length);
            newItems.CopyTo(arr, 0);
            Reflection.m_ImportPackageItems.SetValue(importWindow, arr);
            if (Reflection.m_TreeViewState != null && Reflection.m_Tree != null) {
                Reflection.m_TreeViewState?.SetValue(importWindow, null);
                Reflection.m_Tree?.SetValue(importWindow, null);
            }

            if (newItems.Length == 0) {
                (importWindow as EditorWindow)?.Close();
                EditorApplication.delayCall += () => {
                    if (removedPoiFile) {
                        EditorUtility.DisplayDialog(WarningDialogTitle,
                            "Poiyomi is already installed at " + poiProjectPaths.Join(',') +
                            " and must be removed before importing a new version.", "Ok");
                    } else if (removedVrcsdkFile) {
                        if (vrcsdkProjectPaths.Any(p => p.StartsWith("Assets"))) {
                            EditorUtility.DisplayDialog(WarningDialogTitle,
                                "The VRCSDK is already installed at " + vrcsdkProjectPaths.Join(',') +
                                " and must be removed before importing a new version.", "Ok");
                        } else {
                            EditorUtility.DisplayDialog(WarningDialogTitle,
                                "The VRCSDK is already installed using the VCC.", "Ok");
                        }
                    }
                };
            }
        }
    }
}
