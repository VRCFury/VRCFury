using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VF.Hooks.UnityFixes;
using VF.Model;
using VF.Utils;

namespace VF.Tests {
    internal class FixMissingScriptsHookTest {
        [Test]
        public void InstalledComponentScriptsAreHealthy() {
            Assert.That(FixMissingScriptsHook.GetBrokenScripts(), Is.Empty);
        }

        [Test]
        public void HealthyScriptsDoNotConsumeARepairAttempt() {
            var path = AssetDatabase.GUIDToAssetPath("d9e94e501a2d4c95bff3d5601013d923");
            var guid = AssetDatabase.AssetPathToGUID(path);
            var attempted = "com.vrcfury.scriptImportRepair." + guid;
            var previous = SessionState.GetBool(attempted, false);
            try {
                SessionState.EraseBool(attempted);
                Assert.That(FixMissingScriptsHook.TryRepair(path, typeof(VRCFury)), Is.False);
                Assert.That(SessionState.GetBool(attempted, false), Is.False);
            } finally {
                SessionState.SetBool(attempted, previous);
            }
        }

        [Test]
        public void CleanCompilationCanBeRetriedForNewFailuresOrAfterAHealthyCheck() {
            var guid = "d9e94e501a2d4c95bff3d5601013d923";
            var otherGuid = Guid.NewGuid().ToString("N");
            var attempted = "com.vrcfury.scriptImportRepair." + guid;
            var cleanCompilation = attempted + ".cleanCompilation";
            var previousAttempt = SessionState.GetBool(attempted, false);
            var previousCompilation = SessionState.GetBool(cleanCompilation, false);
            try {
                SessionState.EraseBool(cleanCompilation);
                Assert.That(FixMissingScriptsHook.TryMarkCleanCompilation(guid), Is.True);
                Assert.That(FixMissingScriptsHook.TryMarkCleanCompilation(guid), Is.False);
                Assert.That(FixMissingScriptsHook.TryMarkCleanCompilation(otherGuid), Is.True);

                SessionState.SetBool(attempted, true);
                Assert.That(FixMissingScriptsHook.GetBrokenScripts(), Is.Empty);
                Assert.That(SessionState.GetBool(attempted, false), Is.False);
                Assert.That(FixMissingScriptsHook.TryMarkCleanCompilation(guid), Is.True);
                Assert.That(FixMissingScriptsHook.TryMarkCleanCompilation(otherGuid), Is.False);
            } finally {
                SessionState.SetBool(attempted, previousAttempt);
                SessionState.SetBool(cleanCompilation, previousCompilation);
                SessionState.EraseBool("com.vrcfury.scriptImportRepair." + otherGuid + ".cleanCompilation");
            }
        }

        [TestCase(typeof(VRCFury))]
        [TestCase(typeof(BinaryContainer))]
        public void FailedImportRepairIsNotRepeatedWithoutARecompile(Type type) {
            var folder = "Assets/VRCFuryScriptImportTest-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            var path = folder + "/" + type.Name + ".cs";
            const string source = "// Represents unavailable compiler type information.\n";
            File.WriteAllText(path, source);
            try {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Assert.That(
                    FixMissingScriptsHook.GetBrokenScripts(folder + "/ExpectedAssembly.asmdef", type.Assembly),
                    Is.Empty,
                    "A matching filename alone must not make a script from a different assembly eligible for repair."
                );

                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("Could not repair Unity's script import for " + Regex.Escape(path))
                );
                Assert.That(FixMissingScriptsHook.TryRepair(path, type), Is.False);
                // The guard must live in SessionState so it survives the reload requested after a repair.
                var attempted = "com.vrcfury.scriptImportRepair." + AssetDatabase.AssetPathToGUID(path);
                Assert.That(SessionState.GetBool(attempted, false), Is.True);
                Assert.That(FixMissingScriptsHook.TryRepair(path, type), Is.False);
                LogAssert.NoUnexpectedReceived();
                Assert.That(
                    File.ReadAllText(path),
                    Is.EqualTo(source),
                    "Recovery must not rewrite the script source."
                );
            } finally {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase("Assets/ScriptImportRecoveryTest.txt")]
        [TestCase("Library/Artifacts-other/ScriptImportRecoveryTest.txt")]
        [TestCase("Library/Artifacts/../ScriptImportRecoveryTest.txt")]
        public void RejectsPathsOutsideArtifactDirectoryBeforeDeletingAnything(string outside) {
            var folder = CreateArtifactTestFolder();
            var artifact = folder + "/artifact";
            File.WriteAllText(artifact, "Keep this when another output is invalid.");
            try {
                Assert.Throws<Exception>(() => FixMissingScriptsHook.RemoveArtifacts(new[] { artifact, outside }));
                Assert.That(File.Exists(artifact), Is.True);
            } finally {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void RemovesOnlyTheSpecifiedArtifact() {
            var folder = CreateArtifactTestFolder();
            var first = folder + "/first";
            var second = folder + "/second";
            File.WriteAllText(first, "Damaged derived output.");
            File.WriteAllText(second, "Unrelated derived output.");
            try {
                FixMissingScriptsHook.RemoveArtifacts(new[] { first });
                Assert.That(File.Exists(first), Is.False);
                Assert.That(File.Exists(second), Is.True);
            } finally {
                Directory.Delete(folder, true);
            }
        }

        private static string CreateArtifactTestFolder() {
            // Unity's packed data store does not create Library/Artifacts.
            var folder = "Library/Artifacts/ScriptImportRecoveryTest-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
