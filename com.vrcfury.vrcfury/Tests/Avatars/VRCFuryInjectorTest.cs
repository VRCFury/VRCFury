using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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
        private class InjectorConsumer {
            public readonly VRCFuryInjector injector;
            public InjectorConsumer(VRCFuryInjector injector) {
                this.injector = injector;
            }
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
                injector.Set("avatarObject", null);
                injector.Set("componentObject", null);
                injector.Set(typeof(GlobalsService), null);
                injector.Set(typeof(VRCAvatarDescriptor), null);
                injector.GetServices<object>();
            });
        }

        [Test]
        public void ActionDebuggerContextResolves() {
            Verify("action debugger context", () => {
                var injector = new VRCFuryInjector();
                injector.ImportOne(typeof(ActionClipService));
                injector.ImportOne(typeof(ClipFactoryService));
                injector.ImportOne(typeof(VRCFObjectPathCache));
                injector.ImportScan(typeof(ActionBuilder));
                injector.Set("avatarObject", null);
                injector.Set("componentObject", null);
                injector.GetService<ActionClipService>();
            });
        }

        [Test]
        public void PerFrameSpsInspectorContextResolves() {
            Verify("per-frame SPS inspector context", () => {
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                injector.Set("avatarObject", null);
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
                    injector.Set(modelType, null);
                    injector.Set(typeof(SerializedProperty), null);
                    injector.Set("avatarObject", null);
                    injector.Set("componentObject", null);
                    injector.VerifyMethod(editorMethod);
                });
            }
        }

        [Test]
        public void ActionBuilderContextsResolve() {
            foreach (var builderType in ReflectionUtils.GetTypes(typeof(IVRCFuryBuilder))) {
                var modelType = ReflectionUtils.GetGenericArgument(builderType, typeof(IVRCFuryBuilder<>));
                if (typeof(ActionBuilder).IsAssignableFrom(builderType)) {
                    Verify("Build method for " + builderType.Name, () => {
                        var injector = new VRCFuryInjector();
                        injector.Set(Activator.CreateInstance(modelType));
                        injector.Set("actionName", null);
                        injector.Set("animObject", null);
                        injector.Set("debugMode", false);
                        injector.Set(typeof(ActionClipService), null);
                        var buildMethod = builderType.VFMethod("Build");
                        injector.VerifyMethod(buildMethod);
                    });
                    Verify("BuildOff method for " + builderType.Name, () => {
                        var injector = new VRCFuryInjector();
                        injector.Set(Activator.CreateInstance(modelType));
                        injector.Set("animObject", null);
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
