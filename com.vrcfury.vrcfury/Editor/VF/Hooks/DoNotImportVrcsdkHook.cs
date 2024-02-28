using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VF.Hooks {
    public static class DoNotImportVrcsdkHook {
        private static readonly Type PackageImportWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.PackageImport");
        private static readonly FieldInfo m_ImportPackageItems = PackageImportWindow?.GetField("m_ImportPackageItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_Tree = PackageImportWindow?.GetField("m_Tree", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Type ImportPackageItem = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ImportPackageItem");
        private static readonly FieldInfo AssetPath = ImportPackageItem?.GetField("exportedAssetPath");

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

            var newItems = items.Where(item => {
                var path = AssetPath.GetValue(item) as string;
                if (path == null) return true;
                if (path == "Assets/VRCSDK" || path.StartsWith("Assets/VRCSDK/")) return false;
                return true;
            }).ToArray();

            if (newItems.Length == items.Length) return;

            var arr = Array.CreateInstance(ImportPackageItem, newItems.Length);
            newItems.CopyTo(arr, 0);
            m_ImportPackageItems.SetValue(importWindow, arr);
            m_Tree?.SetValue(importWindow, null);
        }
    }
}
