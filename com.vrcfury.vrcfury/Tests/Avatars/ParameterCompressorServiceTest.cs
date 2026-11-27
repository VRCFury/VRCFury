using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Service;
using VF.Service.Compressor;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class ParameterCompressorServiceTest {
        [TestCase(false)]
        [TestCase(true)]
        public void SavesChangedParametersAsANewSubasset(bool compress) {
            var root = new GameObject("Compress " + Guid.NewGuid().ToString("N"));
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            string outputDir = null;
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var expressions = injector.GetService<ExpressionParamsService>();
                var count = compress ? ExpressionParamsService.GetMaxCost() / 8 + 1 : 1;
                var parameters = Enumerable.Range(0, count)
                    .Select(i => ExpressionParam.Create("Toggle" + i).Int().Synced()).ToArray();
                expressions.SetParameters(parameters);
                menu.controls = parameters.Select(parameter => new VRCExpressionsMenu.Control {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter.name }
                }).ToList();
                avatar.expressionsMenu = menu;

                var save = injector.GetService<SaveAssetsService>();
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    save.Run(Array.Empty<VFAvatarController>());
                }
                var original = avatar.expressionParameters;
                var originalPath = AssetDatabase.GetAssetPath(original);
                outputDir = VRCFuryAssetDatabase.GetDirectoryName(originalPath);
                var originalBytes = File.ReadAllBytes(originalPath);

                injector.GetService<ControllersService>().ClearCache(applyBaseMask: false);
                expressions.ClearCache();
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    injector.GetService<ParameterCompressorService>().Apply();
                }

                Assert.That(original.parameters.All(parameter => parameter.IsNetworkSynced()), Is.True);
                if (!compress) {
                    Assert.That(avatar.expressionParameters, Is.SameAs(original));
                    Assert.That(File.ReadAllBytes(originalPath), Is.EqualTo(originalBytes));
                    return;
                }

                Assert.That(avatar.expressionParameters, Is.Not.SameAs(original));
                Assert.That(AssetDatabase.GetAssetPath(avatar.expressionParameters), Is.EqualTo(originalPath));
                Assert.That(File.ReadAllBytes(originalPath), Is.Not.EqualTo(originalBytes));
                AssetDatabase.ImportAsset(originalPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var saved = AssetDatabase.LoadAllAssetsAtPath(originalPath).OfType<VRCExpressionParameters>()
                    .Single(asset => asset == avatar.expressionParameters);
                Assert.That(saved.CalcTotalCost(), Is.LessThanOrEqualTo(ExpressionParamsService.GetMaxCost()));
                Assert.That(saved.parameters.Any(parameter => !parameter.IsNetworkSynced()), Is.True);
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(menu);
                if (outputDir != null) AssetDatabase.DeleteAsset(outputDir);
            }
        }
    }
}
