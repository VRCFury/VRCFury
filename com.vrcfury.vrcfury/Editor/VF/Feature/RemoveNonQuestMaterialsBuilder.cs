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
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
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

            foreach (var renderer in avatarManager.AvatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                    if (!IsMobileMat(m)) return null;
                    return m;
                }).ToArray();
            }
        }

        private bool IsMobileMat(Material m) {
            if (m == null) return true;
            if (m.shader == null) return true;
            return m.shader.name.StartsWith("VRChat/Mobile/");
        }
    }
}
