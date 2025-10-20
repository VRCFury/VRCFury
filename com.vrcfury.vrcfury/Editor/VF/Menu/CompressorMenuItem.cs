using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class CompressorMenuItem {
        private const string EditorPref = "com.vrcfury.parameterCompressor";

        public enum Value {
            Compress,
            Ask,
            Fail
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static Value Get() {
            var i = EditorPrefs.GetInt(EditorPref, 0);
            if (i == 1) return Value.Ask;
            if (i == 2) return Value.Fail;
            return Value.Compress;
        }
        private static void UpdateMenu() {
            var value = Get();
            UnityEditor.Menu.SetChecked(MenuItems.compressCompress, value == Value.Compress);
            UnityEditor.Menu.SetChecked(MenuItems.compressAsk, value == Value.Ask);
            UnityEditor.Menu.SetChecked(MenuItems.compressFail, value == Value.Fail);
        }

        [MenuItem(MenuItems.compressHeader, priority = MenuItems.compressHeaderPriority)]
        private static void Header() {}

        [MenuItem(MenuItems.compressHeader, true)]
        private static bool HeaderEnabled() => false;

        [MenuItem(MenuItems.compressCompress, priority = MenuItems.compressCompressPriority)]
        private static void CompressCompress() {
            EditorPrefs.SetInt(EditorPref, 0);
            UpdateMenu();
        }
        [MenuItem(MenuItems.compressAsk, priority = MenuItems.compressAskPriority)]
        private static void CompressAsk() {
            EditorPrefs.SetInt(EditorPref, 1);
            UpdateMenu();
        }
        [MenuItem(MenuItems.compressFail, priority = MenuItems.compressFailPriority)]
        private static void CompressFail() {
            EditorPrefs.SetInt(EditorPref, 2);
            UpdateMenu();
        }
    }
}