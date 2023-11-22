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
                " blendshapes, avoiding usage of layers that MMD worlds are known to interfere with, and disabling" +
                " hand animations when MMD dances are active."));
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
            
            // Ensure layer 1 and 2 are empty, since MMD worlds like to turn them off (but only sometimes)
            var layer1 = fx.NewLayer("MMD Dummy Layer 1", 1);
            var layer2 = fx.NewLayer("MMD Dummy Layer 2", 2);

            var handsActive = fx.NewFloat("HandsActive", def: 0);
            var handsActiveClip = new AnimationClip();
            // MMD worlds will disable this layer, setting HandsActive back to the default of 0
            handsActiveClip.SetConstant(EditorCurveBinding.FloatCurve("", typeof(Animator), handsActive.Name()), 1);
            layer1.NewState("Mmd Detector").WithAnimation(handsActiveClip);

            foreach (var state in new AnimatorIterator.States().From(fx.GetRaw())) {
                if (new AnimatorIterator.Clips().From(state)
                    .Any(clip => clip.HasMuscles())) {
                    var tree = mathService.MakeDirect("WhenHandsActive");
                    tree.Add(handsActive, state.motion);
                    state.motion = tree;
                    state.writeDefaultValues = true;
                }
            }
        }
    }
}
