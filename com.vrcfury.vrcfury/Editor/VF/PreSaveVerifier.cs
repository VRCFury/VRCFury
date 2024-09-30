using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Component;
using VF.Utils;

namespace VF {
    internal class PreSaveVerifier : UnityEditor.AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths) {
            var blocked = new List<string>();
            var blockedPaths = new HashSet<string>();
            
            foreach (var path in paths) {
                var brokenComponents = new HashSet<VRCFuryComponent>();
                
                void ScanScene(Scene scene) {
                    brokenComponents.UnionWith(VFGameObject.GetRoots(scene)
                        .SelectMany(obj => obj.GetComponentsInSelfAndChildren<VRCFuryComponent>())
                        .Where(vrcf => vrcf.IsBroken()));
                }

                if (typeof(SceneAsset) == AssetDatabase.GetMainAssetTypeAtPath(path)) {
                    for (var n = 0; n < SceneManager.sceneCount; ++n) {
                        var scene = SceneManager.GetSceneAt(n);
                        if (scene.path == path) {
                            ScanScene(scene);
                        }
                    }
#if UNITY_2022_1_OR_NEWER
                } else if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == path) {
                    var scene = PrefabStageUtility.GetCurrentPrefabStage()?.scene;
                    if (scene != null) ScanScene(scene.Value);
#endif
                } else {
                    brokenComponents.UnionWith(AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<VRCFuryComponent>()
                        .Where(vrcf => vrcf.IsBroken()));
                }

                foreach (var brokenComponent in brokenComponents) {
                    blocked.Add($"{brokenComponent.owner().GetPath()} in {path} ({brokenComponent.GetBrokenMessage()})");
                    blockedPaths.Add(path);
                }
            }

            var extras = 0;
            while (blocked.Count > 5) {
                blocked.RemoveAt(blocked.Count - 1);
                extras++;
            }
            if (extras > 0) {
                blocked.Add($"... and {extras} more");
            }

            if (blocked.Count > 0) {
                DialogUtils.DisplayDialog("VRCFury Blocked Saving",
                    "VRCFury blocked these assets from saving to prevent unity from overwriting them with corrupt data:\n\n" + blocked.Join("\n\n"),
                    "Ok");
                paths = paths.ToList().Where(e => !blockedPaths.Contains(e)).ToArray();
            }
            
            return paths;
        }
    }
}
