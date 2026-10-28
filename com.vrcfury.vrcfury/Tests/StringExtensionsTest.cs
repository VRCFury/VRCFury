using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using VF.Utils;

namespace VF.Tests {
    [Category("VRCFury")]
    public class StringExtensionsTest {
        [TestCase("Name", "Name")]
        [TestCase("Taken", "Taken_3")]
        [TestCase("Taken_2", "Taken_2_2")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void MakesNamesUniqueWithoutChangingTheCollection(string input, string expected) {
            var existing = new[] { "Taken", "Taken_2" };
            Assert.That(input.MakeUnique(existing), Is.EqualTo(expected));
            Assert.That(existing, Is.EqualTo(new[] { "Taken", "Taken_2" }));
        }

        [Test]
        public void SupportsLazyCollections() {
            Assert.That("Name".MakeUnique(Enumerable.Range(1, 3).Select(i => i == 1 ? "Name" : "Name_" + i)), Is.EqualTo("Name_4"));
        }

        [Test]
        public void PreservesTheCollectionsComparer() {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NAME", "name_2" };
            Assert.That("Name".MakeUnique(existing), Is.EqualTo("Name_3"));
            Assert.That(existing, Has.Count.EqualTo(2));
        }

        [Test]
        public void TruncatesTheBaseToLeaveRoomForTheSuffix() {
            var existing = new[] { "LongName", "LongNa_2", "LongNa_3" };
            Assert.That("LongNameThatIsTooLong".MakeUnique(existing, 8), Is.EqualTo("LongNa_4"));
            Assert.That("OtherLongName".MakeUnique(existing, 8), Is.EqualTo("OtherLon"));
        }

        [Test]
        public void SuffixesCanGrowToMultipleDigits() {
            var existing = new HashSet<string> { "LongName" };
            for (var i = 2; i <= 10; i++) existing.Add("LongName".MakeUnique(existing, 8));
            Assert.That(existing, Does.Contain("LongN_10"));
            Assert.That("LongName".MakeUnique(existing, 8), Is.EqualTo("LongN_11"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsInvalidMaximumLengths(int maxLength) {
            Assert.Throws<ArgumentOutOfRangeException>(() => "Name".MakeUnique(Array.Empty<string>(), maxLength));
        }

        [Test]
        public void ReportsWhenTheSuffixCannotFit() {
            Assert.That("Name".MakeUnique(Array.Empty<string>(), 1), Is.EqualTo("N"));
            Assert.Throws<InvalidOperationException>(() => "Name".MakeUnique(new[] { "N" }, 1));
        }

        [Test]
        public void SupportsAvailabilityLookups() {
            Assert.That("Name".MakeUnique(candidate => candidate == "Name" || candidate == "Name_2"), Is.EqualTo("Name_3"));
        }

        [Test]
        public void UniqueAssetPathsAvoidBothFilesAndDirectories() {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try {
                File.WriteAllText(Path.Combine(dir, "Name.asset"), "");
                Directory.CreateDirectory(Path.Combine(dir, "Name_2.asset"));
                Assert.That(VRCFuryAssetDatabase.GetUniquePath(dir, "Name", "asset"), Is.EqualTo(dir + "/Name_3.asset"));
            } finally {
                File.Delete(Path.Combine(dir, "Name.asset"));
                Directory.Delete(Path.Combine(dir, "Name_2.asset"));
                Directory.Delete(dir);
            }
        }
    }
}
