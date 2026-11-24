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

    internal class OgbEnabledServiceTest : ExpressionParameterTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void OgbOverridesExistingControllerAndExpressionDefaults(bool expressionAlreadyExists) {
            var root = new GameObject("OGB defaults");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var parameter = controllers.GetFx().GetOrCreateBool("OGB/Enabled", true);
                if (expressionAlreadyExists) parameter.MakeExpressionBool(synced: false);
                injector.GetService<OgbEnabledService>().Register(root);
                Assert.That(parameter.GetDefault(), Is.Zero);
                var expression = avatar.expressionParameters.FindParameter("OGB/Enabled");
                Assert.That(expression.defaultValue, Is.Zero);
                Assert.That(expression.IsNetworkSynced(), Is.False);
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
