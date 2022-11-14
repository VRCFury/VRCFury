using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Model;

namespace VF {
    public class PreSaveVerifier : UnityEditor.AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths) {
            var blocked = new List<string>();
            foreach (var path in paths) {
                var isBroken = false;
                if (typeof(SceneAsset) == AssetDatabase.GetMainAssetTypeAtPath(path)) {
                    for (int n = 0; n < SceneManager.sceneCount; ++n) {
                        var scene = SceneManager.GetSceneAt(n);
                        if (scene.path == path) {
                            isBroken = scene.GetRootGameObjects()
                                .SelectMany(obj => obj.GetComponentsInChildren<VRCFuryComponent>(true))
                                .Any(vrcf => vrcf.IsBroken());
                        }
                    }
                } else {
                    isBroken = AssetDatabase.LoadAllAssetsAtPath(path)
                        .Select(asset => asset as VRCFuryComponent)
                        .Where(asset => asset != null)
                        .Any(asset => asset.IsBroken());
                }
                if (isBroken) {
                    blocked.Add(path);
                }
            }

            if (blocked.Count > 0) {
                EditorUtility.DisplayDialog("VRCFury Error",
                    "These assets have been blocked from saving, because doing so would corrupt the contained" +
                    " VRCFury components. You may need to update VRCFury using Tools -> VRCFury -> Update VRCFury," +
                    " or report the issue to https://discord.com/vrcfury.\n\n" +
                    string.Join("\n", blocked),
                    "Ok");
                paths = paths.ToList().Where(e => !blocked.Contains(e)).ToArray();
            }
            
            return paths;
        }
    }
}
