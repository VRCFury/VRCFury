using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Builder;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    public class PhysboneResetServiceTest {
        [TestCase(true)]
        [TestCase(false)]
        public void ResetsPhysBonesInTheHierarchyAndPreservesTheirDefaults(bool initiallyEnabled) {
            var root = new GameObject("Avatar");
            var graph = default(PlayableGraph);
            var assets = new List<Object>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);

                var obj = new GameObject("PhysBone");
                obj.transform.SetParent(root.transform, false);
                var child = new GameObject("Child");
                child.transform.SetParent(obj.transform, false);
                var physBone = obj.AddComponent<VRCPhysBone>();
                physBone.enabled = initiallyEnabled;
                var childBone = child.AddComponent<VRCPhysBone>();
                var otherObject = new GameObject("Outside reset hierarchy");
                otherObject.transform.SetParent(root.transform, false);
                var other = otherObject.AddComponent<VRCPhysBone>();
                var collider = obj.AddComponent<BoxCollider>();
                var trigger = injector.GetService<PhysboneResetService>().CreatePhysBoneResetter(obj, "Reset");
                var bindings = fx.GetClips().SelectMany(c => c.GetFloatBindings()).ToArray();
                Assert.That(bindings.Any(b => b.target == obj && b.type == typeof(VRCPhysBone)
                    && b.propertyName == "m_Enabled"), Is.True);
                Assert.That(bindings.Any(b => b.target == child && b.type == typeof(VRCPhysBone)
                    && b.propertyName == "m_Enabled"), Is.True);
                Assert.That(bindings.Any(b => b.type == typeof(GameObject) && b.propertyName == "m_IsActive"), Is.False);

                injector.GetService<NoBadControllerParamsService>().Apply();
                injector.GetService<FixPartiallyWeightedAapsService>().Apply();
                var context = new VFSaveContext(root, false);
                var controller = new AnimatorController { parameters = fx.parameters.Select(p => p.Save()).ToArray() };
                controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
                assets.Add(controller);
                assets.AddRange(context.NewAssets);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("PhysBone reset");
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "FX", animator).SetSourcePlayable(playable);
                graph.Play();

                for (var i = 0; i < 8; i++) graph.Evaluate(1f / 60);
                Assert.That(physBone.enabled, Is.EqualTo(initiallyEnabled));
                foreach (var value in new[] { 1f, 0f }) {
                    playable.SetFloat(trigger, value);
                    var disabled = false;
                    var childDisabled = false;
                    for (var i = 0; i < 8; i++) {
                        graph.Evaluate(1f / 60);
                        disabled |= !physBone.enabled;
                        childDisabled |= !childBone.enabled;
                        Assert.That(obj.activeInHierarchy && child.activeInHierarchy, Is.True);
                        Assert.That(other.enabled, Is.True, "PhysBones outside the hierarchy must not be reset");
                        Assert.That(collider.enabled, Is.True, "Other component types must not be disabled");
                    }
                    Assert.That(disabled, Is.True, "Both trigger edges must produce a reset");
                    Assert.That(childDisabled, Is.True, "Child PhysBones must also reset");
                    Assert.That(physBone.enabled, Is.EqualTo(initiallyEnabled), "Restore the original component state");
                    Assert.That(childBone.enabled, Is.True, "Restore the child's original component state");
                }
            } finally {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(root);
                foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
    }
}
