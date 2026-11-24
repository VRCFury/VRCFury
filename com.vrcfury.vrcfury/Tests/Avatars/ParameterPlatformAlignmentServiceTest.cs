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

    internal class ParameterPlatformAlignmentServiceTest : ParameterPlatformBuildTestBase {
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
