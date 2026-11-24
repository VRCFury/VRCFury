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

    internal class sps_lightsTest : SpsShaderTestBase {
        [TestCase(.41f, true, true)]
        [TestCase(.42f, true, true)]
        [TestCase(.45f, true, false)]
        [TestCase(.41f, false, false)]
        public void LegacyLightCanStartDeformationWithoutEntranceContacts(float range, bool legacy, bool deforms) {
            SetPath(deformationShader, (Vector3.forward, Vector3.forward, 0));
            SetContacts("SPS_Self", Matrix4x4.identity);
            material.SetFloat("_SPS_UseLegacyLights", legacy ? 1 : 0);
            material.SetVector("_TestLightAtten", new Vector4(0, 0, 0, 25 / (range * range)));
            material.SetVector("_TestLightX", new Vector4(0, 0, 0, .1f));
            material.SetVector("_TestLightZ", new Vector4(0, 0, 0, .5f));
            material.SetFloat("_SampleDistance", .3f);
            var sample = Render();
            if (deforms) {
                Assert.That(sample.a, Is.EqualTo(1).Within(.0001f));
                Assert.That(sample.r, Is.GreaterThan(0));
            } else {
                Assert.That(sample.a, Is.EqualTo(-2));
            }
        }

        [TestCase(3.6f, false)]
        [TestCase(4.2f, true)]
        public void IdleLightRangeIncludesScaledReachAndEntryAdvance(float distance, bool exitsEarly) {
            SetPath(deformationShader, (Vector3.forward, Vector3.forward, 0));
            var root = Vector3.forward * 20;
            SetContacts("SPS_Self", Matrix4x4.TRS(root, Quaternion.identity, Vector3.one * 2));
            material.SetFloat("_SPS_UseLegacyLights", 1);
            material.SetVector("_TestLightAtten", new Vector4(25 / (.41f * .41f), 0, 0, 0));
            material.SetVector("_TestLightZ", new Vector4(root.z + distance, 0, 0, 0));
            Assert.That(Render().a == -2, Is.EqualTo(exitsEarly));
        }
    }
}
