using NUnit.Framework;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    internal class AnimationCurveExtensionsTest {
        [Test]
        public void ClonePreservesWeightedKeysWrapModesAndNan() {
            var source = new AnimationCurve(new Keyframe(0, 2, 3, 4, 0.25f, 0.75f) {
                weightedMode = WeightedMode.Both
            }) {
                preWrapMode = WrapMode.PingPong,
                postWrapMode = WrapMode.Loop
            };
            Assert.That(source.AddKey(1, float.NaN), Is.EqualTo(1));

            var copy = source.Clone();
            Assert.That(copy.length, Is.EqualTo(source.length));
            Assert.That(copy[0], Is.EqualTo(source[0]));
            Assert.That(float.IsNaN(copy[1].value), Is.True);
            Assert.That(copy[1].time, Is.EqualTo(source[1].time));
            Assert.That(copy.preWrapMode, Is.EqualTo(source.preWrapMode));
            Assert.That(copy.postWrapMode, Is.EqualTo(source.postWrapMode));
            copy.RemoveKey(1);
            Assert.That(source.length, Is.EqualTo(2));
        }
    }
}
