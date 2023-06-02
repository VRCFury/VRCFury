using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Component;
using VF.Model;

namespace VF {
    public class PreSaveVerifier : UnityEditor.AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths) {
            var blocked = new List<string>();
            var blockedPaths = new HashSet<string>();

            foreach (var path in paths) {
                var brokenComponents = new HashSet<VRCFuryComponent>();

                if (path.StartsWith("Packages/com.vrcfury") && File.Exists(path)) {
                    blocked.Add(
                        $"{path} is an immutable VRCFury file and cannot be changed.\n\n" +
                        "If you want to change where menu items go, add a VRCFury Move Menu Item component to your avatar root instead.\n\n" +
                        "If you want to change other things, create your own copy of the file somewhere else in your project.");
                    blockedPaths.Add(path);
                }
                if (typeof(SceneAsset) == AssetDatabase.GetMainAssetTypeAtPath(path)) {
                    for (int n = 0; n < SceneManager.sceneCount; ++n) {
                        var scene = SceneManager.GetSceneAt(n);
                        if (scene.path == path) {
                            brokenComponents.UnionWith(scene.GetRootGameObjects()
                                .SelectMany(obj => obj.GetComponentsInChildren<VRCFuryComponent>(true))
                                .Where(vrcf => vrcf.IsBroken()));
                        }
                    }
                } else {
                    brokenComponents.UnionWith(AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<VRCFuryComponent>()
                        .Where(vrcf => vrcf.IsBroken()));
                }

                foreach (var brokenComponent in brokenComponents) {
                    blocked.Add($"{brokenComponent.gameObject.name} in {path} ({brokenComponent.GetBrokenMessage()})");
                    blockedPaths.Add(path);
                }
            }

            if (blocked.Count > 0) {
                EditorUtility.DisplayDialog("VRCFury Error",
                    "These assets have been blocked from saving, because doing so would corrupt the contained" +
                    " VRCFury components. You may need to update VRCFury using Tools -> VRCFury -> Update VRCFury," +
                    " or report the issue to https://vrcfury.com/discord\n\n" +
                    string.Join("\n", blocked),
                    "Ok");
                paths = paths.ToList().Where(e => !blockedPaths.Contains(e)).ToArray();
            }
            
            return paths;
        }
    }
}
