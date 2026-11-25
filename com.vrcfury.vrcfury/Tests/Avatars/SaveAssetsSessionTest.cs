using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class SaveAssetsSessionTest {
        [Test]
        public void WritesSectionsWithoutAssetEntriesAndClearsThemAfterSaving() {
            var session = new SaveAssetsSession("WorkLogSections");
            var outputDir = (string)typeof(SaveAssetsSession)
                .GetField("outputDir", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(session);
            try {
                session.AddWorkLogSection("Build Step Timing", "* First" + Environment.NewLine + "* Second");
                session.AddWorkLogSection("Other", "Detail");
                session.WriteWorkLogManifest();
                session.WriteWorkLogManifest();
                var lines = File.ReadAllLines(Directory.GetFiles(outputDir, "*.txt").Single());
                Assert.That(lines.Where(line => line.Length > 0), Is.EqualTo(new[] {
                    "Build Step Timing:",
                    "* First",
                    "* Second",
                    "Other:",
                    "Detail"
                }));
            } finally {
                AssetDatabase.DeleteAsset(outputDir);
            }
        }

        [Test]
        public void WorkLogPreservesNamesOrderAndOwnershipAcrossFiles() {
            var session = new SaveAssetsSession("SaveAssetsSessionTest " + Guid.NewGuid().ToString("N"));
            var outputDir = (string)typeof(SaveAssetsSession)
                .GetField("outputDir", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(session);
            try {
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    var controller = VrcfObjectFactory.Create<AnimatorController>();
                    controller.name = "Controller";
                    var first = VrcfObjectFactory.Create<VRCExpressionParameters>();
                    first.name = "Z last";
                    first.WorkLog("First line\nSecond line");
                    var second = VrcfObjectFactory.Create<VRCExpressionParameters>();
                    second.name = "A first";
                    var separate = VrcfObjectFactory.Create<VRCExpressionParameters>();
                    separate.name = "Other file";
                    session.SaveControllerAndChildren(controller, Array.Empty<Object>(), new Object[] { first, second });
                    session.FlushAssets();
                    Assert.That(Directory.GetFiles(outputDir, "*.txt"), Is.Empty);
                    first.WorkLog("Updated during the second pass");
                    session.SaveAssetAndChildren(separate);
                    session.FlushAssets();
                    session.WriteWorkLogManifest();

                    var otherParent = ((Lazy<Object>)typeof(SaveAssetsSession)
                        .GetField("otherParent", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(session)).Value;
                    var expected = new StringBuilder();
                    foreach (var obj in new Object[] { controller, first, second, separate, otherParent }
                                 .OrderBy(obj => obj.GetPathAndName())) {
                        expected.AppendLine(obj.GetPathAndName() + ":");
                        foreach (var item in obj.GetWorkLog()) expected.AppendLine("* " + item);
                        expected.AppendLine();
                    }
                    expected.AppendLine();
                    var manifest = Directory.GetFiles(outputDir, "*.txt").Single();
                    Assert.That(File.ReadAllText(manifest), Is.EqualTo(expected.ToString()));
                    Assert.That(AssetDatabase.GetAssetPath(first), Is.EqualTo(AssetDatabase.GetAssetPath(controller)));
                    Assert.That(AssetDatabase.GetAssetPath(separate), Is.Not.EqualTo(AssetDatabase.GetAssetPath(controller)));

                    session.WriteWorkLogManifest();
                    Assert.That(Directory.GetFiles(outputDir, "*.txt"), Is.EquivalentTo(new[] { manifest }));
                }
                AssetDatabase.ImportAsset(outputDir, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
                var loadedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(outputDir + "/Controller.controller");
                Assert.That(loadedController, Is.Not.Null);
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(loadedController))
                    .OfType<VRCExpressionParameters>().Select(p => p.name), Is.EquivalentTo(new[] { "A first", "Z last" }));
            } finally {
                AssetDatabase.DeleteAsset(outputDir);
            }
        }
    }
}
