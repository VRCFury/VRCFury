namespace VF.Tests {
    using System;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Builder;
    using VF.Exceptions;
    using VF.Injector;
    using VF.Service;
    using VF.Service.Compressor;
    using VF.Utils;
    using VRC.Core;
    using VRC.SDK3.Avatars.Components;
    using Object = UnityEngine.Object;

    internal class UniqueParamServiceTest : ParameterPlatformBuildTestBase {
        [Test]
        public void NamesDependOnBlueprintAndRelativePathAcrossIndependentBuilds() {
            using var pc = new Build("Desktop");
            using var mobile = new Build("Quest");
            using var other = new Build("Desktop", "avtr-other");
            var hat = pc.Add("Props/Hat", "Hat");
            // Published names must remain stable across future builds and editor versions.
            Assert.That(hat, Is.EqualTo("VF_Uh01WpEl_Hat"));
            Assert.That(pc.Add("Props/Hat", "VF12_Hat"), Is.EqualTo("VF_Uh01WpEl_VF12_Hat"));
            Assert.That(mobile.Add("Other/Hat", "Hat"), Is.Not.EqualTo(hat));
            mobile.injector.GetService<GlobalsService>().currentFeatureNum = 35;
            Assert.That(mobile.Add("Props/Hat", "Hat"), Is.EqualTo(hat));
            Assert.That(mobile.Add("Props/Hat", hat), Is.EqualTo(hat + "_2"));
            Assert.That(other.Add("Props/Hat", "Hat"), Is.Not.EqualTo(hat));
        }

        [Test]
        public void InternalParametersShareOneRandomPrefixPerBuild() {
            using var first = new Build("Avatar");
            using var second = new Build("Avatar");
            var names = first.injector.GetService<UniqueParamService>();
            var a = names.GetInternalName("Counter");
            Assert.That(a, Does.Match("^VF_[A-Za-z0-9]{8}_Counter$"));
            var prefix = a.Substring(0, a.Length - "Counter".Length);
            Assert.That(names.GetInternalName("Counter"), Is.EqualTo(prefix + "Counter_2"));
            Assert.That(names.GetInternalName("Weight"), Is.EqualTo(prefix + "Weight"));
            Assert.That(second.injector.GetService<UniqueParamService>().GetInternalName("Counter"), Is.Not.EqualTo(a));
        }

        [Test]
        public void RenamedOwnerUsesItsNewPathForSubsequentNames() {
            using var build = new Build("Avatar");
            var owner = GameObjects.Create("Mobile prop", build.root);
            var names = build.injector.GetService<UniqueParamService>();
            var before = names.GetObjectScopedName(owner, "Color");
            owner.name = "Desktop prop";
            var after = names.GetObjectScopedName(owner, "Color");
            Assert.That(after, Is.Not.EqualTo(before));
            Assert.That(after, Is.EqualTo("VF_zFoL8ZlQ_Color"));
        }

        [Test]
        public void AvatarWideNameCannotSilentlyAcquireAPlatformSpecificSuffix() {
            using var pc = new Build("Desktop");
            pc.fx.GetOrCreateBool("VF_kwm3VRff_SyncPointer");
            Assert.Throws<VRCFBuilderException>(() => pc.injector.GetService<UniqueParamService>().GetAvatarWideName("SyncPointer"));
        }
    }
}

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

    internal class UniqueParamServiceReservationTest : ExpressionParameterTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void UniqueNamesCheckAllControllersAndForgetDiscardedControllers(bool overrides) {
            var root = new GameObject("Unloaded parameter names");
            const string prefix = "VF_sGqeWWch_";
            var raw = new UnityEditor.Animations.AnimatorController {
                parameters = new[] { new AnimatorControllerParameter { name = prefix + "Existing", type = AnimatorControllerParameterType.Float } }
            };
            var wrapper = new AnimatorOverrideController(raw);
            var action = new UnityEditor.Animations.AnimatorController {
                parameters = new[] { new AnimatorControllerParameter { name = prefix + "FromAction", type = AnimatorControllerParameterType.Float } }
            };
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer {
                        type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = overrides ? (RuntimeAnimatorController)wrapper : raw
                    }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer {
                        type = VRCAvatarDescriptor.AnimLayerType.Action, animatorController = action
                    }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var names = injector.GetService<UniqueParamService>();
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                Assert.That(names.GetObjectScopedName(root, "Existing"), Is.EqualTo(prefix + "Existing_2"));
                Assert.That(names.GetObjectScopedName(root, "FromAction"), Is.EqualTo(prefix + "FromAction_2"));
                Assert.That(controllers.GetAllMutatedControllers(), Is.Empty);
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                fx.GetOrCreateBool(prefix + "Transient");
                Assert.That(names.GetObjectScopedName(root, "Transient"), Is.EqualTo(prefix + "Transient_2"));
                controllers.ClearCache(applyBaseMask: false);
                Assert.That(names.GetObjectScopedName(root, "Transient"), Is.EqualTo(prefix + "Transient"), "Discarded controller definitions must no longer block names");
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(wrapper);
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void NameChecksDoNotCreateExpressionAssetsAndNewEntriesDefaultToUnsaved() {
            var root = new GameObject("Expression parameter lookup");
            var imported = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                controllers.GetFx();
                var names = injector.GetService<UniqueParamService>();
                var expressions = injector.GetService<ExpressionParamsService>();
                var count = Resources.FindObjectsOfTypeAll<VRCExpressionParameters>().Length;
                Assert.That(expressions.Contains("Missing"), Is.False);
                names.GetObjectScopedName(root, "One");
                names.GetObjectScopedName(root, "Two");
                Assert.That(avatar.expressionParameters, Is.Null);
                Assert.That(Resources.FindObjectsOfTypeAll<VRCExpressionParameters>(), Has.Length.EqualTo(count));

                imported.parameters = new[] { new VRCExpressionParameters.Parameter {
                    name = "Imported", valueType = VRCExpressionParameters.ValueType.Bool, saved = true
                } };
                avatar.customExpressions = true;
                avatar.expressionParameters = imported;
                Assert.That(expressions.Contains("Imported"), Is.True);
                Assert.That(avatar.expressionParameters, Is.SameAs(imported));
                Assert.That(Resources.FindObjectsOfTypeAll<VRCExpressionParameters>(), Has.Length.EqualTo(count));

                var created = expressions.AddOrMerge(new VRCExpressionParameters.Parameter {
                    name = "New", saved = false, valueType = VRCExpressionParameters.ValueType.Bool
                });
                Assert.That(created.saved, Is.False);
                Assert.That(expressions.Contains("New"), Is.True);
                Assert.That(expressions.AddOrMerge(new VRCExpressionParameters.Parameter { name = "Saved", saved = true }).saved, Is.True);
                Assert.That(expressions.AddOrMerge(imported.parameters[0].Clone()).saved, Is.True);
                Assert.That(imported.parameters, Has.Length.EqualTo(1));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(imported);
            }
        }

        [Test]
        public void UniqueNamesUseTheSuppliedOwnerAcrossControllers() {
            var root = new GameObject("Bound unique parameters");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var names = injector.GetService<UniqueParamService>();
                const string prefix = "VF_sGqeWWch_";
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                var literal = fx.GetOrCreateBool("Depth", 2);
                var first = fx.GetOrCreateBool(names.GetObjectScopedName(root, "Depth"), def: true);
                var second = fx.GetOrCreateBool(names.GetObjectScopedName(root, "Depth"), def: 3);
                var third = controllers.GetAction().GetOrCreateBool(names.GetObjectScopedName(root, "Depth"), def: 4);
                Assert.That(first.name, Is.EqualTo(prefix + "Depth"));
                Assert.That(second.name, Is.EqualTo(prefix + "Depth_2"));
                Assert.That(third.name, Is.EqualTo(prefix + "Depth_3"));
                Assert.That(first.GetDefault(), Is.EqualTo(1));
                Assert.That(second.GetDefault(), Is.EqualTo(3));
                Assert.That(third.GetDefault(), Is.EqualTo(4));
                Assert.That(literal.name, Is.EqualTo("Depth"));
                Assert.That(fx.GetOrCreateBool("Depth", 100).GetDefault(), Is.EqualTo(2));
                var other = GameObjects.Create("Other", root);
                Assert.That(
                    fx.GetOrCreateBool(names.GetObjectScopedName(other, "Depth")).name,
                    Is.EqualTo("VF_AC5lr9R5_Depth"),
                    "Allocation must use the supplied object's current path"
                );
                Assert.That(avatar.expressionParameters, Is.Null);
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InternalNamesSkipParametersInOtherControllersAndExpressions() {
            var root = new GameObject("Internal parameter names");
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
                var fx = controllers.GetFx();
                var probe = fx.CreateUniqueFloat("Probe").name;
                var prefix = probe.Substring(0, probe.Length - "Probe".Length);
                string Candidate(int offset) => offset == 1 ? prefix + "Value" : prefix + "Value_" + offset;

                controllers.GetAction().GetOrCreateBool(Candidate(1));
                injector.GetService<ExpressionParamsService>().AddOrMerge(ExpressionParam.Create(Candidate(2)).Float());
                Assert.That(fx.CreateUniqueBool("Value").name, Is.EqualTo(Candidate(3)));
                Assert.That(controllers.GetAction().CreateUniqueInt("Value").name, Is.EqualTo(Candidate(4)));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UniqueNamesAvoidExpressionsAllControllersAndOutstandingReservations() {
            var root = new GameObject("Unique parameter names");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                const string prefix = "VF_sGqeWWch_";
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var expressionParams = injector.GetService<ExpressionParamsService>();
                expressionParams.AddOrMerge(new VRCExpressionParameters.Parameter { name = prefix + "Test", valueType = VRCExpressionParameters.ValueType.Bool, saved = false });
                var fx = controllers.GetFx();
                Assert.That(fx.GetOrCreateBool(prefix + "Test_2").name, Is.EqualTo(prefix + "Test_2"));
                Assert.That(controllers.GetAction().GetOrCreateBool(prefix + "Test_3").name, Is.EqualTo(prefix + "Test_3"));
                var uniqueParams = injector.GetService<UniqueParamService>();
                var first = uniqueParams.GetObjectScopedName(root, "Test");
                Assert.That(first, Is.EqualTo(prefix + "Test_4"));
                Assert.That(uniqueParams.GetObjectScopedName(root, "Test"), Is.EqualTo(prefix + "Test_5"));
                Assert.That(FindDefinition(fx, first), Is.Null, "Allocation must not create an animator parameter");
                Assert.That(expressionParams.ToArray(), Has.Length.EqualTo(1));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
