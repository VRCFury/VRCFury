using System.Reflection;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;

namespace VF.Feature {
    /**
     * Automatically runs the d4k3 optimizer on an avatar if its configuration component is present
     */
    public class D4rkOptimizerBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.D4rkOptimizer)]
        public void Apply() {
            var componentType = ReflectionUtils.GetTypeFromAnyAssembly("d4rkAvatarOptimizer");
            if (componentType == null) {
                Debug.LogWarning("d4rk optimizer not installed");
                return;
            }
            var component = avatarObject.GetComponent(componentType);
            if (component == null) {
                Debug.LogWarning("d4rk optimizer not present on avatar");
                return;
            }
            var editorType = ReflectionUtils.GetTypeFromAnyAssembly("d4rkAvatarOptimizerEditor");
            if (editorType == null) {
                throw new VRCFBuilderException("Failed to find d4rk optimizer editor script");
            }

            var settingsField = editorType.GetField("settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (settingsField == null) {
                throw new VRCFBuilderException("Failed to find settings field on editor");
            }
            settingsField.SetValue(null, component);
            
            var optimizeMethod = editorType.GetMethod("Optimize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (optimizeMethod == null) {
                throw new VRCFBuilderException("Failed to find optimize method");
            }

            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                VRCFuryAssetDatabase.WithStandardizedLocale(() => {
                    optimizeMethod.Invoke(null, new object[] { avatarObject });
                });
            });
        }
    }
}
