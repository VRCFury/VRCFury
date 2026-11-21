using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    internal class SeparateDescriptorBlendshapesTest {
        private GameObject root;
        private VRCAvatarDescriptor avatar;
        private Mesh mesh;
        private VRCFuryInjector injector;

        [SetUp]
        public void SetUp() {
            root = new GameObject("Avatar");
            avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.baseAnimationLayers = new[] {
                new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
            };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            injector.GetService<ControllersService>().ClearCache(applyBaseMask: false);
            mesh = new Mesh { vertices = new[] { Vector3.zero } };
            mesh.AddBlendShapeFrame("Animated", 50, new[] { Vector3.up }, new[] { Vector3.left }, new[] { Vector3.forward });
            mesh.AddBlendShapeFrame("Animated", 100, new[] { Vector3.down }, null, null);
            mesh.AddBlendShapeFrame("Unused", 100, new[] { Vector3.right }, null, null);
        }

        [TearDown]
        public void TearDown() {
            var copies = new HashSet<Mesh>();
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) copies.Add(skin.sharedMesh);
            Object.DestroyImmediate(root);
            foreach (var copy in copies) if (copy != null && copy != mesh) Object.DestroyImmediate(copy);
            Object.DestroyImmediate(mesh);
        }

        private SkinnedMeshRenderer Skin(string name) {
            var skin = GameObjects.Create(name, root).AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            return skin;
        }

        private VFClip Animate(SkinnedMeshRenderer skin) {
            var clip = VFClip.Create("Animated face");
            clip.SetCurve(skin, "blendShape.Animated", 75);
            injector.GetService<ControllersService>().GetFx().NewLayer("Toggle").NewState("On").WithAnimation(clip);
            return clip;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnimationUsesOneCopyForSharedEyelidAndMouthShape(bool jawFlap) {
            var skin = Skin("Face");
            var other = Skin("Untouched");
            skin.SetBlendShapeWeight(0, 25);
            avatar.VisemeSkinnedMesh = skin;
            avatar.lipSync = jawFlap
                ? VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape : VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
            avatar.MouthOpenBlendShapeName = "Animated";
            avatar.VisemeBlendShapes = new[] { "Animated", "Unused" };
            avatar.enableEyeLook = true;
            avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
            avatar.customEyeLookSettings.eyelidsSkinnedMesh = skin;
            avatar.customEyeLookSettings.eyelidsBlendshapes = new[] { 0, 1, -1 };
            var clip = Animate(skin);

            injector.GetService<SeparateDescriptorBlendshapesService>().Apply();

            Assert.That(clip.GetFloatBindings()[0].propertyName,
                Is.EqualTo("blendShape." + skin.sharedMesh.GetBlendShapeName(2)));
            Assert.That(other.sharedMesh, Is.SameAs(mesh));
            Assert.That(mesh.blendShapeCount, Is.EqualTo(2));
            Assert.That(skin.sharedMesh.blendShapeCount, Is.EqualTo(3));
            Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
            Assert.That(skin.GetBlendShapeWeight(2), Is.EqualTo(25));
            Assert.That(avatar.customEyeLookSettings.eyelidsBlendshapes, Is.EqualTo(new[] { 0, 1, -1 }));
            Assert.That(jawFlap ? avatar.MouthOpenBlendShapeName : avatar.VisemeBlendShapes[0],
                Is.EqualTo("Animated"));
            Assert.That(avatar.VisemeBlendShapes[1], Is.EqualTo("Unused"));
            Assert.That(skin.sharedMesh.GetBlendShapeFrameCount(2), Is.EqualTo(2));
            Assert.That(skin.sharedMesh.GetBlendShapeFrameWeight(2, 0), Is.EqualTo(50));
            var vertices = new Vector3[1];
            var normals = new Vector3[1];
            var tangents = new Vector3[1];
            skin.sharedMesh.GetBlendShapeFrameVertices(2, 0, vertices, normals, tangents);
            Assert.That(vertices[0], Is.EqualTo(Vector3.up));
            Assert.That(normals[0], Is.EqualTo(Vector3.left));
            Assert.That(tangents[0], Is.EqualTo(Vector3.forward));
        }

#if VRCSDK_HAS_FACE_TRACKING
        [Test]
        public void SharedFaceTrackingMappingOnlyCopiesTheAnimatedMesh() {
            var first = Skin("Animated face");
            var second = Skin("Other face");
            var third = Skin("Unmapped face");
            var clip = Animate(first);
            avatar.enableFaceTracking = true;
            avatar.faceTrackingMeshes = new List<SkinnedMeshRenderer> { first, second };
            var set = FaceTrackingUtils.CreateBlendshapeSet("Face");
            set.faceTrackingBlendshapeNames[0] = "Animated";
            set.visemeBlendshapeNames[0] = "Animated";
            avatar.faceTrackingBlendshapesSetList = new List<VRCAvatarDescriptor.FaceTrackingBlendshapesSet> { set };
            var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax,
                targetBlendshapeName = "Animated"
            };
            avatar.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> { behavior };

            injector.GetService<SeparateDescriptorBlendshapesService>().Apply();

            Assert.That(clip.GetFloatBindings()[0].propertyName,
                Is.EqualTo("blendShape." + first.sharedMesh.GetBlendShapeName(2)));
            Assert.That(second.sharedMesh, Is.SameAs(mesh));
            Assert.That(set.faceTrackingBlendshapeNames[0], Is.EqualTo("Animated"));
            Assert.That(set.visemeBlendshapeNames[0], Is.EqualTo("Animated"));
            Assert.That(behavior.targetBlendshapeName, Is.EqualTo("Animated"));
            Assert.That(third.sharedMesh, Is.SameAs(mesh));
        }
#endif
    }
}
