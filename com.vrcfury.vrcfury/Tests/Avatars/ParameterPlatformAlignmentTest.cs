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

namespace VF.Tests {
    [Category("VRCFury")]
    public class ParameterPlatformAlignmentTest {
        private class Build : IDisposable {
            public readonly GameObject root;
            public readonly VRCFuryInjector injector;
            public readonly ExpressionParamsService expressions;
            public readonly ParameterPlatformAlignmentService alignment;
            public readonly VFAvatarController fx;

            public Build(string rootName, string blueprint = "avtr-example") {
                root = new GameObject(rootName);
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var pipeline = root.GetComponent<PipelineManager>() ?? root.AddComponent<PipelineManager>();
                pipeline.blueprintId = blueprint;
                injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                fx = controllers.GetFx();
                expressions = injector.GetService<ExpressionParamsService>();
                alignment = injector.GetService<ParameterPlatformAlignmentService>();
            }

            public string Add(string path, string name, bool synced = true) {
                VFGameObject owner = root;
                foreach (var part in path.Split('/').Where(part => part.Length > 0)) {
                    owner = owner.Find(part) ?? GameObjects.Create(part, owner);
                }
                var param = fx.GetOrCreateBool(injector.GetService<UniqueParamService>().GetObjectScopedName(owner, name));
                param.MakeExpressionBool(synced: synced);
                return param.name;
            }

            public OptimizationDecision Align(ParameterPlatformAlignmentService.SavedData desktop) {
                var parameters = expressions.GetSnapshot().ToList();
                var result = ParameterPlatformAlignmentService.AlignForMobile(parameters, desktop);
                expressions.SetParameters(parameters);
                return result.decision;
            }

            public void Dispose() => Object.DestroyImmediate(root);
        }

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
        public void GestureLockNamesSurviveOmittedAndSharedGestures() {
            using var desktop = new Build("Desktop");
            using var mobile = new Build("Mobile");
            string WaveParameter(Build build, params string[] locks) {
                build.injector.Set("componentObject", new Func<VFGameObject>(() => build.root));
                var builder = build.injector.CreateAndFillObject<VF.Feature.GestureDriverBuilder>();
                builder.featureBaseObject = build.root;
                builder.model = new VF.Model.Feature.GestureDriver();
                foreach (var path in locks) {
                    builder.model.gestures.Add(new VF.Model.Feature.GestureDriver.Gesture {
                        enableLockMenuItem = true,
                        lockMenuItem = path
                    });
                }
                builder.Apply();
                var menu = build.injector.GetService<MenuService>().GetReadOnlyMenu();
                return menu.controls.Single(c => c.name == "Wave").parameter.name;
            }
            Assert.That(WaveParameter(mobile, "Wave"), Is.EqualTo(WaveParameter(desktop, "Smile", "Wave", "Wave")));
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

        [Test]
        public void CompressedDesktopSlotDoesNotDriveAnExplicitlyLocalMobileParameter() {
            using var pc = new Build("Desktop");
            using var mobile = new Build("Mobile");
            var name = pc.Add("Prop", "Local");
            mobile.Add("Prop", "Local", synced: false);
            var parameters = pc.expressions.GetSnapshot();
            var decision = new OptimizationDecision { boolSlots = 1, compress = parameters };
            var plan = mobile.Align(pc.alignment.CaptureDesktopData(decision, parameters));
            Assert.That(plan.compress.Single().name, Does.StartWith("__missing_param_from_desktop_"));
            Assert.That(mobile.expressions.Single(p => p.name == name).IsNetworkSynced(), Is.False);
        }

#if VRCSDK_HAS_NAME_BASED_PARAMETER_SYNC
        [TestCase(false)]
        [TestCase(true)]
        public void MobileOnlyParametersWithAnEmptyDesktopPlanRespectTheBudget(bool overBudget) {
            using var pc = new Build("Desktop");
            using var mobile = new Build("Mobile");
            var limit = ExpressionParamsService.GetMaxCost();
            var count = overBudget ? limit : limit - 1;
            for (int i = 0; i < count; i++) {
                pc.fx.GetOrCreateBool("Shared" + i).MakeExpressionBool();
                mobile.fx.GetOrCreateBool("Shared" + i).MakeExpressionBool();
            }
            mobile.fx.GetOrCreateBool("Extra").MakeExpressionBool();
            var data = pc.alignment.CaptureDesktopData(new OptimizationDecision(), pc.expressions.GetSnapshot());
            var plan = mobile.Align(data);
            Assert.That(plan.compress, Is.Empty);
            Assert.That(mobile.expressions.Single(p => p.name == "Extra").IsNetworkSynced(), Is.EqualTo(!overBudget));
            Assert.That(mobile.expressions.CalcTotalCost(), Is.EqualTo(limit));
        }
#endif

        [Test]
        public void MobileUsesFinalNamesAndPreservesDesktopCompressorSlots() {
            using var pc = new Build("Desktop");
            using var mobile = new Build("Mobile");
            var first = pc.Add("Props/Hat", "Enabled");
            var absent = pc.Add("PC", "Toggle");
            var global = pc.injector.GetService<UniqueParamService>().GetAvatarWideName("Sync");
            pc.fx.GetOrCreateBool(global).MakeExpressionBool();
            pc.fx.GetOrCreateBool("Authored").MakeExpressionBool();

            var extra = mobile.Add("Quest", "Toggle");
            mobile.fx.GetOrCreateBool("Authored").MakeExpressionBool();
            Assert.That(mobile.Add("Props/Hat", "Enabled"), Is.EqualTo(first));
            Assert.That(mobile.injector.GetService<UniqueParamService>().GetAvatarWideName("Sync"), Is.EqualTo(global));
            mobile.fx.GetOrCreateBool(global).MakeExpressionBool();

            var decision = new OptimizationDecision {
                boolSlots = 2,
                compress = pc.expressions.GetSnapshot().Reverse().ToList()
            };
            var data = pc.alignment.CaptureDesktopData(decision, pc.expressions.GetSnapshot());
            var saved = JsonUtility.FromJson<ParameterPlatformAlignmentService.SavedData>(JsonUtility.ToJson(data));
            var plan = mobile.Align(saved);
            var filler = plan.compress[2].name;
            Assert.That(filler, Does.StartWith("__missing_param_from_desktop_"));
            Assert.That(plan.boolSlots, Is.EqualTo(2));
            Assert.That(plan.compress.Select(p => p.name), Is.EqualTo(new[] { "Authored", global, filler, first }));
            var result = mobile.expressions.GetSnapshot();
            Assert.That(result.Any(p => p.name == absent), Is.False);
#if VRCSDK_HAS_NAME_BASED_PARAMETER_SYNC
            Assert.That(result.Single(p => p.name == extra).IsNetworkSynced(), Is.False);
            Assert.That(result.Single(p => p.name == filler).IsNetworkSynced(), Is.False);
#else
            Assert.That(result.Select(p => p.name), Is.EqualTo(new[] { first, filler, global, "Authored", extra }));
            Assert.That(result.Single(p => p.name == filler).IsNetworkSynced(), Is.True);
            Assert.That(result.Single(p => p.name == extra).IsNetworkSynced(), Is.False);
#endif
        }
    }
}
