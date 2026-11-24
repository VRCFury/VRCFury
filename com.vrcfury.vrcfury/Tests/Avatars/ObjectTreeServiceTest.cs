#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
namespace VF.Tests {
    using static VF.Inspector.VRCFuryHapticPlugEditor;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class ObjectTreeServiceTest : SpsAvatarBuildTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void ObjectTreeCapturesLateDefaultsWithoutAnimatingSdkInputs(bool optimize) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            optimizeTrees = optimize;
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel>();
            var owner = GameObjects.Create("Owner", root);
            owner.localScale = Vector3.one * 2;
            var enabled = fx.CreateUniqueFloat("Owner enabled", 1);
            enabled.MakeExpressionFloat(synced: false);
            var objectTrees = injector.GetService<ObjectTreeService>();
            var calculations = objectTrees.Get(owner);
            var on = VFClip.Create("Owner on"); on.SetEnabled(owner, true);
            var off = VFClip.Create("Owner off"); off.SetEnabled(owner, false);
            fx.NewLayer("Owner toggle").NewState("Owner toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Owner toggle", enabled, (0, off), (1, on)));
            Assert.That(objectTrees.Get(owner), Is.SameAs(calculations));
            var output = fx.CreateUniqueFloat("Helper output", 4);
            calculations.Add(output.MakeSetter(9));
            var detector = injector.GetService<WorldScaleDetectorService>();
            var raw = detector.GetRawWorldScale(owner, "Owner");
            var inverse = detector.GetInverseWorldScale(owner, "Owner");
            // Defaults are captured after helpers have contributed and resting values have changed.
            output.SetDefault(5);
            Start();
            playable.SetFloat(raw, 0.02f);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.EqualTo(9));
            Assert.That(playable.GetFloat(inverse), Is.EqualTo(0.25f).Within(0.001f));
            playable.SetFloat(enabled, 0);
            Evaluate(4);
            Assert.That(playable.GetFloat(output), Is.EqualTo(5));
            Assert.That(playable.GetFloat(inverse), Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(playable.IsParameterControlledByCurve(raw), Is.False);
        }

        [Test]
        public void ObjectTreeDefaultsUseRecordedMaterialValuesAndObjectReferences() {
            var renderer = GameObjects.Create("Default material", root).AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Unlit/Color"));
            var replacement = new Material(material);
            assets.Add(material);
            assets.Add(replacement);
            renderer.sharedMaterial = material;
            const string property = "material._VRCFuryRecordedDefault";
            var resting = VFClip.Create("Recorded material default");
            resting.SetCurve(renderer, property, 0.4f);
            injector.GetService<AvatarBindingStateService>().ApplyClip(resting, "Test resting state");
            var active = VFClip.Create("Active material");
            active.SetCurve(renderer, property, 0.9f);
            active.SetCurve(renderer, "m_Materials.Array.data[0]", replacement);
            var objectTrees = injector.GetService<ObjectTreeService>();
            var calculations = objectTrees.Get(renderer.owner());
            calculations.Add(active);
            objectTrees.Apply();
            var disabled = new AnimatorIterator.Clips().From(fx).Single(c => c.name == "Default material - Disabled");
            var floatBinding = active.GetFloatBindings().Single();
            Assert.That(disabled.GetCurve(floatBinding, true).GetFirst().GetFloat(), Is.EqualTo(0.4f));
            var objectBinding = active.GetObjectBindings().Single();
            Assert.That(disabled.GetCurve(objectBinding, false).GetFirst().GetObject(), Is.SameAs(material));
        }
    }
}
#endif
