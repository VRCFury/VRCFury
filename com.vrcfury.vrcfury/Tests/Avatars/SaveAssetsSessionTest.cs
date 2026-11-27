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
        [TestCase(false)]
        [TestCase(true)]
        public void SavesAnimationClipsWithControllerAndClipReferencesIntact(bool useBinary) {
            var session = new SaveAssetsSession("BinaryClips " + Guid.NewGuid().ToString("N"));
            var outputDir = (string)typeof(SaveAssetsSession)
                .GetField("outputDir", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(session);
            try {
                var binding = EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x");
                var materialBinding = EditorCurveBinding.PPtrCurve("Bone", typeof(MeshRenderer), "m_Materials.Array.data[0]");
                var curve = AnimationCurve.Linear(0, 2, 1, 5);
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    var controller = VrcfObjectFactory.Create<AnimatorController>();
                    controller.name = "Controller";
                    var reference = VrcfObjectFactory.Create<AnimationClip>();
                    reference.name = "Reference";
                    var clip = VrcfObjectFactory.Create<AnimationClip>();
                    clip.name = "Animation";
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                    var material = VrcfObjectFactory.CreateMaterial(Shader.Find("Standard"));
                    material.name = "Animated material";
                    material.mainTexture = VrcfObjectFactory.CreateTexture2D(2, 2);
                    AnimationUtility.SetObjectReferenceCurve(
                        clip,
                        materialBinding,
                        new[] { new ObjectReferenceKeyframe { time = 0, value = material } }
                    );
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.additiveReferencePoseClip = reference;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    var machine = VrcfObjectFactory.Create<AnimatorStateMachine>();
                    var state = VrcfObjectFactory.Create<AnimatorState>();
                    state.motion = clip;
                    machine.states = new[] { new ChildAnimatorState { state = state } };
                    machine.defaultState = state;
                    controller.layers = new[] { new AnimatorControllerLayer { name = "Layer", stateMachine = machine } };
                    session.SaveControllerAndChildren(
                        controller,
                        new Object[] { machine, state, reference, clip },
                        new Object[] { material },
                        useBinary ? 4096 : 2
                    );
                    session.FlushAssets();
                }

                var controllerPath = outputDir + "/Controller.controller";
                var savedContents = File.ReadAllBytes(controllerPath);
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(controllerPath));
                Assert.That(File.ReadAllBytes(controllerPath), Is.EqualTo(savedContents), "The batch should have saved the complete controller.");

                AssetDatabase.ImportAsset(outputDir, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                var loadedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                var loadedClip = (AnimationClip)loadedController.layers.Single().stateMachine.defaultState.motion;
                var loadedReference = AnimationUtility.GetAnimationClipSettings(loadedClip).additiveReferencePoseClip;
                var clipPath = outputDir + (useBinary ? "/Controller Clips.asset" : "/Controller.controller");
                Assert.That(AssetDatabase.GetAssetPath(loadedClip), Is.EqualTo(clipPath));
                Assert.That(AssetDatabase.GetAssetPath(loadedReference), Is.EqualTo(clipPath));
                Assert.That(loadedReference.name, Is.EqualTo("Reference"));
                Assert.That(AnimationUtility.GetEditorCurve(loadedClip, binding).keys, Is.EqualTo(curve.keys));
                var loadedMaterial = (Material)AnimationUtility.GetObjectReferenceCurve(loadedClip, materialBinding).Single().value;
                Assert.That(loadedMaterial.name, Is.EqualTo("Animated material"));
                Assert.That(AssetDatabase.GetAssetPath(loadedMaterial), Is.EqualTo(controllerPath));
                Assert.That(AssetDatabase.GetAssetPath(loadedMaterial.mainTexture), Is.EqualTo(controllerPath));
                if (useBinary) {
                    using var data = File.OpenRead(clipPath);
                    var prefix = new byte[5];
                    Assert.That(data.Read(prefix, 0, prefix.Length), Is.EqualTo(prefix.Length));
                    Assert.That(Encoding.ASCII.GetString(prefix), Is.Not.EqualTo("%YAML"));
                }
            } finally {
                AssetDatabase.DeleteAsset(outputDir);
            }
        }

        [Test]
        public void WritesSectionsWithoutAssetEntriesAndClearsThemAfterSaving() {
            var session = new SaveAssetsSession("WorkLogSections");
            var outputDir = (string)typeof(SaveAssetsSession)
                .GetField("outputDir", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(session);
            try {
                session.AddWorkLogSection("Build Step Timing", "* First" + Environment.NewLine + "* Second");
                session.AddWorkLogSection("Other", "Detail");
                session.AddNote("First note");
                session.AddNote("Second note");
                session.WriteWorkLogManifest();
                session.WriteWorkLogManifest();
                var lines = File.ReadAllLines(Directory.GetFiles(outputDir, "*.txt").Single());
                Assert.That(lines.Where(line => line.Length > 0), Is.EqualTo(new[] {
                    "Build Step Timing:",
                    "* First",
                    "* Second",
                    "Other:",
                    "Detail",
                    "Notes:",
                    "* First note",
                    "* Second note"
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
                var controller = VrcfObjectFactory.Create<AnimatorController>();
                controller.name = "Controller";
                var first = VrcfObjectFactory.Create<VRCExpressionParameters>();
                first.name = "Z last";
                first.WorkLog("First line\nSecond line");
                var second = VrcfObjectFactory.Create<VRCExpressionParameters>();
                second.name = "A first";
                var separate = VrcfObjectFactory.Create<VRCExpressionParameters>();
                separate.name = "Other file";
                var material = VrcfObjectFactory.CreateMaterial(Shader.Find("Standard"));
                material.name = "Material";
                var texture = VrcfObjectFactory.CreateTexture2D(2, 2);
                texture.name = "Material texture";
                material.mainTexture = texture;
                Object otherParent;
                string manifest;
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    session.SaveControllerAndChildren(
                        controller,
                        Array.Empty<Object>(),
                        new Object[] { first, second },
                        0
                    );
                    var nativeControllerPath = AssetDatabase.GetAssetPath(controller);
                    session.FlushAssets();
                    Assert.That(Directory.GetFiles(outputDir, "*.txt"), Is.Empty);
                    first.WorkLog("Updated during the second pass");
                    session.SaveAssetAndChildren(separate);
                    session.SaveAssetAndChildren(material);
                    session.FlushAssets();
                    session.WriteWorkLogManifest();
                    Assert.That(AssetDatabase.GetAssetPath(controller), Is.EqualTo(nativeControllerPath));
                    Assert.That(VRCFuryAssetDatabase.GetAssetPath(material), Is.EqualTo(outputDir + "/VRCFury Other.asset"));
                    otherParent = ((Lazy<BinaryContainer>)typeof(SaveAssetsSession)
                        .GetField("otherParent", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(session)).Value;
                    manifest = Directory.GetFiles(outputDir, "*.txt").Single();
                    session.WriteWorkLogManifest();
                    Assert.That(Directory.GetFiles(outputDir, "*.txt"), Is.EquivalentTo(new[] { manifest }));
                }

                var expected = new StringBuilder();
                foreach (var obj in new Object[] { controller, first, second, separate, material, texture, otherParent }
                             .OrderBy(obj => obj.GetPathAndName())) {
                    expected.AppendLine(obj.GetPathAndName() + ":");
                    foreach (var item in obj.GetWorkLog()) expected.AppendLine("* " + item);
                    expected.AppendLine();
                }
                expected.AppendLine();
                Assert.That(File.ReadAllText(manifest), Is.EqualTo(expected.ToString()));
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(manifest.Replace('\\', '/')), Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(controller), Is.EqualTo(outputDir + "/Controller.controller"));
                Assert.That(AssetDatabase.GetAssetPath(first), Is.EqualTo(AssetDatabase.GetAssetPath(controller)));
                Assert.That(AssetDatabase.GetAssetPath(separate), Is.Not.EqualTo(AssetDatabase.GetAssetPath(controller)));
                var otherPath = outputDir + "/VRCFury Other.asset";
                var loadedMaterial = AssetDatabase.LoadAssetAtPath<Material>(otherPath);
                Assert.That(loadedMaterial.mainTexture.name, Is.EqualTo("Material texture"));
                Assert.That(AssetDatabase.GetAssetPath(loadedMaterial.mainTexture), Is.EqualTo(otherPath));
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
