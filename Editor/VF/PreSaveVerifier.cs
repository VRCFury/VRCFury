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
                if (typeof(SceneAsset) == AssetDatabase.GetMainAssetTypeAtPath(path)) {
                    for (int n = 0; n < SceneManager.sceneCount; ++n) {
                        var scene = SceneManager.GetSceneAt(n);
                        if (scene.path == path) {
                            var brokenComponents = scene.GetRootGameObjects()
                                .SelectMany(obj => obj.GetComponentsInChildren<VRCFuryComponent>(true))
                                .Where(vrcf => vrcf.IsBroken());
                            foreach (var brokenComponent in brokenComponents) {
                                blocked.Add(brokenComponent.gameObject.name + " in " + path);
                            }
                        }
                    }
                } else {
                    var isBroken = AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<VRCFuryComponent>()
                        .Any(asset => asset.IsBroken());
                    if (isBroken) {
                        blocked.Add(path);
                    }
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
