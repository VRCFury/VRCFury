using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Inspector;

namespace VF.Menu {
    public class HapticsMenuItem {

        private const string DialogTitle = "VRCFury Haptics";

        public static void Create(bool plug) {
            var newObj = GameObjects.Create(plug ? "Haptic Plug" : "Haptic Socket", Selection.activeTransform);

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
            
            EditorUtility.DisplayDialog(DialogTitle,
                $"{(plug ? "Plug" : "Socket")} created!\n\nDon't forget to attach it to an appropriate bone on your avatar and rotate it so it faces the correct direction!", "Ok");
            
            var sv = EditorWindow.GetWindow<SceneView>();
            if (sv != null) sv.drawGizmos = true;
        }
    }
}
