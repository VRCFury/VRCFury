using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    internal class VRCFuryAssetDatabaseTest {
        [Test]
        public void EmptyFolderCleanupPreservesPendingAssets() {
            var folder = AssetDatabase.GenerateUniqueAssetPath("Assets/VF Pending File Test");
            var path = folder + "/Pending.anim";
            var clip = new AnimationClip();
            try {
                VRCFuryAssetDatabase.CreateFolder(folder);
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    VRCFuryAssetDatabase.SaveAsset(clip, path);
                    VRCFuryAssetDatabase.DeleteDirIfEmpty(folder);
                    Assert.That(File.Exists(path), Is.True);
                }

                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.SameAs(clip));
                AssetDatabase.DeleteAsset(path);
                VRCFuryAssetDatabase.DeleteDirIfEmpty(folder);
                Assert.That(Directory.Exists(folder), Is.False);
            } finally {
                if (clip != null && !clip.IsSaved()) Object.DestroyImmediate(clip);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void MoveThrowsWhileAssetEditing() {
            var folder = AssetDatabase.GenerateUniqueAssetPath("Assets/VF Pending Move Test");
            var source = folder + "/Source.anim";
            var destinationFolder = folder + "/Destination";
            var destination = destinationFolder + "/Final.anim";
            var clip = new AnimationClip();
            string originalGuid;
            long originalFileId;
            try {
                VRCFuryAssetDatabase.CreateFolder(folder);
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    VRCFuryAssetDatabase.SaveAsset(clip, source);
                    Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out originalGuid, out originalFileId), Is.True);
                    Assert.Throws<System.InvalidOperationException>(() => VRCFuryAssetDatabase.MoveAsset(source, destination));
                    Assert.That(Directory.Exists(destinationFolder), Is.False);
                    Assert.That(VRCFuryAssetDatabase.GetAssetPath(clip), Is.EqualTo(source));
                    Assert.That(File.Exists(source), Is.True);
                }

                VRCFuryAssetDatabase.MoveAsset(source, destination);
                var moved = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
                Assert.That(moved, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(destination));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(moved, out var guid, out long fileId), Is.True);
                Assert.That((guid, fileId), Is.EqualTo((originalGuid, originalFileId)));
                Assert.That(File.Exists(source), Is.False);
            } finally {
                if (clip != null && !clip.IsSaved()) Object.DestroyImmediate(clip);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void ReplacesUntrackedFolderAndPreservesPendingWrites() {
            var folder = AssetDatabase.GenerateUniqueAssetPath("Assets/VF Unregistered Folder Test");
            var leftover = folder + "/Leftover.txt";
            var pending = folder + "/Pending.txt";
            var firstPath = folder + "/First.anim";
            var secondPath = folder + "/Second.anim";
            var first = new AnimationClip();
            var second = new AnimationClip();
            try {
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(leftover, "Leftover");
                    Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);

                    VRCFuryAssetDatabase.CreateFolder(folder);
                    Assert.That(File.Exists(leftover), Is.False);
                    File.WriteAllText(pending, "Pending");
                    VRCFuryAssetDatabase.SaveAsset(first, firstPath);
                    VRCFuryAssetDatabase.SaveAsset(second, secondPath);
                    Assert.That(File.Exists(pending), Is.True);
                    Assert.That(File.Exists(firstPath), Is.True);
                }

                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(firstPath), Is.SameAs(first));
                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(secondPath), Is.SameAs(second));
            } finally {
                if (first != null && !first.IsSaved()) Object.DestroyImmediate(first);
                if (second != null && !second.IsSaved()) Object.DestroyImmediate(second);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingPathIsAvailableUntilImportsResume(bool resumeWithinScope) {
            var folder = AssetDatabase.GenerateUniqueAssetPath("Assets/VF Pending Asset Path Test");
            var path = folder + "/Pending.anim";
            var movedPath = folder + "/Moved.anim";
            var clip = new AnimationClip();
            try {
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    using (VRCFuryAssetDatabase.WithAssetEditing()) {
                        VRCFuryAssetDatabase.SaveAsset(clip, path);
                    }
                    Assert.That(VRCFuryAssetDatabase.GetAssetPath(clip), Is.EqualTo(path));
                    if (resumeWithinScope) VRCFuryAssetDatabase.WithoutAssetEditing(MoveAndCheck);
                }
                if (!resumeWithinScope) MoveAndCheck();
            } finally {
                if (clip != null && !clip.IsSaved()) Object.DestroyImmediate(clip);
                AssetDatabase.DeleteAsset(folder);
            }

            void MoveAndCheck() {
                Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(path));
                Assert.That(AssetDatabase.MoveAsset(path, movedPath), Is.Empty);
                Assert.That(VRCFuryAssetDatabase.GetAssetPath(clip), Is.EqualTo(movedPath));
            }
        }
    }
}
