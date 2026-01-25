using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class RemoveNonQuestMaterialsService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        
        [FeatureBuilderAction(FeatureOrder.RemoveNonQuestMaterials)]
        public void Apply() {
            if (BuildTargetUtils.IsDesktop()) {
                return;
            }

            var removedMats = new HashSet<string>();
            var removedFromActiveRootRenderer = false;

            foreach (var ctrl in controllers.GetAllUsedControllers()) {
                foreach (var clip in ctrl.GetClips()) {
                    clip.Rewrite(AnimationRewriter.RewriteObject(obj => {
                        if (obj is Material m && !IsMobileMat(m)) {
                            removedMats.Add($"{m.name} in animation {clip.name}");
                        }
                        return obj;
                    }));
                }
            }

            foreach (var renderer in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                foreach(var m in renderer.sharedMaterials) {
                    if (!IsMobileMat(m)) {
                        removedMats.Add($"{m.name} in {renderer.owner().GetPath(avatarObject, true)}");
                        if (renderer.owner().active && renderer.owner().parent == avatarObject) {
                            removedFromActiveRootRenderer = true;
                        }
                    }
                }
            }

            foreach (var light in avatarObject.GetComponentsInSelfAndChildren<Light>()) {
                Object.DestroyImmediate(light);
            }
            
            if (removedFromActiveRootRenderer) {
                var sorted = removedMats.OrderBy(a => a.Length).Take(10).ToArray();
                var more = removedMats.Count - sorted.Length;
                var moreText = more > 0 ? $"\n... and {more} more" : "";
                EditorUtility.DisplayDialog("Invalid Mobile Materials", 
                                            "You are currently building an avatar for Android/Quest/iOS and are using shaders that are not mobile compatible. " + 
                                            "You have likely switched build target by mistake and simply need to switch back to Windows mode using the VRChat SDK Control Panel. " + 
                                            "If you have not switched by mistake and want to build for mobile, you will need to change your materials to use shaders found in VRChat/Mobile. " +
                                            "You can ignore this message if other tools will process your materials for mobile.\n" +
                                            "\n" +
                                            sorted.Join('\n') + moreText, 
                                            "OK");
            }
        }

        private bool IsMobileMat(Material m) {
            if (m == null) return true;
            if (m.shader == null) return true;
            return m.shader.name.StartsWith("VRChat/Mobile/");
        }
    }
}
