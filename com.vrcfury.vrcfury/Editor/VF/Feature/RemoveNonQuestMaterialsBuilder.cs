using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Feature {
    [VFService]
    public class RemoveNonQuestMaterialsBuilder {
        [VFAutowired] private AvatarManager avatarManager;
        
        [FeatureBuilderAction(FeatureOrder.RemoveNonQuestMaterials)]
        public void Apply() {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
                return;
            }

            foreach (var ctrl in avatarManager.GetAllUsedControllers()) {
                foreach (var clip in ctrl.GetClips()) {
                    foreach (var binding in clip.GetObjectBindings()) {
                        var changed = false;
                        var curve = clip.GetObjectCurve(binding).Select(
                            key => {
                                if (key.value is Material m && !IsMobileMat(m)) {
                                    changed = true;
                                    key.value = null;
                                }
                                return key;
                            }).ToArray();
                        if (changed) {
                            clip.SetObjectCurve(binding, curve);
                        }
                    }
                }
            }

            var keptMats = 0;

            foreach (var renderer in avatarManager.AvatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                    if (!IsMobileMat(m)) return null;
                    keptMats++;
                    return m;
                }).ToArray();
            }

            foreach (var light in avatarManager.AvatarObject.GetComponentsInSelfAndChildren<Light>()) {
                Object.DestroyImmediate(light);
            }

            if (keptMats == 0) {
                EditorUtility.DisplayDialog("No Valid Android Materials", 
                                            "You are currently building for Android and are not using any compatible shaders. " + 
                                            "You have likely switched to Android mode by mistake and simply need to switch back to Windows mode using the VRChat SDK Control Panel. " + 
                                            "If you have not switched by mistake and want to build for Android, you will need to change your materials to use shaders found in VRChat/Mobile.", 
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
