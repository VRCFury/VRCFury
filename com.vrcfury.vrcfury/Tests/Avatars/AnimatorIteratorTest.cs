using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;
using VF.Utils.Controller;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class AnimatorIteratorTest {
        [Test]
        public void SharedMotionsAndAdditiveReferencePosesAreIncludedAtEveryScope() {
            var rawClip = new AnimationClip();
            var rawPose = new AnimationClip();
            var root = new GameObject("Avatar");
            try {
                var settings = AnimationUtility.GetAnimationClipSettings(rawClip);
                settings.additiveReferencePoseClip = rawPose;
                AnimationUtility.SetAnimationClipSettings(rawClip, settings);
                var clip = VFClip.Load(rawClip, new VFLoadContext { OwnerObject = root, AnimatorObject = root });
                var pose = clip.GetAdditiveReferencePoseClip();
                Assert.That(pose, Is.Not.Null);
                var other = VFClip.Create("Other");
                var shared = VFTree.Create("Shared", BlendTreeType.Simple1D);
                shared.AddChild(new VFTreeChild { motion = clip });
                shared.AddChild(new VFTreeChild { motion = other });
                var tree = VFTree.Create("Root", BlendTreeType.Direct);
                tree.AddChild(new VFTreeChild { motion = shared });
                tree.AddChild(new VFTreeChild { motion = shared });
                tree.AddChild(new VFTreeChild { motion = null });
                var controller = VFController.Create();
                var layer = controller.NewLayer("First");
                var state = layer.NewState("First");
                state.motion = tree;
                layer.NewState("Second").motion = shared;
                layer.NewState("Empty");
                controller.NewLayer("Other layer").NewState("Shared").motion = tree;
                controller.NewLayer("Empty layer");

                void Check<T>(AnimatorIterator.Iterator<T> iterator, T[] expected) {
                    Assert.That(iterator.From(tree), Is.EquivalentTo(expected));
                    Assert.That(iterator.From(state), Is.EquivalentTo(expected));
                    Assert.That(iterator.From(layer), Is.EquivalentTo(expected));
                    Assert.That(iterator.From(new[] { layer, layer }), Is.EquivalentTo(expected));
                    Assert.That(iterator.From(controller), Is.EquivalentTo(expected));
                }
                Check(new AnimatorIterator.Clips(), new[] { clip, pose, other });
                Check(new AnimatorIterator.Trees(), new[] { tree, shared });
                Check(new AnimatorIterator.Motions(), new VFMotion[] { tree, shared, clip, pose, other });
                Assert.That(new AnimatorIterator.States().From(layer), Has.Count.EqualTo(3));
                Assert.That(new AnimatorIterator.States().From(controller), Has.Count.EqualTo(4));
            } finally {
                Object.DestroyImmediate(rawClip);
                Object.DestroyImmediate(rawPose);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LeafQueriesAndSubsequentGraphChangesPreserveSnapshotResults() {
            var clip = VFClip.Create();
            Assert.That(new AnimatorIterator.Clips().From(clip), Is.EquivalentTo(new[] { clip }));
            Assert.That(new AnimatorIterator.Motions().From(clip), Is.EquivalentTo(new[] { clip }));
            Assert.That(new AnimatorIterator.Trees().From(clip), Is.Empty);
            var tree = VFTree.Create("Root", BlendTreeType.Direct);
            tree.AddChild(new VFTreeChild { motion = clip });
            var iterator = new AnimatorIterator.Clips();
            var snapshot = iterator.From(tree);
            var later = VFClip.Create();
            tree.AddChild(new VFTreeChild { motion = later });
            Assert.That(snapshot, Is.EquivalentTo(new[] { clip }));
            Assert.That(iterator.From(tree), Is.EquivalentTo(new[] { clip, later }));
        }

        [Test]
        public void EmptyScopesReturnEmptySets() {
            void Check<T>(AnimatorIterator.Iterator<T> iterator) {
                Assert.That(iterator.From((VFMotion)null), Is.Empty);
                Assert.That(iterator.From((VFState)null), Is.Empty);
                Assert.That(iterator.From((VFController)null), Is.Empty);
                Assert.That(iterator.From((IEnumerable<VFLayer>)null), Is.Empty);
                Assert.That(iterator.From(VFController.Create().NewLayer("Empty")), Is.Empty);
            }
            Check(new AnimatorIterator.Clips());
            Check(new AnimatorIterator.Trees());
            Check(new AnimatorIterator.Motions());
        }
    }
}
