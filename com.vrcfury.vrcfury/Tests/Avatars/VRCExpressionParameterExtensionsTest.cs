using NUnit.Framework;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Tests {
    [Category("VRCFury")]
    public class VRCExpressionParameterExtensionsTest {
        [TestCase(VRCExpressionParameters.ValueType.Bool, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8)]
        public void DefaultParameterCosts(VRCExpressionParameters.ValueType type, int expected) {
            Assert.That(new VRCExpressionParameters.Parameter { valueType = type }.TypeCost(), Is.EqualTo(expected));
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
    }
}
