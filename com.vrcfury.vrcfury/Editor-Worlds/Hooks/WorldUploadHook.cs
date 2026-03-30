using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;

namespace VF.Hooks {
    internal class WorldUploadHook : VrcfWorldPreprocessor {
        protected override int order => -10000;

        protected override void Process(Scene scene) {
            var obj = new GameObject("VRCFury ran!");
            SceneManager.MoveGameObjectToScene(obj, scene);


            // foreach (var cube in VFGameObject.GetRoots(scene).Where(o => o.name == "Cube (1)")) {
            //     var copy = cube.Clone();
            //     var wpos = copy.worldPosition;
            //     wpos.x += 1;
            //     copy.worldPosition = wpos;
            // }
        }
    }
}
