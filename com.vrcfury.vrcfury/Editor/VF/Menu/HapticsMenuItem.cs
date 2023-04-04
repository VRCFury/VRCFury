using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Inspector;
using VF.Model;

namespace VF.Menu {
    public class HapticsMenuItem {

        private const string DialogTitle = "VRCFury Haptics";

        public static void Create() {
            var newObj = new GameObject("Haptic Socket");

            var o = newObj.AddComponent<VRCFuryHapticSocket>();
            o.addLight = VRCFuryHapticSocket.AddLight.Auto;
            o.addMenuItem = true;

            if (Selection.activeGameObject) {
                newObj.transform.SetParent(Selection.activeGameObject.transform);
                newObj.transform.localPosition = Vector3.zero;
            }
            
            Tools.pivotRotation = PivotRotation.Local;
            Tools.pivotMode = PivotMode.Pivot;
            Tools.current = Tool.Move;
            Selection.SetActiveObjectWithContext(newObj, newObj);
            //SceneView.FrameLastActiveSceneView();
            
            EditorUtility.DisplayDialog(DialogTitle,
                "Object created!\n\nDon't forget to attach it to an appropriate bone on your avatar and rotate it so the arrow faces correctly!", "Ok");
            
            SceneView sv = EditorWindow.GetWindow<SceneView>();
            if (sv != null) sv.drawGizmos = true;
        }

        public static void RunBake() {
            var ok = EditorUtility.DisplayDialog(DialogTitle,
                "This utility will convert the selected VRCFury haptic component into plain VRChat colliders, so that this prefab can be distributed without the client needing VRCFury. This is intended for avatar artists only.",
                "I am an avatar artist distributing this package, Continue",
                "Cancel"
            );
            if (!ok) return;
            
            ok = EditorUtility.DisplayDialog(DialogTitle,
                "BEWARE that baked components can only send haptic triggers, not receive. Thus after baking, this will trigger haptics on other users, but not receive haptics for the owner." +
                " Users can still easily re-add full support themselves by simply running the VRCFury 'Upgrade Legacy Haptics' tool on their avatar (which will convert this bake back to a component).",
                "I understand the limitations, bake now",
                "Cancel"
            );
            if (!ok) return;

            if (!Selection.activeGameObject) {
                EditorUtility.DisplayDialog(DialogTitle,"No object selected", "Ok");
                return;
            }

            var pen = Selection.activeGameObject.GetComponent<VRCFuryHapticPlug>();
            var orf = Selection.activeGameObject.GetComponent<VRCFuryHapticSocket>();
            if (!pen && !orf) {
                EditorUtility.DisplayDialog(DialogTitle,"No haptic components found on selected object", "Ok");
                return;
            }

            if (pen) {
                VRCFuryHapticPlugEditor.Bake(pen, onlySenders:true);
                Object.DestroyImmediate(pen);
            }
            if (orf) {
                VRCFuryHapticSocketEditor.Bake(orf, onlySenders:true);
                Object.DestroyImmediate(orf);
            }
        }
    }
}
