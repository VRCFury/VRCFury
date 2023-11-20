using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    public class MmdCompatibilityBuilder : FeatureBuilder<MmdCompatibility> {
        [VFAutowired] private readonly MathService mathService;
        
        public override string GetEditorTitle() {
            return "MMD Compatibility";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Info(
                "This component will improve MMD compatibility for your avatar, by maintaining MMD" +
                " blendshapes, and avoiding usage of layers that MMD worlds are known to interfere with."));
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("useNewHandDisabler"), "Use new hand disable logic (beta)"));
            return c;
        }

        public override bool OnlyOneAllowed() {
            return true;
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }

        [FeatureBuilderAction(FeatureOrder.AvoidMmdLayers)]
        public void Apply() {
            var fx = GetFx();
            if (fx.GetLayers().Count() <= 1) {
                return;
            }

            if (model.useNewHandDisabler) {
                var handsActive = fx.NewFloat("HandsActive", def: 1);
                {
                    var handsActiveLayer = fx.NewLayer("MMD Hands Deactivator");
                    var active = handsActiveLayer.NewState("Active").Drives(handsActive, 1);
                    var inactive = handsActiveLayer.NewState("Inactive").Drives(handsActive, 0);
                    var inactiveWhen = fx.NewBool("Seated", usePrefix: false).IsFalse()
                        .And(fx.NewBool("InStation", usePrefix: false).IsTrue());
                    active.TransitionsTo(inactive).When(inactiveWhen);
                    inactive.TransitionsTo(active).When(inactiveWhen.Not());
                }
                foreach (var state in new AnimatorIterator.States().From(fx.GetRaw())) {
                    if (new AnimatorIterator.Clips().From(state)
                        .SelectMany(clip => clip.GetFloatBindings())
                        .Any(b => b.IsMuscle() || b.IsProxyBinding())) {
                        var tree = mathService.MakeDirect("WhenHandsActive");
                        tree.Add(handsActive, state.motion);
                        state.motion = tree;
                        state.writeDefaultValues = true;
                    }
                }
            } else {
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

            // Ensure layer 1 and 2 are empty, since MMD worlds like to turn them off (but only sometimes)
            fx.NewLayer("MMD Dummy Layer 1", 1);
            fx.NewLayer("MMD Dummy Layer 2", 2);
        }
    }
}
