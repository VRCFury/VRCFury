using UnityEditor;
using UnityEngine;
using VF.Inspector;
using VF.Model;

namespace VF.Menu {
    public class HapticsMenuItem {

        public static void Create() {
            var newObj = new GameObject("Orifice");

            var o = newObj.AddComponent<HapticSocket>();
            o.addLight = HapticSocket.AddLight.Auto;
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
            
            EditorUtility.DisplayDialog("OGB",
                "Orifice added.\n\nDon't forget to attach it to an appropriate bone on your avatar and rotate it so the arrow faces correctly!", "Ok");
            
            SceneView sv = EditorWindow.GetWindow<SceneView>();
            if (sv != null) sv.drawGizmos = true;
        }

        public static void RunBake() {
            var ok = EditorUtility.DisplayDialog("OGB",
                "This utility will convert the selected OscGB component into plain VRChat colliders, so that this prefab can be distributed without the client needing VRCFury. This is intended for avatar artists only.",
                "I am an avatar artist distributing this package, Continue",
                "Cancel"
            );
            if (!ok) return;
            
            ok = EditorUtility.DisplayDialog("OGB",
                "Beware that baked OscGB components do not provide toy support. Only senders are included, not receivers, so toys will work for partners of this prefab, but not the owner." +
                " Users can still easily re-add toy support themselves by simply running the VRCFury OscGB upgrader on their avatar (which will convert this bake back to a component).",
                "I understand the limitations, bake now",
                "Cancel"
            );
            if (!ok) return;

            if (!Selection.activeGameObject) {
                EditorUtility.DisplayDialog("OGB","No object selected", "Ok");
                return;
            }

            var pen = Selection.activeGameObject.GetComponent<OGBPenetrator>();
            var orf = Selection.activeGameObject.GetComponent<HapticSocket>();
            if (!pen && !orf) {
                EditorUtility.DisplayDialog("OGB","No penetrator or orifice component found on selected object", "Ok");
                return;
            }

            if (pen) {
                OGBPenetratorEditor.Bake(pen, onlySenders:true);
                Object.DestroyImmediate(pen);
            }
            if (orf) {
                HapticSocketEditor.Bake(orf, onlySenders:true);
                Object.DestroyImmediate(orf);
            }
        }
    }
}
