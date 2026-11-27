using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Actions;
using VF.Builder;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class AnimationClipActionBuilderTest {
        [Test]
        public void SeparatesBothHandEmotesWithoutCopyingFxTriggersIntoThem() {
            var root = new GameObject("Avatar");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX },
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Gesture }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);

                var source = VFClip.Create("Hands");
                source.SetAap("LeftHand.Index.1 Stretched", 0.3f);
                source.SetAap("RightHand.Index.1 Stretched", 0.8f);
                source.SetEnabled(root, true);
                var bindings = source.GetAllBindings();
                Assert.That(source.GetMuscleBindingTypes(), Does.Contain(VFBinding.MuscleBindingType.LeftHand));
                Assert.That(source.GetMuscleBindingTypes(), Does.Contain(VFBinding.MuscleBindingType.RightHand));

                var builder = injector.CreateAndFillObject<AnimationClipActionBuilder>();
                var fx = (VFClip)builder.Build(new AnimationClipAction { vfClip = source }, root);
                var gesture = controllers.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);
                var emotes = gesture.GetClips().ToArray();
                Assert.That(emotes, Has.Length.EqualTo(2));
                foreach (var emote in emotes) Assert.That(emote.GetAllBindings(), Is.EquivalentTo(bindings));
                Assert.That(fx.GetAllBindings().Count(b => b.GetPropType() == EditorCurveBindingType.Aap), Is.EqualTo(2));
                Assert.That(fx.GetAllBindings().Any(b => b.GetPropType() == EditorCurveBindingType.Muscle), Is.False);
                Assert.That(source.GetAllBindings(), Is.EquivalentTo(bindings));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
