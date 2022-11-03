using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Model;

namespace VF {
    public class PreSaveVerifier : UnityEditor.AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths) {
            var blocked = new List<string>();
            foreach (var path in paths) {
                var vrcfuryAssets = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Select(asset => asset as VRCFuryComponent)
                    .Where(asset => asset != null);
                foreach (var asset in vrcfuryAssets) {
                    if (asset.IsBroken()) {
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
