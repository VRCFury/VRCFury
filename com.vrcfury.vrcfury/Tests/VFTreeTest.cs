using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Tests {
    internal class VFTreeTest {
        [Test]
        public void SamplingKeepsSharedTreesAndSeparatesClipOccurrences() {
            var root = VFTree.Create("Root", BlendTreeType.Direct);
            var subtree = VFTree.Create("Subtree", BlendTreeType.Direct);
            var clip = VFClip.Create("Clip");
            var binding = VFBinding.MakeAnimatorBinding("Value");
            clip.SetCurve(binding, AnimationCurve.Linear(0, 0, 1, 8));
            root.AddChild(new VFTreeChild { motion = subtree });
            root.AddChild(new VFTreeChild { motion = subtree });
            subtree.AddChild(new VFTreeChild { motion = clip });
            subtree.AddChild(new VFTreeChild { motion = clip });
            subtree.AddChild(new VFTreeChild { motion = root });

            var sampled = (VFTree)root.EvaluateMotion(0.25f);
            Assert.That(sampled, Is.Not.SameAs(root));
            var sampledSubtree = (VFTree)sampled.children[0].motion;
            Assert.That(sampled.children[1].motion, Is.SameAs(sampledSubtree));
            Assert.That(sampledSubtree.children[2].motion, Is.SameAs(sampled));
            var first = (VFClip)sampledSubtree.children[0].motion;
            var second = (VFClip)sampledSubtree.children[1].motion;
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.GetFloatCurve(binding).Evaluate(0), Is.EqualTo(2));
            Assert.That(second.GetFloatCurve(binding).Evaluate(0), Is.EqualTo(2));
            first.SetCurve(binding, 5);
            Assert.That(second.GetFloatCurve(binding).Evaluate(0), Is.EqualTo(2));
            Assert.That(clip.GetFloatCurve(binding).Evaluate(1), Is.EqualTo(8));
        }
    }
}
