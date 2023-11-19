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
                                if (!(key.value is Material m) || IsMobileMat(m)) return key;
                                changed = true;
                                key.value = null;
                                return key;
                            }).ToArray();
                        if (changed) {
                            clip.SetObjectCurve(binding, curve);
                        }
                    }
                }
            }

            foreach (var renderer in avatarManager.AvatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => !IsMobileMat(m) ? null : m).ToArray();
            }

            foreach (var light in avatarManager.AvatarObject.GetComponentsInSelfAndChildren<Light>()) {
                Object.DestroyImmediate(light);
            }
        }

        private bool IsMobileMat(Material m) {
            if (m == null) return true;
            return m.shader == null || m.shader.name.StartsWith("VRChat/Mobile/");
        }
    }
}
