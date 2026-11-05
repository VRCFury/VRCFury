using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using JetBrains.Annotations;
using UnityEditor;
using VF.Actions;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Inspector;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Injector {
    [Category("VRCFury")]
    public class VRCFuryInjectorTest {
        private UnityEngine.GameObject root;

        [SetUp]
        public void SetUp() => root = new UnityEngine.GameObject("Injector context");

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        private VRCFuryInjector CreateActionDebuggerInjector() {
            var injector = new VRCFuryInjector();
            injector.ImportOne(typeof(ActionClipService));
            injector.ImportOne(typeof(ClipFactoryService));
            injector.ImportOne(typeof(VRCFObjectPathCache));
            injector.ImportScan(typeof(ActionBuilder));
            injector.Set("avatarObject", root.asVf());
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            return injector;
        }

        private class InjectorConsumer {
            public readonly VRCFuryInjector injector;
            public InjectorConsumer(VRCFuryInjector injector) {
                this.injector = injector;
            }
        }

        private class Dependency { }

        private class RequiredConsumer {
            public readonly Dependency dependency;
            public RequiredConsumer(Dependency dependency) => this.dependency = dependency;
        }

        private class OptionalConsumer {
            public readonly Dependency dependency;
            public OptionalConsumer([CanBeNull] Dependency dependency) => this.dependency = dependency;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SetRejectsNullWithoutRegisteringIt(int overload) {
            var injector = new VRCFuryInjector();
            switch (overload) {
                case 0: Assert.Throws<ArgumentNullException>(() => injector.Set((object)null)); break;
                case 1: Assert.Throws<ArgumentNullException>(() => injector.Set(typeof(Dependency), null)); break;
                case 2: Assert.Throws<ArgumentNullException>(() => injector.Set("dependency", null)); break;
            }
            Assert.Throws<Exception>(() => injector.CreateAndFillObject<RequiredConsumer>());
            var dependency = new Dependency();
            if (overload == 2) injector.Set("dependency", dependency);
            else injector.Set(dependency);
            Assert.That(injector.CreateAndFillObject<RequiredConsumer>().dependency, Is.SameAs(dependency));
        }

        [Test]
        public void MissingDependenciesRequireCanBeNull() {
            var injector = new VRCFuryInjector();
            Assert.That(injector.CreateAndFillObject<OptionalConsumer>().dependency, Is.Null);
            Assert.Throws<Exception>(() => injector.CreateAndFillObject<RequiredConsumer>());
        }

        [Test]
        public void RejectedNullDoesNotOverwriteNamedRegistration() {
            var injector = new VRCFuryInjector();
            var dependency = new Dependency();
            injector.Set("dependency", dependency);
            Assert.Throws<ArgumentNullException>(() => injector.Set("dependency", null));
            Assert.That(injector.CreateAndFillObject<RequiredConsumer>().dependency, Is.SameAs(dependency));
        }

        [Test]
        public void EachInjectorResolvesAndInjectsItself() {
            var first = new VRCFuryInjector();
            var second = new VRCFuryInjector();
            Assert.That(first.GetService<VRCFuryInjector>(), Is.SameAs(first));
            Assert.That(second.GetService<VRCFuryInjector>(), Is.SameAs(second));
            Assert.That(first.CreateAndFillObject<InjectorConsumer>().injector, Is.SameAs(first));
            Assert.That(second.CreateAndFillObject<InjectorConsumer>().injector, Is.SameAs(second));
        }

        [Test]
        public void DetachedBakeReceiversDoNotResolveAvatarServices() {
            var root = new UnityEngine.GameObject("Detached bake receivers");
            try {
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                var receivers = injector.GetService<SelfAndOthersReceiverService>().Create(
                    new SelfAndOthersReceiverService.Request { frame = root, name = "Detached", worldRadius = 1 });
                var services = (System.Collections.IDictionary)typeof(VRCFuryInjector)
                    .GetField("services", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(injector);
                Assert.That(services.Contains(typeof(SelfAndOthersReceiverAnimatorService)), Is.False);
                Assert.That(services.Contains(typeof(ControllersService)), Is.False);
                Assert.That(receivers.IsCreated, Is.False);
            } finally {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AvatarBuildReceiversUseAutowiredAnimatorServices() {
            var root = new UnityEngine.GameObject("Build receivers");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer {
                    type = VRCAvatarDescriptor.AnimLayerType.FX
                } };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var receivers = injector.GetService<SelfAndOthersReceiverService>().Create(
                    new SelfAndOthersReceiverService.Request {
                        frame = root, name = "Build", worldRadius = 1
                    });
                var receiverAnimator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
                receiverAnimator.GetParameter(receivers, Party.Self);
                Assert.That(controllers.GetFx().GetLayers(), Is.Empty, "Unmerged receiver outputs don't need an animator tree");
                var parameter = receiverAnimator.GetParameter(receivers, Party.Merged);
                Assert.That(controllers.GetFx().GetLayers(), Is.Not.Empty);
                Assert.That(controllers.GetFx().parameters.FirstOrDefault(p => p.name == parameter.name), Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver>(true),
                    Has.Length.EqualTo(4));
                var layers = controllers.GetFx().GetLayers().Count();
                Assert.That(receiverAnimator.GetParameter(receivers, Party.Merged).name, Is.EqualTo(parameter.name));
                Assert.That(controllers.GetFx().GetLayers().Count(), Is.EqualTo(layers));
            } finally {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainComponentBuildContextResolves() {
            Verify("main component build context", () => {
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                injector.ImportScan(typeof(ActionBuilder));
                injector.Set("avatarObject", root.asVf());
                injector.Set("componentObject", new Func<VFGameObject>(() => root));
                injector.Set(new GlobalsService { avatarObject = root });
                injector.Set(root.AddComponent<VRCAvatarDescriptor>());
                injector.GetServices<object>();
            });
        }

        [Test]
        public void ActionDebuggerContextResolves() {
            Verify("action debugger context", () => {
                var injector = CreateActionDebuggerInjector();
                injector.GetService<ActionClipService>();
            });
        }

        [Test]
        public void PerFrameSpsInspectorContextResolves() {
            Verify("per-frame SPS inspector context", () => {
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                injector.Set("avatarObject", root.asVf());
                injector.GetService<SpsConfigurer>();
                injector.GetService<VRCFuryHapticPlugBaker>();
                injector.GetService<VRCFuryHapticSocketBaker>();
            });
        }

        [Test]
        public void DetachedSpsBakeContextResolves() {
            Verify("detached SPS bake context", () => {
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                injector.GetService<VRCFuryHapticPlugBaker>();
                injector.GetService<VRCFuryHapticSocketBaker>();
            });
        }

        [Test]
        public void FeatureEditorContextsResolve() {
            foreach (var builderType in ReflectionUtils.GetTypes(typeof(IVRCFuryBuilder))) {
                var modelType = ReflectionUtils.GetGenericArgument(builderType, typeof(IVRCFuryBuilder<>));

                var editorMethod = builderType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(method => method.GetCustomAttribute<FeatureEditorAttribute>() != null)
                    .DefaultIfEmpty(null)
                    .First();
                if (editorMethod == null) {
                    throw new Exception($"{builderType.Name} is missing an Editor");
                }

                Verify("editor context for " + builderType.Name, () => {
                    var injector = new VRCFuryInjector();
                    injector.Set(Activator.CreateInstance(modelType));
                    using (var serializedObject = new SerializedObject(root.transform)) {
                        injector.Set(serializedObject.FindProperty("m_LocalPosition"));
                        injector.Set("avatarObject", root.asVf());
                        injector.Set("componentObject", root.asVf());
                        injector.VerifyMethod(editorMethod);
                    }
                });
            }
        }

        [Test]
        public void ActionBuilderContextsResolve() {
            var actionClipService = CreateActionDebuggerInjector().GetService<ActionClipService>();
            foreach (var builderType in ReflectionUtils.GetTypes(typeof(IVRCFuryBuilder))) {
                var modelType = ReflectionUtils.GetGenericArgument(builderType, typeof(IVRCFuryBuilder<>));
                if (typeof(ActionBuilder).IsAssignableFrom(builderType)) {
                    Verify("Build method for " + builderType.Name, () => {
                        var injector = new VRCFuryInjector();
                        injector.Set(Activator.CreateInstance(modelType));
                        injector.Set("actionName", "Test action");
                        injector.Set("animObject", root.asVf());
                        injector.Set("debugMode", false);
                        injector.Set(actionClipService);
                        var buildMethod = builderType.VFMethod("Build");
                        injector.VerifyMethod(buildMethod);
                    });
                    Verify("BuildOff method for " + builderType.Name, () => {
                        var injector = new VRCFuryInjector();
                        injector.Set(Activator.CreateInstance(modelType));
                        injector.Set("animObject", root.asVf());
                        var buildMethod = builderType.VFMethod("BuildOff");
                        if (buildMethod != null) {
                            injector.VerifyMethod(buildMethod);
                        }
                    });
                }
            }
        }

        [Test]
        public void InjectorTypeConventionsHold() {
            var typesToScan =
                TypeCache.GetMethodsWithAttribute<FeatureBuilderActionAttribute>().Select(m => m.DeclaringType)
                    .Concat(TypeCache.GetTypesWithAttribute<VFServiceAttribute>())
                    .Concat(TypeCache.GetTypesDerivedFrom<FeatureBuilder>())
                    .Concat(TypeCache.GetTypesWithAttribute<VFPrototypeScopeAttribute>())
                    .Concat(TypeCache.GetTypesDerivedFrom<IVRCFuryBuilder>())
#if UNITY_2020_1_OR_NEWER
                    .Concat(TypeCache.GetFieldsWithAttribute<VFAutowiredAttribute>().Select(f => f.DeclaringType))
#endif
                    .ToImmutableHashSet();
            foreach (var type in typesToScan) {
                var hasBuilderAction = type.GetMethods()
                    .Any(m => m.GetCustomAttribute<FeatureBuilderActionAttribute>() != null);
                var isService = type.GetCustomAttribute<VFServiceAttribute>() != null;
                var isFeatureBuilder = typeof(FeatureBuilder).IsAssignableFrom(type);
                var isPrototypeService = type.GetCustomAttribute<VFPrototypeScopeAttribute>() != null;
                var isIBuilder = typeof(IVRCFuryBuilder).IsAssignableFrom(type);
                var hasAutowired = ReflectionUtils.GetAllFields(type)
                    .Any(field => field.GetCustomAttribute<VFAutowiredAttribute>() != null);
                if (hasAutowired) {
                    if (!isService && !isIBuilder) {
                        throw new Exception($"Autowired field found in non-service non-builder {type.Name}");
                    }
                }
                if (hasBuilderAction) {
                    if (!isService && !isFeatureBuilder) {
                        throw new Exception($"Feature builder action found in non-service non-builder {type.Name}");
                    }
                    if (isPrototypeService) {
                        throw new Exception($"Feature builder action found in prototype service {type.Name}");
                    }
                }
                if (isIBuilder && isService) {
                    throw new Exception($"IBuilder is also a service {type.Name}");
                }
            }
        }

        private static void Verify(string context, Action action) {
            try {
                action();
            } catch (Exception e) {
                throw new Exception("Failed to verify " + context, e);
            }
        }
    }
}
