using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using Object = UnityEngine.Object;

namespace VF.Menu {
    [InitializeOnLoad]
    public class NdmfFirstMenuItem {
        private const string EditorPref = "com.vrcfury.ndmfFirst";

        static NdmfFirstMenuItem() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.ndmfFirst, Get());
        }

        [MenuItem(MenuItems.ndmfFirst, priority = MenuItems.ndmfFirstPriority)]
        private static void Click() {
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }

        private static Type GetAlreadyProcessedTagType() {
            return ReflectionUtils.GetTypeFromAnyAssembly("nadena.dev.ndmf.runtime.AlreadyProcessedTag");
        }

        public static void Run(VFGameObject obj) {
            if (!Get()) return;
            var processedTagType = GetAlreadyProcessedTagType();
            if (processedTagType == null) return;
            var processor = ReflectionUtils.GetTypeFromAnyAssembly("nadena.dev.ndmf.AvatarProcessor");
            if (processor == null) return;
            if (obj.GetComponent(processedTagType) != null) return;
            Debug.Log("VRCF is triggering NDMF to run");
            var processMethod = processor.GetMethod(
                "ProcessAvatar",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new Type[] { typeof(GameObject) },
                null
            );
            if (processMethod == null) return;
            processMethod.Invoke(null, new object[] { (GameObject)obj });
            obj.AddComponent(processedTagType);
        }

        public static void Cleanup(VFGameObject obj) {
            if (!Get()) return;
            var processedTagType = GetAlreadyProcessedTagType();
            if (processedTagType == null) return;
            foreach (var c in obj.GetComponents(processedTagType)) {
                Object.DestroyImmediate(c);
            }
        }
    }
}
