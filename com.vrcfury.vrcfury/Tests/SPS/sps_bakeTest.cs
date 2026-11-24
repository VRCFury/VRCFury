namespace VF.Tests {
    using System;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Rendering;
    using UnityEngine;
    using UnityEngine.Rendering;
    using VF.Builder.Haptics;
    using Object = UnityEngine.Object;

    internal class sps_bakeTest : SpsShaderTestBase {
        [TestCase(2, 0, 0)]
        [TestCase(3, 3, 0)]
        [TestCase(24, 3, 2)]
        public void BakedProfileUsesNearestUniformSampleAndClampsEndpoints(int count, int vertices, int blendshapes) {
            Read(Vector3.forward * .2f, Vector3.forward);
            var keys = Enumerable.Range(0, count).Select(i => new Keyframe(
                Mathf.Pow(i / (count - 1f), 2),
                i == count - 1 ? 0 : .2f + (i % 3) * .75f
            )).ToArray();
            var curve = ProfileCurve(keys);
            var texture = SetProfile(vertices, blendshapes, keys);
            try {
                material.SetFloat("_Operation", 7);
                foreach (var depth in keys.Select(key => key.time).Concat(Enumerable.Range(-1, 32).Select(i => i / 29f))) {
                    material.SetFloat("_ProfileDistance", depth);
                    var nearest = Mathf.RoundToInt(Mathf.Clamp01(depth) * (SpsBaker.RadiusProfileSamples - 1));
                    var expected = curve.Evaluate(nearest / (SpsBaker.RadiusProfileSamples - 1f));
                    Assert.That(Render().r, Is.EqualTo(expected).Within(.0001f), "Depth " + depth);
                }
            } finally {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
