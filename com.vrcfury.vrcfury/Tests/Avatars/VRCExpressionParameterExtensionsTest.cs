using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VF.Service;
using VF.Service.Compressor;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class VRCExpressionParameterExtensionsTest {
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
            var paramz = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                paramz.parameters = new[] { original };
                if (same) {
                    Assert.DoesNotThrow(() => paramz.Add(added));
                } else {
                    var error = Assert.Throws<Exception>(() => paramz.Add(added));
                    Assert.That(error.Message, Does.Contain("Test").And.Contain("bit width or signedness"));
                }
                Assert.That(paramz.parameters.Single(), Is.SameAs(original));
            } finally {
                Object.DestroyImmediate(paramz);
            }
        }

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

        [Test]
        public void ParameterListsDetectEncodingOnlyChangesForPlatformAlignment() {
            var desktop = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            var mobile = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                desktop.parameters = new[] { new VRCExpressionParameters.Parameter {
                    name = "Test", valueType = VRCExpressionParameters.ValueType.Int
                } };
                mobile.parameters = new[] { desktop.parameters[0].Clone() };
                Assert.That(desktop.IsSameAs(mobile), Is.True);
                mobile.parameters[0].bitWidth = 3;
                Assert.That(desktop.IsSameAs(mobile), Is.False);
                mobile.parameters[0].bitWidth = 8;
                mobile.parameters[0].flippedRepresentation = true;
                Assert.That(desktop.IsSameAs(mobile), Is.False);
            } finally {
                Object.DestroyImmediate(desktop);
                Object.DestroyImmediate(mobile);
            }
        }

        [Test]
        public void CompressorSolverExcludesNonstandardWidthsAndSignedness() {
            var obj = new GameObject("Avatar");
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            var paramz = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = obj.AddComponent<VRCAvatarDescriptor>();
                avatar.customExpressions = true;
                avatar.expressionsMenu = menu;
                var parameters = new List<VRCExpressionParameters.Parameter>();
                var expected = new List<string>();
                foreach (var type in new[] { VRCExpressionParameters.ValueType.Int, VRCExpressionParameters.ValueType.Float }) {
                    foreach (var width in new byte[] { 3, 8 }) {
                        foreach (var flipped in new[] { false, true }) {
                            var p = new VRCExpressionParameters.Parameter {
                                name = type + "/" + width + "/" + flipped, valueType = type,
                                bitWidth = width, flippedRepresentation = flipped
                            };
                            parameters.Add(p);
                            if (width == 8 && !flipped) expected.Add(p.name);
                        }
                    }
                }
                parameters.Add(new VRCExpressionParameters.Parameter { name = "Bool", valueType = VRCExpressionParameters.ValueType.Bool });
                expected.Add("Bool");
                foreach (var p in parameters) p.SetNetworkSynced(true);
                paramz.parameters = parameters.ToArray();
                menu.controls = parameters.Select(p => new VRCExpressionsMenu.Control {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = p.name }
                }).ToList();
                var menuService = new MenuService();
                typeof(MenuService).VFField("avatar").SetValue(menuService, avatar);
                var solver = new ParameterCompressorSolverService();
                typeof(ParameterCompressorSolverService).VFField("menuService").SetValue(solver, menuService);
                var method = typeof(ParameterCompressorSolverService).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(m => m.Name == "GetParamsToOptimize" && m.GetParameters().Length == 5);
                var decision = (OptimizationDecision)method.Invoke(solver, new object[] {
                    paramz, new HashSet<VRCExpressionsMenu.Control.ControlType> { VRCExpressionsMenu.Control.ControlType.Toggle },
                    new HashSet<string>(), 512, false
                });
                Assert.That(decision.compress.Select(p => p.name), Is.EquivalentTo(expected));
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(menu);
                Object.DestroyImmediate(paramz);
            }
        }
#endif
    }
}
