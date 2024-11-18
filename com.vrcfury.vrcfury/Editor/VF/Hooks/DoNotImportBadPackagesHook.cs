using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

        private static readonly string[] vrcsdkLocations = {
            "Packages/com.vrchat.base",
            "Packages/com.vrchat.avatars",
            "Packages/com.vrchat.worlds",
            "Assets/VRCSDK",
            "Assets/Plugins/VRCSDK"
        };
        private static readonly string[] poiyomiLocations = {
            "Packages/com.poiyomi.toon",
            "Assets/_PoiyomiShaders",
            "Assets/_PoiyomiToonShader"
        };

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!UnityReflection.IsReady(typeof(UnityReflection.PackageImport))) return;
            Scheduler.Schedule(Check, 0);
        }

        private static EditorWindow lastCheckedWindow = null;
        private static void Check() {
            var importWindow = EditorWindow.focusedWindow;
            if (!UnityReflection.PackageImport.PackageImportWindow.IsInstanceOfType(importWindow)) return;
            if (importWindow == lastCheckedWindow) return;
            lastCheckedWindow = importWindow;

            var items = UnityReflection.PackageImport.m_ImportPackageItems.GetValue(importWindow) as object[];
            if (items == null) return;

            var vrcsdkProjectPath = vrcsdkLocations.FirstOrDefault(path => Directory.Exists(path));
            var poiProjectPath = poiyomiLocations.FirstOrDefault(path => Directory.Exists(path));

            // Some poi plugins (dps) are allowed to import into the poi location even if it's already installed,
            // as long as they don't contain their own full shader files
            var packageIncludesPoiShaderFile = items.Any(item => {
                var path = UnityReflection.PackageImport.AssetPath.GetValue(item) as string;
                if (path == null) return false;
                return path.EndsWith(".shader") && poiyomiLocations.Any(p => path.StartsWith(p + "/"));
            });

            var removedPoiFile = false;
            var removedVrcsdkFile = false;
            var newItems = items.Where(item => {
                var path = UnityReflection.PackageImport.AssetPath.GetValue(item) as string;
                if (path == null) return true;
                if (vrcsdkProjectPath != null) {
                    var isVrcsdkFile = vrcsdkLocations.Any(p => path == p || path.StartsWith(p + "/"));
                    if (isVrcsdkFile) {
                        removedVrcsdkFile = true;
                        return false;
                    }
                }
                if (poiProjectPath != null && packageIncludesPoiShaderFile) {
                    var isPoiFile = poiyomiLocations.Any(p => path == p || path.StartsWith(p + "/"));
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

            var arr = Array.CreateInstance(UnityReflection.PackageImport.ImportPackageItem, newItems.Length);
            newItems.CopyTo(arr, 0);
            UnityReflection.PackageImport.m_ImportPackageItems.SetValue(importWindow, arr);
            if (UnityReflection.PackageImport.m_TreeViewState != null && UnityReflection.PackageImport.m_Tree != null) {
                UnityReflection.PackageImport.m_TreeViewState?.SetValue(importWindow, null);
                UnityReflection.PackageImport.m_Tree?.SetValue(importWindow, null);
            }

            if (newItems.Length == 0) {
                (importWindow as EditorWindow)?.Close();
                EditorApplication.delayCall += () => {
                    if (removedPoiFile) {
                        EditorUtility.DisplayDialog(WarningDialogTitle,
                            "Poiyomi is already installed at " + poiProjectPath +
                            " and must be removed before importing a new version.", "Ok");
                    } else if (removedVrcsdkFile) {
                        if (vrcsdkProjectPath != null && vrcsdkProjectPath.StartsWith("Assets")) {
                            EditorUtility.DisplayDialog(WarningDialogTitle,
                                "The VRCSDK is already installed at " + vrcsdkProjectPath +
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
