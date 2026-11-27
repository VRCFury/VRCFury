using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class ControllersServiceTest {
        [Test]
        public void ParameterInspectionReadsTheFinalControllerWithoutReplacingItsMotions() {
            var root = new GameObject("Parameter inspection");
            var raw = VrcfObjectFactory.Create<AnimatorController>();
            var machine = VrcfObjectFactory.Create<AnimatorStateMachine>();
            var clip = VrcfObjectFactory.Create<AnimationClip>();
            AnimatorState state = null;
            VRCAvatarParameterDriver driver = null;
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer {
                        type = VRCAvatarDescriptor.AnimLayerType.FX,
                        animatorController = raw
                    }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                raw.layers = new[] { new AnimatorControllerLayer { name = "FX", stateMachine = machine } };
                state = machine.AddState("Pose");
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                    AnimationCurve.Constant(0, 1, 2)
                );
                state.motion = clip;
                state.timeParameterActive = true;
                state.timeParameter = "Time";
                raw.AddParameter("Time", AnimatorControllerParameterType.Float);
                driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter> {
                    new VRC_AvatarParameterDriver.Parameter { name = "Count", type = VRC_AvatarParameterDriver.ChangeType.Add }
                };

                var service = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ControllersService>();
                service.LoadAllControllers();
                Assert.That(service.GetFx().TryGetParam("LateParam", out _), Is.False);
                raw.AddParameter("LateParam", AnimatorControllerParameterType.Float);
                service.ClearCache(applyBaseMask: false);
                var view = service.GetControllersForParameterInspection().Single();
                Assert.That(view.TryGetParam("LateParam", out _), Is.True);
                Assert.That(view.GetClips(), Is.Empty);
                Assert.That(
                    view.layers.Single().GetBehaviours<VRCAvatarParameterDriver>().Single().parameters[0].name,
                    Is.EqualTo("Count")
                );
                var reads = new HashSet<string>();
                view.RewriteParameters(name => {
                    reads.Add(name);
                    return name;
                }, includeWrites: false);
                Assert.That(reads, Does.Contain("Time"));

                var full = service.GetAllUsedControllers().Single();
                Assert.That(full, Is.Not.SameAs(view));
                Assert.That(full.GetClips().Single().GetSourceAsset(), Is.SameAs(clip));
                Assert.That(service.GetControllersForParameterInspection().Single(), Is.SameAs(full));
                Assert.That(state.motion, Is.SameAs(clip));
            } finally {
                Object.DestroyImmediate(root);
                if (state != null) Object.DestroyImmediate(state);
                if (driver != null) Object.DestroyImmediate(driver);
                Object.DestroyImmediate(machine);
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void LoadedControllerAndItsCloneCanBeEditedIndependentlyOfTheDescriptor() {
            var folderName = "ControllerOwnership_" + Guid.NewGuid().ToString("N");
            var folder = "Assets/" + folderName;
            var root = new GameObject("Playable controller ownership");
            AssetDatabase.CreateFolder("Assets", folderName);
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                var raw = AnimatorController.CreateAnimatorControllerAtPath(folder + "/FX.controller");
                var clip = new AnimationClip { name = "Position" };
                var position = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
                AnimationUtility.SetEditorCurve(clip, position, AnimationCurve.Constant(0, 1, 1));
                AssetDatabase.AddObjectToAsset(clip, raw);
                raw.layers[0].stateMachine.AddState("Position").motion = clip;
                raw.AddParameter("Weight", AnimatorControllerParameterType.Float);
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer {
                        type = VRCAvatarDescriptor.AnimLayerType.FX,
                        animatorController = raw
                    }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.customizeAnimationLayers = true;

                var controllers = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ControllersService>();
                controllers.LoadAllControllers();
                var fx = controllers.GetFx();
                var duplicate = (VFAvatarController)fx.Clone();
                var layer = fx.layers.Single();
                var duplicateLayer = duplicate.layers.Single();
                Assert.That(fx.GetSourceAsset(), Is.SameAs(raw));
                Assert.That(fx.GetLayerOwner(layer), Is.EqualTo(LayerSourceService.AvatarDescriptorSource));
                Assert.That(duplicate.GetLayerOwner(duplicateLayer), Is.EqualTo(LayerSourceService.AvatarDescriptorSource));
                Assert.That(fx.parameters.Single().Controller, Is.SameAs(fx));
                Assert.That(duplicate.parameters.Single().Controller, Is.SameAs(duplicate));

                var builtClip = fx.GetClips().Single();
                var binding = builtClip.GetFloatBindings().Single();
                builtClip.SetCurve(binding, AnimationCurve.Constant(0, 1, 2));
                Assert.That(builtClip.GetFloatCurve(binding).Evaluate(1), Is.EqualTo(2));
                Assert.That(duplicate.GetClips().Single().GetFloatCurve(binding).Evaluate(1), Is.EqualTo(1));
                Assert.That(AnimationUtility.GetEditorCurve(clip, position).Evaluate(1), Is.EqualTo(1));

                layer.Remove();
                Assert.That(fx.layers, Is.Empty);
                Assert.That(duplicate.layers.Count, Is.EqualTo(1));
                Assert.That(raw.layers, Has.Length.EqualTo(1));
            } finally {
                Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
