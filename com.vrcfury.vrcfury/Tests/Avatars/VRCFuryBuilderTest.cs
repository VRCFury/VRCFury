#if VRCSDK_HAS_FACE_TRACKING
namespace VF.Tests {
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.TestTools;
    using UnityEngine.UIElements;
    using VF.Feature;
    using VF.Builder;
    using VF.Feature.Base;
    using VF.Inspector;
    using VF.Model;
    using VF.Model.Feature;
    using VF.Model.StateAction;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using FtBehaviourControl = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.BlendSet;
    using Toggle = VF.Model.Feature.Toggle;

    internal class VRCFuryBuilderTest : FaceTrackingTestBase {
        [Test]
        public void FullBuildKeepsFaceTrackingAndToggleCurvesThroughLinkingAndOptimization() {
            using (var fixture = new Build()) {
                var root = fixture.root;
                root.name = "FtMergeRegression_" + System.Guid.NewGuid().ToString("N");
                var avatar = fixture.avatar;
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    VRCAvatarDescriptor.AnimLayerType.Base, VRCAvatarDescriptor.AnimLayerType.Additive,
                    VRCAvatarDescriptor.AnimLayerType.Gesture, VRCAvatarDescriptor.AnimLayerType.Action, VRCAvatarDescriptor.AnimLayerType.FX
                }.Select(type => new VRCAvatarDescriptor.CustomAnimLayer { type = type, isDefault = false }).ToArray();
                var body = fixture.Skin("Base");
                var accessory = fixture.Skin("Accessory");
                foreach (var skin in new[] { body, accessory })
                    skin.sharedMesh.AddBlendShapeFrame("Unused", 100, new[] { Vector3.right }, null, null);
                var marker = new GameObject("Marker");
                marker.transform.SetParent(root.transform);
                var set = FaceTrackingUtils.CreateBlendshapeSet("Face");
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                var sourceController = new UnityEditor.Animations.AnimatorController();
                sourceController.AddLayer("FT");
                var sourceBehavior = sourceController.layers[0].stateMachine.AddState("FT")
                    .AddStateMachineBehaviour<VRCAnimatorFaceTrackingBlendshapesSet>();
                sourceBehavior.blendSets = new List<FtBehaviourControl> { new FtBehaviourControl { blendshapesSetName = "Face", weight = 0.6f } };
                body.gameObject.AddComponent<VRCFury>().content = new FullController {
                    controllers = { new FullController.ControllerEntry { controller = sourceController } },
                    faceTrackingMeshes = { (ObjectReference<SkinnedMeshRenderer>)body },
                    faceTrackingBlendshapeSets = { set }
                };
                root.AddComponent<VRCFury>().content = new BlendShapeLink {
                    baseObj = "Base", linkSkins = { new BlendShapeLink.LinkSkin { renderer = accessory } }
                };
                var toggle = new Toggle { name = "Smile" };
                toggle.state.actions.Add(new BlendShapeAction { renderer = body, allRenderers = false, blendShape = "Smile", blendShapeValue = 75 });
                root.AddComponent<VRCFury>().content = toggle;
                var visemes = NewVisemes();
                visemes.state_aa.actions.Add(new BlendShapeAction { renderer = body, allRenderers = false, blendShape = "Smile", blendShapeValue = 65 });
                visemes.state_aa.actions.Add(new ObjectToggleAction { obj = marker });
                root.AddComponent<VRCFury>().content = visemes;
                root.AddComponent<VRCFury>().content = new BlendshapeOptimizer();
                // Exercise the real scheduler without showing the editor's progress popup.
                var progress = ScriptableObject.CreateInstance<VRCFProgressWindow>();
                string outputFolder = null;
                try {
                    typeof(VRCFuryBuilder).GetMethod("ApplyFuryConfigs", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { (VFGameObject)root, progress });
                    var fx = (UnityEditor.Animations.AnimatorController)VRCAvatarUtils.GetAvatarController(avatar, VRCAvatarDescriptor.AnimLayerType.FX).Item2;
                    outputFolder = VRCFuryAssetDatabase.GetDirectoryName(AssetDatabase.GetAssetPath(fx));
                    VRCFuryInjectorBuilder.GetInjector(avatar).GetService<SaveAssetsService>().Session.WriteWorkLogManifest();
                    var workLog = System.IO.File.ReadAllLines(System.IO.Directory.GetFiles(outputFolder, "*.txt").Single());
                    var timingHeader = System.Array.IndexOf(workLog, "Build Step Timing:");
                    Assert.That(timingHeader, Is.GreaterThanOrEqualTo(0));
                    var steps = workLog.Skip(timingHeader + 1).TakeWhile(line => line.StartsWith("* ")).ToArray();
                    Assert.That(steps.Any(line => line.EndsWith("Collecting VRCFury components")), Is.True);
                    Assert.That(steps.Any(line => line.Contains("SaveAssetsService.Run on ")), Is.True);
                    var durations = steps.Select(line => double.Parse(line.Substring(2, line.IndexOf(" ms") - 2))).ToArray();
                    Assert.That(durations, Is.Ordered.Descending);
                    var ft = avatar.faceTrackingBlendshapesSetList.Single(entry => entry.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen] != "");
                    var name = ft.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                    Assert.That(name, Does.StartWith("VF_FT_"));
                    var animatorName = "VF_Animated_" + name;
                    foreach (var skin in new[] { body, accessory }) {
                        Assert.That(skin.HasBlendshape(name), Is.True);
                        Assert.That(skin.HasBlendshape(animatorName), Is.True);
                        Assert.That(skin.HasBlendshape("Unused"), Is.False, "The optimizer must actually have run");
                        var path = AnimationUtility.CalculateTransformPath(skin.transform, root.transform);
                        var bindings = fx.animationClips.SelectMany(AnimationUtility.GetCurveBindings)
                            .Where(binding => binding.path == path).Select(binding => binding.propertyName).ToArray();
                        Assert.That(bindings, Does.Contain("blendShape." + animatorName));
                        Assert.That(bindings, Does.Not.Contain("blendShape." + name));
                    }
                    var migrated = avatar.faceTrackingBlendshapesSetList.Single(entry => entry.visemeBlendshapeNames[10] != "");
                    Assert.That(migrated.visemeBlendshapeNames[0], Is.Empty);
                    Assert.That(fx.animationClips.SelectMany(AnimationUtility.GetCurveBindings)
                        .Any(binding => binding.path == "Marker" && binding.propertyName == "m_IsActive"), Is.True,
                        "The non-blendshape action must still be present in the animator");
                    Assert.That(fx.layers.SelectMany(layer => layer.stateMachine.states).SelectMany(state => state.state.behaviours)
                        .OfType<VRCAnimatorFaceTrackingBlendshapesSet>().SelectMany(behavior => behavior.blendSets)
                        .Any(control => control.blendshapesSetName == ft.setName), Is.True);
                    Assert.That(sourceBehavior.blendSets.Single().blendshapesSetName, Is.EqualTo("Face"));
                } finally {
                    Object.DestroyImmediate(progress);
                    Object.DestroyImmediate(sourceController);
                    // The scheduler saves generated assets; leave their destruction to AssetDatabase, not Build.Dispose.
                    foreach (var skin in new[] { body, accessory }) if (skin != null) skin.sharedMesh = null;
                    if (outputFolder != null && outputFolder.StartsWith("Packages/com.vrcfury.temp/Builds/FtMergeRegression_", System.StringComparison.Ordinal))
                        AssetDatabase.DeleteAsset(outputFolder);
                }
            }
        }
    }
}
#endif
