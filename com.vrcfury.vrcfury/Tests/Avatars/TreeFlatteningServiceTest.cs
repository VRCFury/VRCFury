using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Tests {
    internal class TreeFlatteningServiceTest {
        [Test]
        public void MergeKeepsOverlappingClipsSeparateAndCombinesDisjointBindings() {
            var root = new GameObject("Tree merge avatar");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = System.Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.LoadAllControllers();
                var fx = controllers.GetFx();
                var shared = fx.GetOrCreateFloat("Shared");
                var separate = fx.GetOrCreateFloat("Separate");
                var first = VFClip.Create("First");
                var overlapping = VFClip.Create("Overlapping");
                var disjoint = VFClip.Create("Disjoint");
                first.SetAap(shared, 2);
                overlapping.SetAap(shared, 3);
                disjoint.SetAap(separate, 4);

                var tree = VFBlendTreeDirect.Create("Merge clips");
                tree.Add(first);
                tree.Add(overlapping);
                tree.Add(disjoint);
                var state = fx.NewLayer("Merge clips").NewState("Merge clips").WithAnimation(tree);

                injector.GetService<TreeFlatteningService>().Optimize();

                var children = ((VFTree)state.motion).children;
                Assert.That(children, Has.Count.EqualTo(2));
                Assert.That(children[1].motion, Is.SameAs(overlapping));
                var merged = (VFClip)children[0].motion;
                Assert.That(merged.name, Is.EqualTo("First + Disjoint"));
                Assert.That(merged.GetAllBindings().Select(binding => binding.propertyName), Is.EquivalentTo(new[] {
                    shared.name,
                    separate.name
                }));
                Assert.That(merged.GetFloatCurve(VFBinding.MakeAnimatorBinding(shared.name)).Evaluate(0), Is.EqualTo(2));
                Assert.That(merged.GetFloatCurve(VFBinding.MakeAnimatorBinding(separate.name)).Evaluate(0), Is.EqualTo(4));
                Assert.That(first.GetAllBindings(), Has.Length.EqualTo(1));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
