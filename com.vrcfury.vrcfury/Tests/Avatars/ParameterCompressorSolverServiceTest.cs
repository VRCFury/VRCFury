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

    internal class ParameterCompressorSolverServiceTest : ExpressionParameterTestBase {
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [Test]
        public void CompressorSolverAcceptsCustomWidthsAndSignedness() {
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
                            expected.Add(p.name);
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
                    .Single(m => m.Name == "GetParamsToOptimize");
                var decision = (OptimizationDecision)method.Invoke(solver, new object[] {
                    paramz.parameters,
                    new HashSet<VRCExpressionsMenu.Control.ControlType> { VRCExpressionsMenu.Control.ControlType.Toggle },
                    new HashSet<string>(),
                    512
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
