using System;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class FullControllerGlobalParamsTest {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void DeprecatedFlagActsAsGlobalWildcard(bool deprecatedFlag, bool wildcard) {
            var root = new GameObject("Avatar");
            var raw = new AnimatorController();
            var expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var owner = GameObjects.Create("Shirt", root);
                var physbone = owner.AddComponent<VRCPhysBone>();
                physbone.parameter = "shirt";
                raw.AddParameter("shirt_Stretch", AnimatorControllerParameterType.Float);
                raw.AddParameter("Private", AnimatorControllerParameterType.Bool);
                expressionParameters.parameters = new[] { ExpressionParam.Create("Synced").Bool() };

                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.Set("componentObject", new Func<VFGameObject>(() => owner));
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var builder = injector.CreateAndFillObject<FullControllerBuilder>();
                builder.featureBaseObject = owner;
                builder.model = new FullController {
                    controllers = { new FullController.ControllerEntry { controller = raw } },
                    prms = { new FullController.ParamsEntry { parameters = expressionParameters } },
                    globalParams = { "shirt_Stretch", "!Private" }
                };
                builder.model.allNonsyncedAreGlobal = deprecatedFlag;
                if (wildcard) builder.model.globalParams.Add("*");

                var allGlobal = deprecatedFlag || wildcard;
                var privateSyncedParams = allGlobal ? Array.Empty<string>() : new[] { "Synced" };
                Assert.That(builder.GetPrivateSyncedParameters(), Is.EqualTo(privateSyncedParams));
                builder.Apply();

                Assert.That(physbone.parameter == "shirt", Is.EqualTo(allGlobal));
                Assert.That(controllers.GetFx().TryGetParam("shirt_Stretch", out _), Is.True);
                Assert.That(controllers.GetFx().TryGetParam("Private", out _), Is.False);
                Assert.That(injector.GetService<ExpressionParamsService>().Contains("Synced"), Is.EqualTo(allGlobal));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(expressionParameters);
            }
        }
    }
}
