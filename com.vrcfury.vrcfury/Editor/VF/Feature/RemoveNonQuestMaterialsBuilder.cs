using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Feature {
    [VFService]
    internal class RemoveNonQuestMaterialsBuilder {
        [VFAutowired] private AvatarManager avatarManager;
        
        [FeatureBuilderAction(FeatureOrder.RemoveNonQuestMaterials)]
        public void Apply() {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
                return;
            }

            var removedMats = new HashSet<string>();
            var removedFromActiveRootRenderer = false;

            foreach (var ctrl in avatarManager.GetAllUsedControllers()) {
                foreach (var clip in ctrl.GetClips()) {
                    clip.Rewrite(AnimationRewriter.RewriteObject(obj => {
                        if (obj is Material m && !IsMobileMat(m)) {
                            removedMats.Add($"{m.name} in animation {clip.name}");
                            return null;
                        }
                        return obj;
                    }));
                }
            }

            foreach (var renderer in avatarManager.AvatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                    if (!IsMobileMat(m)) {
                        removedMats.Add($"{m.name} in {renderer.owner().GetPath(avatarManager.AvatarObject, true)}");
                        if (renderer.owner().active && renderer.owner().parent == avatarManager.AvatarObject) {
                            removedFromActiveRootRenderer = true;
                        }
                        return null;
                    }
                    return m;
                }).ToArray();
            }

            foreach (var light in avatarManager.AvatarObject.GetComponentsInSelfAndChildren<Light>()) {
                Object.DestroyImmediate(light);
            }
            
            if (removedFromActiveRootRenderer) {
                var sorted = removedMats.OrderBy(a => a.Length).Take(10).ToArray();
                var more = removedMats.Count - sorted.Length;
                var moreText = more > 0 ? $"\n... and {more} more" : "";
                EditorUtility.DisplayDialog("Invalid Android Materials", 
                                            "You are currently building an avatar for Android and are using shaders that are not mobile compatible. " + 
                                            "You have likely switched to Android mode by mistake and simply need to switch back to Windows mode using the VRChat SDK Control Panel. " + 
                                            "If you have not switched by mistake and want to build for Android, you will need to change your materials to use shaders found in VRChat/Mobile.\n" +
                                            "\n" +
                                            string.Join("\n", sorted) + moreText, 
                                            "OK" );
            }
        }

        private bool IsMobileMat(Material m) {
            if (m == null) return true;
            if (m.shader == null) return true;
            return m.shader.name.StartsWith("VRChat/Mobile/");
        }
    }
}
