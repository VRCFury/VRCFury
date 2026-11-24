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

    internal class ExpressionParamsServiceTest : ExpressionParameterTestBase {
        [Test]
        public void ExpressionServiceEnumeratesOwnedParametersAndSnapshotsDoNotMutateImportedAssets() {
            var root = new GameObject("Expression iteration");
            var imported = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var service = GetExpressionParams(root);
                var avatar = root.GetComponent<VRCAvatarDescriptor>();
                var local = new VRCExpressionParameters.Parameter { name = "Local", valueType = VRCExpressionParameters.ValueType.Int };
                local.SetNetworkSynced(false);
                imported.parameters = new[] {
                    new VRCExpressionParameters.Parameter { name = "Enabled", valueType = VRCExpressionParameters.ValueType.Bool },
                    new VRCExpressionParameters.Parameter { name = "Depth", valueType = VRCExpressionParameters.ValueType.Float },
                    new VRCExpressionParameters.Parameter { name = "", valueType = VRCExpressionParameters.ValueType.Int },
                    local
                };
                avatar.customExpressions = true;
                avatar.expressionParameters = imported;
                var snapshot = service.GetSnapshot();
                Assert.That(service.CalcTotalCost(), Is.EqualTo(imported.CalcTotalCost()));
                Assert.That(avatar.expressionParameters, Is.SameAs(imported));
                snapshot[0].defaultValue = 1;
                Assert.That(imported.parameters[0].defaultValue, Is.Zero);
                var owned = service.ToArray();
                owned[0].defaultValue = 1;
                Assert.That(avatar.expressionParameters, Is.Not.SameAs(imported));
                Assert.That(imported.parameters[0].defaultValue, Is.Zero);
                service.SetParameters(owned.Reverse());
                Assert.That(((System.Collections.IEnumerable)service).Cast<VRCExpressionParameters.Parameter>(),
                    Is.EqualTo(owned.Reverse()));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(imported);
            }
        }

        [Test]
        public void CompressorSnapshotsPreserveDuplicateEntriesForSdkCostAccounting() {
            var root = new GameObject("Duplicate expression parameters");
            var imported = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var service = GetExpressionParams(root);
                var avatar = root.GetComponent<VRCAvatarDescriptor>();
                imported.parameters = Enumerable.Range(0, 33).Select(_ => new VRCExpressionParameters.Parameter {
                    name = "DuplicateInt", valueType = VRCExpressionParameters.ValueType.Int
                }).ToArray();
                avatar.customExpressions = true;
                avatar.expressionParameters = imported;

                var originalCost = service.CalcTotalCost();
                var snapshot = service.GetSnapshot();
                Assert.That(originalCost, Is.EqualTo(264));
                Assert.That(snapshot, Has.Length.EqualTo(33));
                var decision = new OptimizationDecision { compress = snapshot };
                decision.Optimize(originalCost);
                Assert.That(decision.GetFinalCost(originalCost), Is.LessThanOrEqualTo(ExpressionParamsService.GetMaxCost()));
                Assert.That(avatar.expressionParameters, Is.SameAs(imported));
                Assert.That(imported.parameters, Has.Length.EqualTo(33));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(imported);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ExpressionMergeKeepsTheFirstDefaultSavedAndSyncedValues(bool changeSaved, bool changeSync) {
            var root = new GameObject("Expression merge settings");
            try {
                var service = GetExpressionParams(root);
                var original = service.AddOrMerge(ExpressionParam.Create("Shared").Float().Default(0.75f).Saved());
                var input = original.Clone();
                input.defaultValue = 0;
                if (changeSaved) input.saved = false;
                if (changeSync) input.Unsynced();
                var before = JsonUtility.ToJson(original);
                Assert.That(service.AddOrMerge(input), Is.SameAs(original));
                Assert.That(JsonUtility.ToJson(original), Is.EqualTo(before));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MergeKeepsTheFirstDefaultAndReturnsTheOwnedParameter() {
            var root = new GameObject("Parameter creation");
            var imported = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                imported.parameters = new[] { new VRCExpressionParameters.Parameter {
                    name = "Imported", valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.75f, saved = true
                } };
                avatar.customExpressions = true;
                avatar.expressionParameters = imported;
                var service = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ExpressionParamsService>();
                var input = imported.parameters[0].Clone();
                input.defaultValue = 0;
                var existing = service.AddOrMerge(input);
                Assert.That(existing.saved, Is.True);
                Assert.That(existing.defaultValue, Is.EqualTo(0.75f));
                Assert.That(existing, Is.Not.SameAs(imported.parameters[0]));
                var created = service.AddOrMerge(new VRCExpressionParameters.Parameter {
                    name = "Created", valueType = VRCExpressionParameters.ValueType.Int, saved = false
                });
                Assert.That(created.name, Is.EqualTo("Created"));
                Assert.That(service.AddOrMerge(created.Clone()), Is.SameAs(created));
                Assert.That(service.ToArray(), Has.Length.EqualTo(2));
                Assert.That(imported.parameters, Has.Length.EqualTo(1));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(imported);
            }
        }

#if VRCSDK_HAS_PARAMETER_INTERPOLATION
        [TestCase(VRCExpressionParameters.ValueType.Int)]
        [TestCase(VRCExpressionParameters.ValueType.Float)]
        public void InterpolationMergingRejectsConflictsWithoutChangingExistingDurations(VRCExpressionParameters.ValueType type) {
            var root = new GameObject("Interpolation merging");
            try {
                var paramz = GetExpressionParams(root);
                var original = new VRCExpressionParameters.Parameter { name = "Test", valueType = type };
                paramz.SetParameters(new[] { original });
                var authored = original.Clone();
                authored.useCustomLerpDuration = true;
                authored.customLerpDuration = 0.5f;
                Assert.That(original.IsSameAs(authored), Is.False);
                Assert.That(Assert.Throws<Exception>(() => paramz.AddOrMerge(authored)).Message,
                    Does.Contain("Test").And.Contain("interpolation"));
                Assert.That(original.useCustomLerpDuration, Is.False);
                Assert.That(original.customLerpDuration, Is.Zero);
                original.useCustomLerpDuration = true;
                original.customLerpDuration = 0.5f;
                paramz.AddOrMerge(authored);
                Assert.That(original.IsSameAs(authored), Is.True);
                Assert.That(authored.Clone().IsSameAs(authored), Is.True);
                Assert.That(Assert.Throws<Exception>(() => paramz.AddOrMerge(
                    new VRCExpressionParameters.Parameter { name = "Test", valueType = type })).Message,
                    Does.Contain("Test").And.Contain("interpolation"));
                Assert.That(original.useCustomLerpDuration, Is.True);
                Assert.That(original.customLerpDuration, Is.EqualTo(0.5f));
                var conflict = authored.Clone();
                conflict.DisableInterpolation();
                Assert.That(conflict.IsSameAs(authored), Is.False);
                Assert.That(Assert.Throws<Exception>(() => paramz.AddOrMerge(conflict)).Message,
                    Does.Contain("Test").And.Contain("interpolation"));
                Assert.That(original.customLerpDuration, Is.EqualTo(0.5f));
                original.useCustomLerpDuration = false;
                authored.useCustomLerpDuration = false;
                original.customLerpDuration = 5;
                Assert.That(original.IsSameAs(authored), Is.True, "Inactive durations do not affect behavior");
                paramz.AddOrMerge(authored);
                Assert.That(original.customLerpDuration, Is.EqualTo(5));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
#endif
    }
}
