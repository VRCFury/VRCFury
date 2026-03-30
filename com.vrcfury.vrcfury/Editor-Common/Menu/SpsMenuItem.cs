using System.Linq;
using UnityEditor;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Exceptions;
using VF.Utils;

namespace VF.Menu {
    internal static class SpsMenuItem {
        [MenuItem("GameObject/VRCFury/Create SPS Socket", priority = 40)]
        [MenuItem(MenuItems.createSocket, priority = MenuItems.createSocketPriority)]
        public static void RunSocket() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                Create(false);
            });
        }

        [MenuItem("GameObject/VRCFury/Create SPS Plug", priority = 41)]
        [MenuItem(MenuItems.createPlug, priority = MenuItems.createPlugPriority)]
        public static void RunPlug() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                Create(true);
            });
        }

        private const string DialogTitle = "VRCFury SPS";

        private static void Create(bool plug) {
            var newObj = GameObjects.Create(plug ? "SPS Plug" : "SPS Socket", Selection.activeTransform);

            if (plug) {
                newObj.AddComponent<VRCFuryHapticPlug>();
            } else {
                newObj.AddComponent<VRCFuryHapticSocket>();
            }

            Tools.pivotRotation = PivotRotation.Local;
            Tools.pivotMode = PivotMode.Pivot;
            Tools.current = Tool.Move;
            Selection.SetActiveObjectWithContext(newObj, newObj);
            //SceneView.FrameLastActiveSceneView();

            DialogUtils.DisplayDialog(DialogTitle,
                $"{(plug ? "Plug" : "Socket")} created!\n\nDon't forget to attach it to an appropriate bone on your avatar and rotate it so it faces the correct direction!", "Ok");

            var sv = EditorWindowFinder.GetWindows<SceneView>().FirstOrDefault();
            if (sv != null) sv.drawGizmos = true;
        }
    }
}