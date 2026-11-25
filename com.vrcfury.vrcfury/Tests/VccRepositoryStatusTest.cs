using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using VF.Utils;

namespace VF.Tests {
    internal class VccRepositoryStatusTest {
        private const string Repo = "\"userRepos\":[{\"id\":\"com.vrcfury.vcc\",\"url\":\"https://vcc.vrcfury.com/\"}]";
        private string directory;
        private string settings;

        [SetUp]
        public void SetUp() {
            directory = Path.GetFullPath(Path.Combine("Temp", "VccRepositoryTest-" + Guid.NewGuid().ToString("N")));
            settings = Directory.CreateDirectory(Path.Combine(directory, "VRChatCreatorCompanion")).FullName;
        }

        [TearDown]
        public void TearDown() => Directory.Delete(directory, recursive: true);

        [TestCase("{}", "missing")]
        [TestCase("{\"userRepos\":[{\"id\":\"another.repo\"}]}", "missing")]
        [TestCase("{" + Repo + "}", null)]
        [TestCase("{\"userRepos\":[{\"url\":\"https://vcc.vrcfury.com\"}]}", null)]
        public void ReportsRepositoryStatusWithoutWritingSettings(string json, string issue) {
            File.WriteAllText(Path.Combine(settings, "settings_extended.json"), "{}");
            var path = Path.Combine(settings, "settings.json");
            File.WriteAllText(path, json);
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(issue == null ? null : Warning("VCC", issue)));
            Assert.That(File.ReadAllText(path), Is.EqualTo(json));
        }

        [TestCase(null)]
        [TestCase("{")]
        [TestCase("{\"userRepos\":{}}")]
        public void MissingOrUnreadableSettingsDoNotWarn(string json) {
            File.WriteAllText(Path.Combine(settings, "settings_extended.json"), "{}");
            if (json != null) File.WriteAllText(Path.Combine(settings, "settings.json"), json);
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
        }

        [Test]
        public void SharedSettingsAloneDoNotWarn() {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{}");
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
            WriteAlcomConfig("{}");
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("ALCOM", "missing")));
        }

        [Test]
        public void AlcomConfigAloneDoesNotWarn() {
            WriteAlcomConfig("{}");
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
        }

        [Test]
        public void ExtendedVccSettingsUseTheSharedRepositoryList() {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{" + Repo + "}");
            var path = Path.Combine(settings, "settings_extended.json");
            File.WriteAllText(path, "{\"enabledRepos\":[]}");
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "disabled")));
            File.WriteAllText(path, "{\"enabledRepos\":[\"com.vrcfury.vcc\"]}");
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
        }

        [Test]
        public void IgnoresNewerBeta2Settings() {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{" + Repo + "}");
            var extended = Path.Combine(settings, "settings_extended.json");
            var v2 = Path.Combine(settings, "settings_v2.json");
            File.WriteAllText(extended, "{\"enabledRepos\":[]}");
            File.WriteAllText(v2, "{" + Repo + "}");
            File.SetLastWriteTimeUtc(extended, DateTime.UtcNow.AddMinutes(-1));
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "disabled")));
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ChecksOnlyTheNewestApp(bool alcomIsNewer, bool newerEnabled) {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{" + Repo + "}");
            var alcomEnabled = alcomIsNewer == newerEnabled;
            var alcom = WriteAlcomConfig(alcomEnabled ? "{}" : "{\"guiHiddenRepositories\":[\"com.vrcfury.vcc\"]}");
            var vcc = WriteLegacySelection(alcomEnabled ? "[]" : "[\"com.vrcfury.vcc\"]");
            File.SetLastWriteTimeUtc(alcomIsNewer ? vcc : alcom, DateTime.UtcNow.AddMinutes(-1));
            var expected = newerEnabled ? null : Warning(alcomIsNewer ? "ALCOM" : "VCC", "disabled");
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(expected));
        }

        [Test]
        public void UsesTheNewestAppsCheckbox() {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{" + Repo + "}");
            var vcc = Path.Combine(settings, "settings_extended.json");
            File.WriteAllText(vcc, "{\"disabledRepos\":[\"com.vrcfury.vcc\"]}");
            var alcom = WriteAlcomConfig("{}");
            File.SetLastWriteTimeUtc(vcc, DateTime.UtcNow.AddMinutes(-1));
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
            File.SetLastWriteTimeUtc(alcom, DateTime.UtcNow.AddMinutes(-2));
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "disabled")));
        }

        [Test]
        public void DoesNotFallBackWhenTheNewestSettingsAreUnreadable() {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{}");
            var vcc = Path.Combine(settings, "settings_extended.json");
            File.WriteAllText(vcc, "{}");
            WriteAlcomConfig("{");
            File.SetLastWriteTimeUtc(vcc, DateTime.UtcNow.AddMinutes(-1));
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
        }

        [TestCase("[\"Curated\",\"com.vrcfury.vcc\"]", null)]
        [TestCase("[\"Curated\",\"https://vcc.vrcfury.com/\"]", null)]
        [TestCase("[\"Curated\"]", "disabled")]
        [TestCase(null, null)]
        public void ReadsTheLegacyCheckboxAtTheDefaultPort(string selection, string issue) {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{" + Repo + "}");
            WriteLegacySelection(selection);
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(issue == null ? null : Warning("VCC", issue)));
        }

        [Test]
        public void MissingLegacyRepositoryStillWarnsWithoutACheckbox() {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{}");
            WriteLegacySelection(null);
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "missing")));
        }

        [Test]
        public void NewerLegacyCheckboxWinsOverAnOlderVccConfig() {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{" + Repo + "}");
            var config = Path.Combine(settings, "settings_extended.json");
            File.WriteAllText(config, "{}");
            var log = WriteLegacySelection("[]");
            File.SetLastWriteTimeUtc(config, File.GetLastWriteTimeUtc(log).AddMinutes(-1));
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "disabled")));
            File.SetLastWriteTimeUtc(config, File.GetLastWriteTimeUtc(log).AddMinutes(1));
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
        }

        [Test]
        public void ReusesLegacyResultUntilSelectedFilesChange() {
            var shared = Path.Combine(settings, "settings.json");
            File.WriteAllText(shared, "{" + Repo + "}");
            var log = WriteLegacySelection("[]");
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "disabled")));
            using (File.Open(log, FileMode.Open, FileAccess.Read, FileShare.None)) {
                Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "disabled")));
                File.WriteAllText(shared, "{}");
                File.SetLastWriteTimeUtc(shared, DateTime.UtcNow.AddMinutes(1));
                Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "missing")));
            }
            File.WriteAllText(shared, "{" + Repo + "}");
            File.SetLastWriteTimeUtc(shared, DateTime.UtcNow.AddMinutes(2));
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.EqualTo(Warning("VCC", "disabled")));
            File.Delete(log);
            WriteLegacySelection("[\"com.vrcfury.vcc\"]");
            File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddMinutes(3));
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
        }

        [Test]
        public void IgnoresAnAlternatePortEvenWhenVccLogsMentionIt() {
            File.WriteAllText(Path.Combine(settings, "settings.json"), "{" + Repo + "}");
            var logs = Directory.CreateDirectory(Path.Combine(settings, "Logs"));
            File.WriteAllText(Path.Combine(logs.FullName, "vcc-log.txt"), "Starting static server at http://localhost:5480/");
            WriteLegacySelection("[]", port: 5480);
            Assert.That(VccRepositoryStatus.ReadWarning(directory), Is.Null);
        }

        private string WriteAlcomConfig(string json) {
            var config = Directory.CreateDirectory(Path.Combine(settings, "vrc-get"));
            var path = Path.Combine(config.FullName, "gui-config.json");
            File.WriteAllText(path, json);
            return path;
        }

        private static string Warning(string app, string issue) =>
            $"VRCFury is {issue} in your {app} repository settings. " +
            "You will not receive updates until this is fixed. The repository URL is vcc.vrcfury.com";

        private string WriteLegacySelection(string selection, int port = 5476) {
            var storage = Directory.CreateDirectory(Path.Combine(
                directory,
                "Photino",
                "EBWebView",
                "Default",
                "Local Storage",
                "leveldb"
            )).FullName;
            File.WriteAllText(Path.Combine(storage, "CURRENT"), "MANIFEST-000001\n");
            File.WriteAllBytes(Path.Combine(storage, "MANIFEST-000001"), LogRecord(new byte[] { 2, 3 }));
            var key = Encoding.ASCII.GetBytes($"_http://localhost:{port}\0\u0001selectedRepos");
            var path = Path.Combine(storage, "000003.log");
            using var stream = new FileStream(path, FileMode.CreateNew);
            void WriteSelection(ulong sequence, string state) {
                using var batch = new MemoryStream();
                using var writer = new BinaryWriter(batch);
                writer.Write(sequence);
                writer.Write(1);
                writer.Write((byte)(state == null ? 0 : 1));
                writer.Write((byte)key.Length);
                writer.Write(key);
                if (state != null) {
                    var value = Encoding.ASCII.GetBytes("\u0001" + state);
                    writer.Write((byte)value.Length);
                    writer.Write(value);
                }
                var record = LogRecord(batch.ToArray());
                stream.Write(record, 0, record.Length);
            }
            WriteSelection(20, "[\"com.vrcfury.vcc\"]");
            WriteSelection(21, selection);
            return path;
        }

        private static byte[] LogRecord(byte[] data) {
            const byte fullRecord = 1;
            var crc = uint.MaxValue;
            void AddByte(byte value) {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0x82f63b78U : 0);
            }
            AddByte(fullRecord);
            foreach (var value in data) AddByte(value);
            crc = ~crc;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(unchecked(((crc >> 15) | (crc << 17)) + 0xa282ead8U));
            writer.Write((ushort)data.Length);
            writer.Write(fullRecord);
            writer.Write(data);
            return stream.ToArray();
        }
    }
}
