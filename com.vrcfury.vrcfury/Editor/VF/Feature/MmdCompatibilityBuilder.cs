using System.Collections.Immutable;
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
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class MmdCompatibilityBuilder : FeatureBuilder<MmdCompatibility> {
        [VFAutowired] private readonly MathService mathService;
        [VFAutowired] private readonly AnimatorLayerControlOffsetBuilder layerControlBuilder;
        
        public override string GetEditorTitle() {
            return "MMD Compatibility";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Info(
                "This component will improve MMD compatibility for your avatar, by maintaining MMD" +
                " blendshapes, avoiding usage of layers that MMD worlds are known to interfere with, and disabling" +
                " hand animations when MMD dances are active."));

            var adv = new Foldout() {
                text = "Advanced Settings",
                value = false
            };
            adv.Add(VRCFuryEditorUtils.WrappedLabel("When MMD is detected, disable layers with these names:"));
            adv.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("disableLayers")));
            adv.Add(VRCFuryEditorUtils.WrappedLabel("When MMD is detected, set this global bool to true:"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("globalParam")));
            c.Add(adv);

            return c;
        }

        [CustomPropertyDrawer(typeof(MmdCompatibility.DisableLayer))]
        public class DisableLayerDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty property) {
                return VRCFuryEditorUtils.Prop(property.FindPropertyRelative("name"));
            }
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
            layer1.weight = 0;
            var layer2 = fx.NewLayer("MMD Dummy Layer 2", 2);
            layer2.weight = 0;

            var layerNamesToDisable = model.disableLayers
                .Select(l => l.name)
                .ToImmutableHashSet();
            var layersToDisable = fx.GetLayers()
                .Where(l => layerNamesToDisable.Contains(l.name))
                .Select(l => l.stateMachine)
                .ToArray();
            
            if (layersToDisable.Length == 0 && string.IsNullOrWhiteSpace(model.globalParam)) {
                return;
            }

            var mmdDetector = fx.NewFloat("MMDDetector", def: 1);
            var mmdDetectorClip = VrcfObjectFactory.Create<AnimationClip>();
            // MMD worlds will disable this layer, setting HandsActive back to the default of 0
            mmdDetectorClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), mmdDetector.Name()), 0);
            layer1.NewState("Mmd Detector").WithAnimation(mmdDetectorClip);
            layer1.weight = 1;

            var notDetected = layer2.NewState("MMD Not Detected");
            var detected = layer2.NewState("MMD Detected");
            notDetected.TransitionsTo(detected).When(mmdDetector.IsGreaterThan(0));
            detected.TransitionsTo(notDetected).When(mmdDetector.IsGreaterThan(0).Not());

            if (layersToDisable.Length > 0) {
                foreach (var l in layersToDisable) {
                    var driveOff = detected.GetRaw().VAddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    layerControlBuilder.Register(driveOff, l);
                    driveOff.goalWeight = 0;
                    var driveOn = notDetected.GetRaw().VAddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    layerControlBuilder.Register(driveOn, l);
                    driveOn.goalWeight = 1;
                }
            }

            if (!string.IsNullOrWhiteSpace(model.globalParam)) {
                detected.Drives(model.globalParam, 1);
                notDetected.Drives(model.globalParam, 0);
            }
        }
    }
}
