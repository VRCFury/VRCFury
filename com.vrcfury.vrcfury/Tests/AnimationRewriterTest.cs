using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    internal class AnimationRewriterTest {
        [Test]
        public void ObjectRewriterKeepsUnchangedCurvesAndCopiesChangedFrames() {
            var first = new Texture2D(1, 1);
            var second = new Texture2D(1, 1);
            try {
                var binding = VFBinding.From(null, EditorCurveBinding.PPtrCurve("", typeof(Renderer), "m_Materials.Array.data[0]"));
                FloatOrObjectCurve curve = new[] {
                    new ObjectReferenceKeyframe { time = 0, value = first },
                    new ObjectReferenceKeyframe { time = 0.5f, value = null },
                    new ObjectReferenceKeyframe { time = 1, value = first }
                };

                var (_, unchanged, reportedChange) = AnimationRewriter.RewriteObject(value => value)
                    .RewriteOneForLoaded(binding, curve);
                Assert.That(unchanged, Is.SameAs(curve));
                Assert.That(reportedChange, Is.False);

                var (_, modified, reportedReplacement) = AnimationRewriter.RewriteObject(_ => second)
                    .RewriteOneForLoaded(binding, curve);
                Assert.That(reportedReplacement, Is.True);
                Assert.That(modified.ObjectCurve[0].value, Is.SameAs(second));
                Assert.That(modified.ObjectCurve[1].value, Is.Null);
                Assert.That(modified.ObjectCurve[2].value, Is.SameAs(second));
                Assert.That(modified.ObjectCurve[2].time, Is.EqualTo(1));
                Assert.That(curve.ObjectCurve[0].value, Is.SameAs(first));
            } finally {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }
    }
}
