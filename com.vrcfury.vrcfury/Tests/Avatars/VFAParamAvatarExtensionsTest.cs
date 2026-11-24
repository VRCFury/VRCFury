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

    internal class VFAParamAvatarExtensionsTest : ExpressionParameterTestBase {
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [TestCase(VRCExpressionParameters.ValueType.Int, 3, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 3, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8, true)]
        public void MakeExpressionRejectsConflictingEncodingWithoutChangingExistingSettings(
            VRCExpressionParameters.ValueType type, int width, bool flipped) {
            var root = new GameObject("Conflicting expression encoding");
            try {
                var expressions = GetExpressionParams(root);
                var original = new VRCExpressionParameters.Parameter {
                    name = "Shared", valueType = type, bitWidth = (byte)width,
                    flippedRepresentation = flipped, saved = true, defaultValue = 1
                };
                original.SetNetworkSynced(false);
                expressions.AddOrMerge(original);
                var before = JsonUtility.ToJson(original);
                var controller = CreateController(expressions);
                var parameter = type == VRCExpressionParameters.ValueType.Int
                    ? controller.GetOrCreateInt("Shared") : controller.GetOrCreateFloat("Shared");

                var error = Assert.Throws<Exception>(() => parameter.MakeExpression(type));

                Assert.That(error.Message, Does.Contain("bit width or signedness"));
                Assert.That(JsonUtility.ToJson(expressions.Single()), Is.EqualTo(before));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
#endif

        [Test]
        public void MovingAParameterKeepsItsIdentityAndUsesTheDestinationExpressionService() {
            var sourceRoot = new GameObject("Source avatar");
            var destinationRoot = new GameObject("Destination avatar");
            try {
                var sourceExpressions = GetExpressionParams(sourceRoot);
                var destinationExpressions = GetExpressionParams(destinationRoot);
                var source = CreateController(sourceExpressions);
                var destination = CreateController(destinationExpressions);
                var parameter = source.GetOrCreateBool("Moved", true);
                destination.TakeOwnershipOf(source);
                Assert.That(source.TryGetParam("Moved", out _), Is.False);
                Assert.That(destination.GetOrCreateBool("Moved"), Is.SameAs(parameter));
                parameter.MakeExpressionBool(saved: true);
                Assert.That(sourceExpressions.Contains("Moved"), Is.False);
                Assert.That(destinationExpressions.Single().defaultValue, Is.EqualTo(1));
                Assert.That(destinationExpressions.Single().saved, Is.True);
                var clone = destination.Clone();
                Assert.That(clone, Is.TypeOf<VFAvatarController>());
                Assert.That(clone.GetOrCreateBool("Moved"), Is.Not.SameAs(parameter));
                clone.GetOrCreateBool("FromClone").MakeExpressionBool();
                Assert.That(destinationExpressions.Contains("FromClone"), Is.True);
                clone.Clone().GetOrCreateBool("FromSecondClone").MakeExpressionBool();
                Assert.That(destinationExpressions.Contains("FromSecondClone"), Is.True);
            } finally {
                Object.DestroyImmediate(sourceRoot);
                Object.DestroyImmediate(destinationRoot);
            }
        }

        [TestCase(VRCExpressionParameters.ValueType.Bool, false)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, true)]
        [TestCase(VRCExpressionParameters.ValueType.Int, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, true)]
        public void MakeExpressionUsesTheRequestedNetworkSync(VRCExpressionParameters.ValueType type, bool synced) {
            var root = new GameObject("Expression network sync");
            try {
                var expressions = GetExpressionParams(root);
                var parameter = CreateController(expressions).GetOrCreateBool("Value");
                switch (type) {
                    case VRCExpressionParameters.ValueType.Bool: parameter.MakeExpressionBool(synced, saved: true); break;
                    case VRCExpressionParameters.ValueType.Int: parameter.MakeExpressionInt(synced, saved: true); break;
                    case VRCExpressionParameters.ValueType.Float: parameter.MakeExpressionFloat(synced, saved: true); break;
                }
                var expression = expressions.Single(p => p.name == parameter.name);
                Assert.That(expression.valueType, Is.EqualTo(type));
                Assert.That(expression.IsNetworkSynced(), Is.EqualTo(synced));
                Assert.That(expression.saved, Is.True);
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(VRCExpressionParameters.ValueType.Bool, VRCExpressionParameters.ValueType.Float)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, VRCExpressionParameters.ValueType.Int)]
        [TestCase(VRCExpressionParameters.ValueType.Int, VRCExpressionParameters.ValueType.Bool)]
        [TestCase(VRCExpressionParameters.ValueType.Int, VRCExpressionParameters.ValueType.Float)]
        [TestCase(VRCExpressionParameters.ValueType.Float, VRCExpressionParameters.ValueType.Bool)]
        [TestCase(VRCExpressionParameters.ValueType.Float, VRCExpressionParameters.ValueType.Int)]
        public void MakeExpressionRejectsConflictingTypesWithoutChangingExistingSettings(
            VRCExpressionParameters.ValueType existingType, VRCExpressionParameters.ValueType requestedType) {
            var root = new GameObject("Conflicting expression types");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                var expressions = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ExpressionParamsService>();
                var parameter = CreateController(expressions).GetOrCreateBool("Shared");
                var existing = parameter.MakeExpression(existingType, saved: true);
                var exception = Assert.Throws<Exception>(() => parameter.MakeExpression(requestedType, saved: false));
                Assert.That(exception.Message, Does.Contain("Shared").And.Contain(existingType.ToString()).And.Contain(requestedType.ToString()));
                Assert.That(expressions.ToArray(), Has.Length.EqualTo(1));
                Assert.That(existing.valueType, Is.EqualTo(existingType));
                Assert.That(existing.saved, Is.True);
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SyncedHandlesKeepTheirAvatarContextThroughLookupCloningAndMerging() {
            VRCAvatarDescriptor MakeAvatar(string name) {
                var avatar = new GameObject(name).AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                return avatar;
            }
            var first = MakeAvatar("First parameter context");
            var second = MakeAvatar("Second parameter context");
            try {
                VFAvatarController GetFx(VRCAvatarDescriptor avatar) {
                    var controllers = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ControllersService>();
                    controllers.ClearCache(applyBaseMask: false);
                    return controllers.GetFx();
                }
                var fx = GetFx(first);
                var otherFx = GetFx(second);
                var input = fx.GetOrCreateBool("Input", 0.75f);
                Assert.That(first.expressionParameters, Is.Null, "Handles must not create expression assets until requested");
                input.MakeExpressionFloat(synced: false, saved: true);
                Assert.That(second.expressionParameters, Is.Null);
                Assert.That(fx.TryGetParam(input.name, out var found), Is.True);
                found.MakeExpression(VRCExpressionParameters.ValueType.Float, synced: false, saved: true);
                var expression = first.expressionParameters.FindParameter(input.name);
                Assert.That(expression.defaultValue, Is.EqualTo(0.75f));
                Assert.That(expression.saved, Is.True);
                Assert.That(expression.IsNetworkSynced(), Is.False);

                var clone = fx.Clone();
                clone.RewriteParameters(name => "Clone/" + name);
                clone.GetOrCreateBool("Clone/" + input.name).MakeExpressionFloat(synced: false, saved: true);
                Assert.That(first.expressionParameters.FindParameter("Clone/" + input.name).defaultValue, Is.EqualTo(0.75f));
                otherFx.TakeOwnershipOf(clone);
                otherFx.GetOrCreateBool("Clone/" + input.name).MakeExpressionFloat(synced: false, saved: true);
                Assert.That(second.expressionParameters.FindParameter("Clone/" + input.name).defaultValue, Is.EqualTo(0.75f));
                otherFx.GetOrCreateBool(input.name, 1).MakeExpressionBool();
                Assert.That(second.expressionParameters.FindParameter(input.name).valueType, Is.EqualTo(VRCExpressionParameters.ValueType.Bool));
                Assert.That(second.expressionParameters.FindParameter(input.name).saved, Is.False);
                Assert.That(first.expressionParameters.FindParameter(input.name).valueType, Is.EqualTo(VRCExpressionParameters.ValueType.Float));
                Assert.Throws<InvalidOperationException>(() => VFController.Create().GetOrCreateBool("No avatar").MakeExpressionBool());
            } finally {
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void AllocationAnimatorRegistrationAndExpressionRegistrationAreIndependent() {
            var root = new GameObject("Parameter flow");
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
                var name = injector.GetService<UniqueParamService>().GetObjectScopedName(root, "Feature");
                var fx = controllers.GetFx();
                Assert.That(FindDefinition(fx, name), Is.Null);
                Assert.That(avatar.expressionParameters, Is.Null);
                var parameter = fx.GetOrCreateBool(name, 0.25f);
                Assert.That(avatar.expressionParameters, Is.Null, "Animator registration must not create expression entries");
                var expressions = injector.GetService<ExpressionParamsService>();
                var expression = expressions.AddOrMerge(new VRCExpressionParameters.Parameter {
                    name = parameter.name, valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = parameter.GetDefault(), saved = true
                });
                Assert.That(expression, Is.SameAs(avatar.expressionParameters.FindParameter(name)));
                Assert.That(expressions.ToArray(), Has.Length.EqualTo(1));
                Assert.That(expression.defaultValue, Is.EqualTo(0.25f));
                var actionParameter = parameter.CopyTo(controllers.GetAction());
                parameter.SetDefault(0.75f);
                Assert.That(fx.GetOrCreateBool(name).GetDefault(), Is.EqualTo(0.75f));
                Assert.That(actionParameter.GetDefault(), Is.EqualTo(0.25f));
                Assert.That(expression.defaultValue, Is.EqualTo(0.25f), "Expression defaults are configured independently");
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
