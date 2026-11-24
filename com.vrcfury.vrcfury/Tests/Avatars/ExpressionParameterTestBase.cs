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

namespace VF.Tests {
    internal abstract class ExpressionParameterTestBase {
        protected static VFAParam FindDefinition(VFController controller, string name) =>
            controller.parameters.FirstOrDefault(p => p.name == name);

        protected static VFAvatarController CreateController(ExpressionParamsService expressions) =>
            new VFAvatarController(
                VFController.Create(),
                VRCAvatarDescriptor.AnimLayerType.FX,
                () => 0,
                null,
                new LayerSourceService(),
                expressions
            );

        protected static ExpressionParamsService GetExpressionParams(GameObject root) =>
            VRCFuryInjectorBuilder.GetInjector(root.AddComponent<VRCAvatarDescriptor>()).GetService<ExpressionParamsService>();

#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH

#endif
    }
}
