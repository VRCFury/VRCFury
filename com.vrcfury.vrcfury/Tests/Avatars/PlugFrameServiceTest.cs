namespace VF.Tests {
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class PlugFrameServiceTest : SpsPlugRadiusTestBase {
        [Test]
        public void HipPlugFrameAlignsUpBeforeBakingWithoutChangingTheAuthoredPose(
            [Values(HumanBodyBones.Hips, HumanBodyBones.Head)] HumanBodyBones closest,
            [Values(0f, 37f, 90f, 180f)] float roll
        ) {
            var obj = GameObjects.Create("Rolled plug", root);
            obj.localPosition = new Vector3(.1f, .2f, .3f);
            obj.worldRotation = Quaternion.Euler(20, 40, 0) * Quaternion.AngleAxis(roll, Vector3.forward);
            var component = obj.AddComponent<VRCFuryHapticPlug>();
            component.autoRenderer = component.autoLength = component.autoRadius = component.enableSps = false;
            component.length = .4f;
            component.radius = .03f;
            var originalRotation = obj.worldRotation;
            var originalPosition = obj.worldPosition;
            var frames = injector.GetService<PlugFrameService>();
            typeof(PlugFrameService).VFField("autoChannelGenerator").SetValue(frames, new LegSpacingBones {
                closest = closest
            });
            var frame = frames.Get(component);
            Assert.That(Vector3.Distance(frame.worldPosition, originalPosition), Is.LessThan(.00001f));
            Assert.That(Vector3.Dot(frame.TransformDirection(Vector3.forward), obj.TransformDirection(Vector3.forward)),
                Is.EqualTo(1).Within(.00001f));
            var expectedUp = closest == HumanBodyBones.Hips
                ? Vector3.ProjectOnPlane(Vector3.up, obj.TransformDirection(Vector3.forward)).normalized
                : obj.TransformDirection(Vector3.up);
            Assert.That(Vector3.Dot(frame.TransformDirection(Vector3.up), expectedUp), Is.EqualTo(1).Within(.00001f));
            Assert.That(obj.worldRotation, Is.EqualTo(originalRotation));
            Assert.That(frames.Get(component), Is.SameAs(frame));
            var renderer = bake.renderers.Single().renderer;
            var sourceVertices = renderer.GetMesh().vertices;
            var bakedVertices = MeshBaker.BakeMesh(renderer, frame, true).vertices;
            for (var i = 0; i < sourceVertices.Length; i++) {
                Assert.That(Vector3.Distance(
                    frame.TransformPoint(bakedVertices[i]),
                    renderer.owner().TransformPoint(sourceVertices[i])
                ), Is.LessThan(.00001f));
            }
        }
    }
}
