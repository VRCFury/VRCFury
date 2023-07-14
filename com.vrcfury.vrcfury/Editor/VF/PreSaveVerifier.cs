using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Component;

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
                            brokenComponents.UnionWith(VFGameObject.GetRoots(scene)
                                .SelectMany(obj => obj.GetComponentsInSelfAndChildren<VRCFuryComponent>())
                                .Where(vrcf => vrcf.IsBroken()));
                        }
                    }
                } else {
                    brokenComponents.UnionWith(AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<VRCFuryComponent>()
                        .Where(vrcf => vrcf.IsBroken()));
                }

                foreach (var brokenComponent in brokenComponents) {
                    blocked.Add($"{brokenComponent.owner().name} in {path} ({brokenComponent.GetBrokenMessage()})");
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
            if (string.IsNullOrWhiteSpace(path)
                || !File.Exists(path)
                || !Path.GetFullPath(path).StartsWith(Path.GetFullPath("Packages"))
            ) {
                return false; 
            }

            return path.StartsWith("Packages/com.vrcfury.vrcfury/")
                   || path.StartsWith("Packages/com.vrcfury.prefabs/");
        }
    }
}
