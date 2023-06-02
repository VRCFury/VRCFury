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
                EditorUtility.DisplayDialog("VRCFury Blocked Saving",
                    string.Join("\n", blocked),
                    "Ok"); 
                paths = paths.ToList().Where(e => !blockedPaths.Contains(e)).ToArray();
            }
            
            return paths;
        }
        
        public static bool IsImmutableVrcf(string path) {
            // We verify if File.Exists, so that prefabs can still be edited in dev mode (when package is a file system folder)
            if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath("."))) {
                return false;
            }

            return path.StartsWith("Packages/com.vrcfury.vrcfury/")
                   || path.StartsWith("Packages/com.vrcfury.prefabs/");
        }
    }
}
