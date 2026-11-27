using NUnit.Framework;
using UnityEngine;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Tests {
    internal class AnimatorLayerControlOffsetServiceTest {
        [TestCase(VRC_AnimatorLayerControl.BlendableLayer.FX, 0)]
        [TestCase(VRC_AnimatorLayerControl.BlendableLayer.Action, 0)]
        [TestCase(VRC_AnimatorLayerControl.BlendableLayer.FX, -1)]
        [TestCase(VRC_AnimatorLayerControl.BlendableLayer.FX, 1)]
        public void RegisterControllerSetPreservesLayerTargets(
            VRC_AnimatorLayerControl.BlendableLayer playable,
            int index
        ) {
            var otherFx = VFController.Create();
            var otherFxLayer = otherFx.NewLayer("Other FX");
            var source = VFController.Create();
            var sourceLayer = source.NewLayer("Source FX");
            var action = VFController.Create();
            var actionLayer = action.NewLayer("Action");
            var duplicateAction = VFController.Create();
            var duplicateActionLayer = duplicateAction.NewLayer("Other Action");
            var state = sourceLayer.NewState("Control layer");

            var control = ScriptableObject.CreateInstance<VRCAnimatorLayerControl>();
            control.playable = playable;
            control.layer = index;
            var behaviour = new VFBehaviour(control);
            state.behaviours.Add(behaviour);
            try {
                var service = new AnimatorLayerControlOffsetService();
                service.RegisterControllerSet(new[] {
                    (otherFx, VRCAvatarDescriptor.AnimLayerType.FX),
                    (source, VRCAvatarDescriptor.AnimLayerType.FX),
                    (action, VRCAvatarDescriptor.AnimLayerType.Action),
                    (duplicateAction, VRCAvatarDescriptor.AnimLayerType.Action)
                });

                Assert.That(service.IsLayerTargeted(otherFxLayer), Is.False);
                Assert.That(service.IsLayerTargeted(duplicateActionLayer), Is.False);
                Assert.That(service.IsLayerTargeted(sourceLayer), Is.EqualTo(index == 0 && playable == VRC_AnimatorLayerControl.BlendableLayer.FX));
                Assert.That(service.IsLayerTargeted(actionLayer), Is.EqualTo(index == 0 && playable == VRC_AnimatorLayerControl.BlendableLayer.Action));
                Assert.That(state.behaviours.Contains(behaviour), Is.EqualTo(index == 0));
            } finally {
                Object.DestroyImmediate(behaviour.Read<VRCAnimatorLayerControl>());
                Object.DestroyImmediate(control);
            }
        }
    }
}
