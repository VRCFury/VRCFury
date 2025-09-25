using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Inspector;
using VF.Utils;

namespace VF.Menu {
    internal static class HapticsMenuItem {

        private const string DialogTitle = "VRCFury SPS";

        public static void Create(bool plug) {
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
