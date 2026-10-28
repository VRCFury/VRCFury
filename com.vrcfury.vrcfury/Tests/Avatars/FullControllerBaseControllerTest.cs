using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VF.Exceptions;
using VF.Feature;
using VF.Feature.Base;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Tests {
    [Category("VRCFury")]
    public class FullControllerBaseControllerTest {
        private static readonly VRCAvatarDescriptor.AnimLayerType[] LocomotionTypes = {
            VRCAvatarDescriptor.AnimLayerType.Base, VRCAvatarDescriptor.AnimLayerType.TPose,
            VRCAvatarDescriptor.AnimLayerType.IKPose, VRCAvatarDescriptor.AnimLayerType.Sitting
        };
        private class Build {
            public readonly GlobalsService globals = new GlobalsService {
                allBuildersInRun = new List<FeatureBuilder>()
            };
            private readonly LayerSourceService sources = new LayerSourceService();
            public readonly ControllerManager controller;

            public Build(VRCAvatarDescriptor.AnimLayerType type = VRCAvatarDescriptor.AnimLayerType.Base,
                string originalOwner = LayerSourceService.AvatarDescriptorSource) {
                typeof(LayerSourceService).VFField("globals").SetValue(sources, globals);
                controller = new ControllerManager(VFController.Create(), null, type,
                    () => 0, () => "", name => name, sources);
                foreach (var name in new[] { "AvatarA", "AvatarB" }) {
                    var layer = controller.NewLayer(name);
                    layer.NewState(name);
                    sources.SetSource(layer, originalOwner);
                }
            }

            public void Merge(string name, bool overwrite) {
                globals.currentFeatureName = name;
                var builder = new FullControllerBuilder {
                    model = new FullController { overwriteLocomotion = overwrite }
                };
                typeof(FullControllerBuilder).VFField("globals").SetValue(builder, globals);
                typeof(FullControllerBuilder).VFField("layerSourceService").SetValue(builder, sources);
                globals.allBuildersInRun.Add(builder);
                var from = VFController.Create();
                from.NewLayer(name + "A").NewState(name + "A");
                from.NewLayer(name + "B").NewState(name + "B");
                typeof(FullControllerBuilder).VFMethod("Merge").Invoke(builder, new object[] { from, controller });
            }

            public string[] States => controller.layers.Select(layer => layer.allStates.Single().name).ToArray();
        }

        [Test]
        public void OverwriteDefaultsOnWithoutAComponentVersionChange() {
            var model = JsonUtility.FromJson<FullController>("{}");
            Assert.That(model.overwriteLocomotion, Is.True);
            Assert.That(model.GetLatestVersion(), Is.EqualTo(4));
        }

        [Test]
        public void OptionControlsWhetherDescriptorLayersSurvive(
            [Values] bool overwrite,
            [Values(LayerSourceService.AvatarDescriptorSource, LayerSourceService.VrcDefaultSource)] string originalOwner,
            [ValueSource(nameof(LocomotionTypes))] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type, originalOwner);
            build.Merge("Imported", overwrite);
            Assert.That(build.States, Is.EqualTo(overwrite
                ? new[] { "ImportedA", "ImportedB" }
                : new[] { "AvatarA", "AvatarB", "ImportedA", "ImportedB" }));
        }

        [Test]
        public void OverwritingLayersPrecedeAllAppendedLayersRegardlessOfBuildOrder(
            [Values] bool overwriteFirst,
            [ValueSource(nameof(LocomotionTypes))] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type);
            if (overwriteFirst) build.Merge("Replacement", true);
            build.Merge("Append1", false);
            if (!overwriteFirst) build.Merge("Replacement", true);
            build.Merge("Append2", false);
            Assert.That(build.States, Is.EqualTo(new[] {
                "ReplacementA", "ReplacementB", "Append1A", "Append1B", "Append2A", "Append2B"
            }));
        }

        [Test]
        public void MultipleAppendControllersPreserveTheDescriptorAndEachOther(
            [ValueSource(nameof(LocomotionTypes))] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type);
            build.Merge("Append1", false);
            build.Merge("Append2", false);
            Assert.That(build.States, Is.EqualTo(new[] {
                "AvatarA", "AvatarB", "Append1A", "Append1B", "Append2A", "Append2B"
            }));
        }

        [Test]
        public void MultipleOverwritingControllersStillConflict(
            [ValueSource(nameof(LocomotionTypes))] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type);
            build.Merge("First", true);
            var exception = Assert.Throws<TargetInvocationException>(() => build.Merge("Second", true));
            Assert.That(exception.InnerException, Is.InstanceOf<VRCFBuilderException>());
            Assert.That(exception.InnerException.Message, Does.Contain("multiple locomotion implementations"));
        }

        [Test]
        public void OptionDoesNotChangeOtherControllerTypes(
            [Values] bool overwrite,
            [Values(VRCAvatarDescriptor.AnimLayerType.FX, VRCAvatarDescriptor.AnimLayerType.Gesture,
                VRCAvatarDescriptor.AnimLayerType.Additive)] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type);
            build.Merge("Imported", overwrite);
            Assert.That(build.States, Is.EqualTo(new[] { "AvatarA", "AvatarB", "ImportedA", "ImportedB" }));
        }
    }
}
