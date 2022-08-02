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
            mainLight.type = LightType.Point;
            mainLight.color = Color.black;
            mainLight.range = ring ? 0.42f : 0.41f;
            mainLight.shadows = LightShadows.None;
            mainLight.renderMode = LightRenderMode.ForceVertex;

            var front = new GameObject("Front");
            front.transform.SetParent(newObj.transform);
            var frontLight = front.AddComponent<Light>();
            front.transform.position = new Vector3(0, 0, 0.01f);
            frontLight.type = LightType.Point;
            frontLight.color = Color.black;
            frontLight.range = 0.45f;
            frontLight.shadows = LightShadows.None;
            frontLight.renderMode = LightRenderMode.ForceVertex;
            
            var marker = new GameObject(DPSContactUpgradeBuilder.MARKER_ORF);
            marker.transform.SetParent(newObj.transform);

            if (Selection.activeGameObject) {
                newObj.transform.SetParent(Selection.activeGameObject.transform);
                newObj.transform.localPosition = Vector3.zero;
            }
            
            Tools.pivotRotation = PivotRotation.Local;
            Tools.pivotMode = PivotMode.Pivot;
            Tools.current = Tool.Move;
            Selection.SetActiveObjectWithContext(newObj, newObj);
            SceneView.FrameLastActiveSceneView();
            
            EditorUtility.DisplayDialog("OscGB",
                (ring ? "Ring" : "Hole") +
                " added.\n\nBlue arrow should face OUTWARD.\nDon't forget to run OseGB Upgrader after placing in position.", "Ok");
        }

        public static void RunPen() {
            var selected = Selection.activeGameObject;
            if (!selected) {
                EditorUtility.DisplayDialog("OseGB Setup", "You must have a skinned or unskinned mesh selected", "Ok");
                return;
            }

            var mesh = selected.GetComponent<MeshRenderer>();
            var smesh = selected.GetComponent<SkinnedMeshRenderer>();
            if (!mesh && !smesh) {
                EditorUtility.DisplayDialog("OseGB Setup", "You must have a skinned or unskinned mesh selected", "Ok");
                return;
            }

            var avatar = MenuUtils.GetSelectedAvatar();
            if (!avatar) {
                EditorUtility.DisplayDialog("OseGB Setup", "Selected mesh does not seem to be inside of an avatar", "Ok");
                return;
            }

            var exists = selected.transform.Find(DPSContactUpgradeBuilder.MARKER_PEN);
            if (exists) {
                EditorUtility.DisplayDialog("OseGB Setup", "Selected mesh is already marked as a penetrator", "Ok");
                return;
            }

            var marker = new GameObject(DPSContactUpgradeBuilder.MARKER_PEN);
            marker.transform.SetParent(selected.transform);

            DPSContactUpgradeBuilder.Apply(avatar);
        }
    }
}
