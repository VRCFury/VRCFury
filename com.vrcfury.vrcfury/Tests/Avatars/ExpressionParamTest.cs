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

    internal class ExpressionParamTest : ExpressionParameterTestBase {
        [TestCase(VRCExpressionParameters.ValueType.Bool)]
        [TestCase(VRCExpressionParameters.ValueType.Int)]
        [TestCase(VRCExpressionParameters.ValueType.Float)]
        public void FluentExpressionDeclarationsDefaultToUnsavedAndConfigureTheSameInstance(VRCExpressionParameters.ValueType type) {
            var parameter = ExpressionParam.Create("Example");
            Assert.That(parameter.name, Is.EqualTo("Example"));
            Assert.That(parameter.saved, Is.False);
            Assert.That(parameter.IsNetworkSynced(), Is.True);
            Assert.That(parameter.defaultValue, Is.Zero);
            var typed = type == VRCExpressionParameters.ValueType.Bool ? parameter.Bool()
                : type == VRCExpressionParameters.ValueType.Int ? parameter.Int() : parameter.Float();
            Assert.That(typed, Is.SameAs(parameter));
            Assert.That(parameter.valueType, Is.EqualTo(type));
            Assert.That(parameter.Default(true).Saved().Unsynced(), Is.SameAs(parameter));
            Assert.That(parameter.defaultValue, Is.EqualTo(1));
            Assert.That(parameter.saved, Is.True);
            Assert.That(parameter.IsNetworkSynced(), Is.False);
            Assert.That(parameter.Saved(false).Synced(), Is.SameAs(parameter));
            Assert.That(parameter.saved, Is.False);
            Assert.That(parameter.IsNetworkSynced(), Is.True);
            Assert.That(parameter.defaultValue, Is.EqualTo(1));
            Assert.That(parameter.valueType, Is.EqualTo(type));
        }
    }
}
