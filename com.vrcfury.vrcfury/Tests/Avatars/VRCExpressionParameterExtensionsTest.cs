namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Builder;
    using VF.Service;
    using VF.Service.Compressor;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Avatars.ScriptableObjects;
    using Object = UnityEngine.Object;
    using VF.Utils.Controller;

    internal class VRCExpressionParameterExtensionsTest : ExpressionParameterTestBase {
        [TestCase(VRCExpressionParameters.ValueType.Bool, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8)]
        public void DefaultParameterCosts(VRCExpressionParameters.ValueType type, int expected) {
            Assert.That(new VRCExpressionParameters.Parameter { valueType = type }.TypeCost(), Is.EqualTo(expected));
            Assert.That(new VRCExpressionParameters.Parameter { valueType = type }.HasStandardNetworkEncoding(), Is.True);
        }

#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [TestCase(VRCExpressionParameters.ValueType.Int, 0, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 1, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 3, 3)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 255, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 5, 5)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 255, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 8, 1)]
        public void DynamicWidthsUseSdkClamping(VRCExpressionParameters.ValueType type, int width, int expected) {
            Assert.That(new VRCExpressionParameters.Parameter {
                valueType = type, bitWidth = (byte)width
            }.TypeCost(), Is.EqualTo(expected));
        }
#endif
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [TestCase(VRCExpressionParameters.ValueType.Int, 3, false, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, false, true)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, true, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 255, false, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 5, false, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8, false, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8, true, false)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 3, true, true)]
        public void StandardEncodingUsesEffectiveWidthAndRepresentation(
            VRCExpressionParameters.ValueType type, int width, bool flipped, bool standard) {
            Assert.That(new VRCExpressionParameters.Parameter {
                valueType = type, bitWidth = (byte)width, flippedRepresentation = flipped
            }.HasStandardNetworkEncoding(), Is.EqualTo(standard));
        }
#endif
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [TestCase(VRCExpressionParameters.ValueType.Int, 3, false, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8, true, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, true, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 255, false, true)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 3, true, true)]
        public void EqualityAndMergeCheckNetworkEncoding(
            VRCExpressionParameters.ValueType type, int width, bool flipped, bool same) {
            var original = new VRCExpressionParameters.Parameter { name = "Test", valueType = type };
            var added = original.Clone();
            added.bitWidth = (byte)width;
            added.flippedRepresentation = flipped;
            Assert.That(original.IsSameAs(added), Is.EqualTo(same));
            var root = new GameObject("Network encoding merging");
            try {
                var paramz = GetExpressionParams(root);
                paramz.SetParameters(new[] { original });
                if (same) {
                    Assert.DoesNotThrow(() => paramz.AddOrMerge(added));
                } else {
                    var error = Assert.Throws<Exception>(() => paramz.AddOrMerge(added));
                    Assert.That(error.Message, Does.Contain("Test").And.Contain("bit width or signedness"));
                }
                Assert.That(paramz.Single(), Is.SameAs(original));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
#endif
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [Test]
        public void ClonePreservesWidthAndRepresentation() {
            var original = new VRCExpressionParameters.Parameter {
                name = "Test", valueType = VRCExpressionParameters.ValueType.Int,
                bitWidth = 5, flippedRepresentation = true
            };
            var clone = original.Clone();
            Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(clone.bitWidth, Is.EqualTo(5));
            Assert.That(clone.flippedRepresentation, Is.True);
            Assert.That(clone.IsSameAs(original), Is.True);
        }
#endif
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [Test]
        public void ParameterListsDetectEncodingOnlyChangesForPlatformAlignment() {
            var root = new GameObject("Parameter list equality");
            try {
                var desktop = GetExpressionParams(root);
                desktop.SetParameters(new[] { new VRCExpressionParameters.Parameter {
                    name = "Test", valueType = VRCExpressionParameters.ValueType.Int
                } });
                var mobile = desktop.GetSnapshot();
                Assert.That(desktop.IsSameAs(mobile), Is.True);
                mobile[0].bitWidth = 3;
                Assert.That(desktop.IsSameAs(mobile), Is.False);
                mobile[0].bitWidth = 8;
                mobile[0].flippedRepresentation = true;
                Assert.That(desktop.IsSameAs(mobile), Is.False);
            } finally {
                Object.DestroyImmediate(root);
            }
        }
#endif
    }
}
