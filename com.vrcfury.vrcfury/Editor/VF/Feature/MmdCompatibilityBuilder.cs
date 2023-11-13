using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    public class MmdCompatibilityBuilder : FeatureBuilder<MmdCompatibility> {
        public override string GetEditorTitle() {
            return "MMD Compatibility";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This component will improve MMD compatibility for your avatar, by maintaining MMD" +
                " blendshapes, and avoiding usage of layers that MMD worlds are known to interfere with.");
        }

        public override bool OnlyOneAllowed() {
            return true;
        }

        public override bool AvailableOnProps() {
            return false;
        }

        [FeatureBuilderAction(FeatureOrder.AvoidMmdLayers)]
        public void Apply() {
            var fx = GetFx();
            if (fx.GetLayers().Count() <= 1) {
                return;
            }

            // Ensure layer 1 and 2 are empty, since MMD worlds like to turn them off (but only sometimes)
            fx.NewLayer("MMD Dummy Layer 1", 1);
            fx.NewLayer("MMD Dummy Layer 2", 2);

            // Change any states using proxy_hands_idle to empty, so that when users aren't doing a hand gesture,
            // it will fall through to the dance's hand gestures.
            foreach (var layer in fx.GetLayers()) {
                foreach (var state in new AnimatorIterator.States().From(layer)) {
                    if (!(state.motion is AnimationClip ac)) continue;
                    var hasIdle = ac.CollapseProxyBindings()
                        .Select(p => p.Item1)
                        .Any(proxy => AssetDatabase.GetAssetPath(proxy).Contains("proxy_hands_idle"));
                    if (hasIdle) {
                        state.motion = null;
                    }
                }
            }
        }
    }
}
