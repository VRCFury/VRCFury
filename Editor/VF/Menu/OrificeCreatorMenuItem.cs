using UnityEditor;
using UnityEngine;

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
            var main = new GameObject("Root");
            main.transform.SetParent(newObj.transform);
            var mainLight = main.AddComponent<Light>();
            var front = new GameObject("Front");
            front.transform.SetParent(newObj.transform);
            var frontLight = front.AddComponent<Light>();
            front.transform.position = new Vector3(0, 0, 0.01f);

            mainLight.color = Color.black;
            frontLight.color = Color.black;
            mainLight.range = ring ? 0.42f : 0.41f;
            frontLight.range = 0.45f;

            if (Selection.activeGameObject) {
                newObj.transform.SetParent(Selection.activeGameObject.transform);
                newObj.transform.localPosition = Vector3.zero;
                SceneView.FrameLastActiveSceneView();
            }
            
            Selection.SetActiveObjectWithContext(newObj, newObj);
            Tools.pivotRotation = PivotRotation.Local;
            Tools.pivotMode = PivotMode.Pivot;
            Tools.current = Tool.Move;

            EditorUtility.DisplayDialog("OscGB",
                (ring ? "Ring" : "Hole") +
                " added.\n\nBlue arrow should face OUTWARD.\nDon't forget to run OseGB Upgrader after placing in position.", "Ok");
        }
    }
}
