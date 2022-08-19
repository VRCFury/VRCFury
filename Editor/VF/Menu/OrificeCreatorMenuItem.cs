using UnityEditor;
using UnityEngine;
using VF.Inspector;
using VF.Model;

namespace VF.Menu {
    public class OrificeCreatorMenuItem {

        public static void RunHole() {
            Create(false);
        }
        
        public static void RunRing() {
            Create(true);
        }

        private static void Create(bool ring) {
            var newObj = new GameObject(ring ? "Ring" : "Hole");

            var o = newObj.AddComponent<OGBOrifice>();
            o.addLight = ring ? AddLight.Ring : AddLight.Hole;

            if (Selection.activeGameObject) {
                newObj.transform.SetParent(Selection.activeGameObject.transform);
                newObj.transform.localPosition = Vector3.zero;
            }
            
            Tools.pivotRotation = PivotRotation.Local;
            Tools.pivotMode = PivotMode.Pivot;
            Tools.current = Tool.Move;
            Selection.SetActiveObjectWithContext(newObj, newObj);
            //SceneView.FrameLastActiveSceneView();
            
            EditorUtility.DisplayDialog("OscGB",
                (ring ? "Ring" : "Hole") +
                " added.\n\nDon't forget to attach it to an appropriate bone on your avatar and rotate it so the blue arrow faces outward!", "Ok");
        }

        public static void RunBake() {
            var ok = EditorUtility.DisplayDialog("OscGB",
                "This utility will convert the selected OscGB component into plain VRChat colliders, so that this prefab can be distributed without the client needing VRCFury. This is intended for avatar artists only.",
                "I am an avatar artist distributing this package, Continue",
                "Cancel"
            );
            if (!ok) return;
            
            ok = EditorUtility.DisplayDialog("OscGB",
                "Beware that baked OscGB components do not provide toy support. Only senders are included, not receivers, so toys will work for partners of this prefab, but not the owner." +
                " Users can still easily re-add toy support themselves by simply running the VRCFury OscGB upgrader on their avatar (which will convert this bake back to a component).",
                "I understand the limitations, bake now",
                "Cancel"
            );
            if (!ok) return;

            if (!Selection.activeGameObject) {
                EditorUtility.DisplayDialog("OscGB","No object selected", "Ok");
                return;
            }

            var pen = Selection.activeGameObject.GetComponent<OGBPenetrator>();
            var orf = Selection.activeGameObject.GetComponent<OGBOrifice>();
            if (!pen && !orf) {
                EditorUtility.DisplayDialog("OscGB","No penetrator or orifice component found on selected object", "Ok");
                return;
            }

            if (pen) {
                OGBPenetratorEditor.Bake(pen, onlySenders:true);
            }
            if (orf) {
                OGBOrificeEditor.Bake(orf, onlySenders:true);
            }
        }
    }
}
