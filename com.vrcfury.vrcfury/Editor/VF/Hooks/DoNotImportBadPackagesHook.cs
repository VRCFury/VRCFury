using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VF.Hooks {
    /**
     * Prevents people from importing bad things (like old versions of the VRCSDK or poiyomi)
     * from unitypackages when they are already installed in the project.
     */
    public static class DoNotImportBadPackagesHook {
        private static readonly Type PackageImportWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.PackageImport");
        private static readonly FieldInfo m_ImportPackageItems = PackageImportWindow?.GetField("m_ImportPackageItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_Tree = PackageImportWindow?.GetField("m_Tree", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_TreeViewState = PackageImportWindow?.GetField("m_TreeViewState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Type ImportPackageItem = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ImportPackageItem");
        private static readonly FieldInfo AssetPath = ImportPackageItem?.GetField("exportedAssetPath");

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
        public static void Init() {
            if (PackageImportWindow == null || m_ImportPackageItems == null || ImportPackageItem == null || AssetPath == null) return;
            EditorApplication.update += Check;
        }

        private static bool checkedContent = false;
        private static void Check() {
            var importWindow = Resources.FindObjectsOfTypeAll(PackageImportWindow).FirstOrDefault();
            if (importWindow == null) {
                checkedContent = false;
                return;
            }

            if (checkedContent) return;
            checkedContent = true;

            var items = m_ImportPackageItems.GetValue(importWindow) as object[];
            if (items == null) return;

            var vrcsdkProjectPath = vrcsdkLocations.FirstOrDefault(path => Directory.Exists(path));
            var poiProjectPath = poiyomiLocations.FirstOrDefault(path => Directory.Exists(path));

            var removedPoiFile = false;
            var removedVrcsdkFile = false;
            var newItems = items.Where(item => {
                var path = AssetPath.GetValue(item) as string;
                if (path == null) return true;
                if (vrcsdkProjectPath != null) {
                    var isVrcsdkFile = vrcsdkLocations.Any(p => path == p || path.StartsWith(p + "/"));
                    if (isVrcsdkFile) {
                        removedVrcsdkFile = true;
                        return false;
                    }
                }
                if (poiProjectPath != null) {
                    var isPoiFile = poiyomiLocations.Any(p => path == p || path.StartsWith(p + "/"));
                    if (isPoiFile) {
                        removedPoiFile = true;
                        return false;
                    }
                }
                return true;
            }).ToArray();

            if (newItems.Length == items.Length) return;

            var arr = Array.CreateInstance(ImportPackageItem, newItems.Length);
            newItems.CopyTo(arr, 0);
            m_ImportPackageItems.SetValue(importWindow, arr);
            if (m_TreeViewState != null && m_Tree != null) {
                m_TreeViewState?.SetValue(importWindow, null);
                m_Tree?.SetValue(importWindow, null);
            }

            if (newItems.Length == 0) {
                (importWindow as EditorWindow)?.Close();
                EditorApplication.delayCall += () => {
                    if (removedPoiFile) {
                        EditorUtility.DisplayDialog("Asset Import",
                            "Poiyomi is already installed at " + poiProjectPath +
                            " and must be removed before importing a new version.", "Ok");
                    } else if (removedVrcsdkFile) {
                        if (vrcsdkProjectPath != null && vrcsdkProjectPath.StartsWith("Assets")) {
                            EditorUtility.DisplayDialog("Asset Import",
                                "The VRCSDK is already installed at " + vrcsdkProjectPath +
                                " and must be removed before importing a new version.", "Ok");
                        } else {
                            EditorUtility.DisplayDialog("Asset Import",
                                "The VRCSDK is already installed using the VCC.", "Ok");
                        }
                    }
                };
            }
        }
    }
}
