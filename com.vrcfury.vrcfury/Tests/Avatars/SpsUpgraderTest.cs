using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Exceptions;
using VF.Injector;
using VF.Inspector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;
using LightType = VF.Component.VRCFuryHapticSocket.AddLight;

#pragma warning disable 0612 // Exercise serialized inputs from legacy components.

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsUpgraderTest {
        private static readonly FieldInfo TpsImportEnabled = typeof(TpsDepthAnimationImporter)
            .GetField("ENABLED", BindingFlags.Static | BindingFlags.NonPublic);
        private bool previousTpsImportEnabled;
        private GameObject root;
        private VRCAvatarDescriptor avatar;
        private AnimatorController controller;
        private VRCExpressionParameters parameters;
        private VRCExpressionsMenu menu;
        private VRCFuryInjector injector;
        private SpsUpgrader upgrader;
        private readonly List<Object> assets = new List<Object>();

        [SetUp]
        public void SetUp() {
            previousTpsImportEnabled = (bool)TpsImportEnabled.GetValue(null);
            TpsImportEnabled.SetValue(null, true);
            root = new GameObject("Upgrade Test");
            avatar = root.AddComponent<VRCAvatarDescriptor>();
            controller = Track(new AnimatorController());
            controller.AddLayer("Unrelated");
            controller.AddParameter("Unrelated", AnimatorControllerParameterType.Bool);
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer {
                type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = controller, isDefault = false
            } };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            parameters = Track(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            parameters.parameters = new[] { new VRCExpressionParameters.Parameter { name = "Unrelated" } };
            VRCAvatarUtils.SetAvatarParams(avatar, parameters);
            menu = NewMenu();
            menu.controls.Add(Toggle("Unrelated"));
            VRCAvatarUtils.SetAvatarMenu(avatar, menu);
            injector = VRCFuryInjectorBuilder.CreateForManualEditing(root);
            upgrader = injector.GetService<SpsUpgrader>();
        }

        [TearDown]
        public void TearDown() {
            TpsImportEnabled.SetValue(null, previousTpsImportEnabled);
            var masks = root.GetComponentsInChildren<VRCFuryHapticPlug>(true)
                .Select(p => p.textureMask.Get()).Where(texture => texture != null && VrcfObjectFactory.DidCreate(texture))
                .Select(AssetDatabase.GetAssetPath).Where(path => path != "").Distinct().ToArray();
            var depthClips = root.GetComponentsInChildren<VRCFuryHapticPlug>(true).SelectMany(p => p.depthActions2)
                .Concat(root.GetComponentsInChildren<VRCFuryHapticSocket>(true).SelectMany(s => s.depthActions2))
                .SelectMany(a => a.actionSet.actions).OfType<VF.Model.StateAction.AnimationClipAction>()
                .Select(a => a.clip.Get()).Where(c => c != null && VrcfObjectFactory.DidCreate(c))
                .Select(AssetDatabase.GetAssetPath).Where(path => path != "").Distinct().ToArray();
            Object.DestroyImmediate(root);
            foreach (var path in masks) AssetDatabase.DeleteAsset(path);
            foreach (var path in depthClips) AssetDatabase.DeleteAsset(path);
            foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private T Track<T>(T asset) where T : Object { assets.Add(asset); return asset; }
        private GameObject Child(string name, Transform parent = null) {
            var child = new GameObject(name);
            child.transform.SetParent(parent != null ? parent : root.transform, false);
            return child;
        }
        private VRCExpressionsMenu NewMenu() => Track(ScriptableObject.CreateInstance<VRCExpressionsMenu>());
        private static VRCExpressionsMenu.Control Toggle(string parameter) => new VRCExpressionsMenu.Control {
            name = parameter, type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter }
        };
        private static VRCExpressionsMenu.Control Submenu(string name, VRCExpressionsMenu subMenu) => new VRCExpressionsMenu.Control {
            name = name, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = subMenu
        };
        private Light Light(GameObject parent, string name, float range, Vector3 position) {
            var light = Child(name, parent.transform).AddComponent<Light>();
            light.color = Color.black;
            light.range = range;
            light.transform.localPosition = position;
            return light;
        }
        private (Light main, Light front) Lights(GameObject parent, float range = 0.41f) => (
            Light(parent, "Root", range, new Vector3(0.03f, -0.02f, 0.04f)),
            Light(parent, "Front", 0.45f, new Vector3(0.04f, -0.02f, 0.04f)));
        private string Apply(bool dryRun = false) => upgrader.Apply(root, dryRun);
        private VRCFuryHapticSocket[] Sockets => root.GetComponentsInChildren<VRCFuryHapticSocket>(true);
        private VRCFuryHapticPlug[] Plugs => root.GetComponentsInChildren<VRCFuryHapticPlug>(true);
        private static void Vector(Vector3 actual, Vector3 expected) => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));

        private (GameObject owner, SkinnedMeshRenderer body, VRCContactReceiver receiver, string layer) TpsDepthAnimation(bool plug, float scale = 1) {
            var owner = plug ? PlugMesh("TPS").gameObject : Child("TPS Socket");
            owner.transform.localScale = Vector3.one * scale;
            if (!plug) Lights(owner);
            var body = Child("Body").AddComponent<SkinnedMeshRenderer>();
            var mesh = Track(new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } });
            mesh.AddBlendShapeFrame("Enter", 100, new Vector3[3], new Vector3[3], new Vector3[3]);
            mesh.AddBlendShapeFrame("In", 100, new Vector3[3], new Vector3[3], new Vector3[3]);
            body.sharedMesh = mesh;
            var kind = plug ? "Pen" : "Orf";
            var prefix = "TPS_" + kind + "_0_";
            var internalPrefix = "TPS_Internal/" + kind + "/0/";
            var receiver = Child("TPS Depth", owner.transform).AddComponent<VRCContactReceiver>();
            receiver.parameter = internalPrefix + (plug ? "RootRoot" : "Depth_In");
            receiver.radius = 0.4f / scale;
            receiver.receiverType = VRC.Dynamics.ContactReceiver.ReceiverType.Proximity;
            receiver.allowSelf = receiver.allowOthers = true;
            receiver.collisionTags = new List<string> { plug ? SpsTags.LegacySocket : SpsTags.LegacyPlugSphere };
            controller.AddParameter(receiver.parameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(prefix + "Depth", AnimatorControllerParameterType.Float);
            controller.AddParameter(prefix + "Width", AnimatorControllerParameterType.Float);
            AnimationClip Frame(float enter, float inside) {
                var clip = Track(new AnimationClip());
                // TPS CustomCurve((1, value)) writes a single pose key at time one, without a time-zero key.
                clip.SetCurve("Body", typeof(SkinnedMeshRenderer), "blendShape.Enter", new AnimationCurve(new Keyframe(1, enter)));
                clip.SetCurve("Body", typeof(SkinnedMeshRenderer), "blendShape.In", new AnimationCurve(new Keyframe(1, inside)));
                clip.SetCurve("", typeof(Animator), prefix + "Depth", new AnimationCurve(new Keyframe(1, inside / 100)));
                return clip;
            }
            var zero = Frame(0, 0);
            Motion Width(Motion full) => plug ? full : Track(new BlendTree {
                blendType = BlendTreeType.Simple1D, blendParameter = prefix + "Width", useAutomaticThresholds = false,
                children = new[] { new ChildMotion { motion = zero, threshold = 0, timeScale = 1 },
                    new ChildMotion { motion = full, threshold = 0.8f, timeScale = 1 } }
            });
            var tree = Track(new BlendTree {
                blendType = BlendTreeType.Simple1D, blendParameter = receiver.parameter, useAutomaticThresholds = false,
                children = new[] {
                    new ChildMotion { motion = Width(zero), threshold = 0, timeScale = 1 },
                    new ChildMotion { motion = Width(Frame(100, 0)), threshold = 0.05f, timeScale = 1 },
                    new ChildMotion { motion = Width(Frame(0, 100)), threshold = 1, timeScale = 1 }
                }
            });
            var layer = "[TPS][" + kind + "0] Depth";
            controller.AddLayer(layer);
            var machine = controller.layers.Last().stateMachine;
            machine.AddState("Penetrated").motion = tree;
            machine.AddState("No Penetration").motion = zero;
            return (owner, body, receiver, layer);
        }

        [TestCase(false, 1f)]
        [TestCase(false, 2f)]
        [TestCase(true, 1f)]
        public void ImportsTpsDepthAnimationsAsLinearClips(bool plug, float scale) {
            var setup = TpsDepthAnimation(plug, scale);
            Child("Body", setup.owner.transform).AddComponent<SkinnedMeshRenderer>().sharedMesh = setup.body.sharedMesh;
            Assert.That(Apply(true), Does.Contain("TPS depth animations will be imported"));
            Assert.That(Plugs.SelectMany(p => p.depthActions2).Concat(Sockets.SelectMany(s => s.depthActions2)), Is.Empty);
            Assert.That(setup.receiver != null, Is.True);
            Apply();
            var action = plug ? Plugs.Single().depthActions2.Single() : Sockets.Single().depthActions2.Single();
            Assert.That(action.units, Is.EqualTo(VRCFuryHapticSocket.DepthActionUnits.Local));
            Assert.That(action.range.x, Is.EqualTo(-0.4f / scale).Within(0.0001f));
            Assert.That(action.range.y, Is.Zero);
            Assert.That(action.enableSelf, Is.True);
            var animation = (VF.Model.StateAction.AnimationClipAction)action.actionSet.actions.Single();
            var clip = animation.clip.Get();
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.Not.Empty);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.type == typeof(Animator)), Is.False);
            var enter = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("/Body", typeof(SkinnedMeshRenderer), "blendShape.Enter"));
            var inside = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("/Body", typeof(SkinnedMeshRenderer), "blendShape.In"));
            Assert.That(enter.Evaluate(0.025f), Is.EqualTo(50).Within(0.001f));
            Assert.That(enter.Evaluate(0.05f), Is.EqualTo(100).Within(0.001f));
            Assert.That(inside.Evaluate(1), Is.EqualTo(100).Within(0.001f));
            var loaded = VFMotion.Load(clip, new VFLoadContext {
                OwnerObject = setup.owner, AnimatorObject = root, ObjectPaths = injector.GetService<VRCFObjectPathCache>()
            });
            Assert.That(loaded.GetFloatBindings().All(b => b.target == setup.body.owner()), Is.True,
                "Absolute paths must keep targeting the original renderer, even with a same-named child");
            Assert.That(setup.receiver == null, Is.True);
            Assert.That(controller.layers.Any(l => l.name == setup.layer), Is.False);
            Assert.That(controller.parameters.Any(p => p.name.StartsWith("TPS_")), Is.False);
            Apply();
            Assert.That(plug ? Plugs.Single().depthActions2.Count : Sockets.Single().depthActions2.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisabledTpsDepthImportPreservesLegacyAnimations() {
            TpsImportEnabled.SetValue(null, false);
            var setup = TpsDepthAnimation(false);
            Apply();
            Assert.That(Sockets.Single().depthActions2, Is.Empty);
            Assert.That(setup.receiver != null, Is.True);
            Assert.That(controller.layers.Any(layer => layer.name == setup.layer), Is.True);
        }

        [Test]
        public void TpsAnimationImportPreservesExistingActionsAndOtherSide() {
            var setup = TpsDepthAnimation(false);
            var socket = setup.owner.AddComponent<VRCFuryHapticSocket>();
            var existing = new VRCFuryHapticSocket.DepthActionNew();
            socket.depthActions2.Add(existing);
            upgrader.ApplyPlugs(root, false);
            Assert.That(socket.depthActions2, Is.EqualTo(new[] { existing }));
            Assert.That(setup.receiver != null, Is.True);
            upgrader.ApplySockets(root, false);
            Assert.That(socket.depthActions2, Has.Count.EqualTo(2));
            Assert.That(socket.depthActions2[0], Is.SameAs(existing));
        }

        [Test]
        public void BuildTpsAnimationImportKeepsGeneratedClipInMemoryAndSourceControllerUntouched() {
            var setup = TpsDepthAnimation(false);
            var before = EditorJsonUtility.ToJson(controller);
            var build = VRCFuryInjectorBuilder.GetInjector(avatar);
            build.GetService<ControllersService>().LoadAllControllers();
            build.GetService<SpsUpgrader>().Apply(root, false);
            var animation = (VF.Model.StateAction.AnimationClipAction)Sockets.Single().depthActions2.Single().actionSet.actions.Single();
            Assert.That(animation.clip.Get(), Is.Null);
            Assert.That(animation.vfClip, Is.TypeOf<VFClip>());
            Assert.That(EditorJsonUtility.ToJson(controller), Is.EqualTo(before));
            Assert.That(build.GetService<ControllersService>().GetFx().GetLayers().Any(l => l.name == setup.layer), Is.False);
        }

        [Test]
        public void TpsShaderBufferingDoesNotCreateDepthActions() {
            var setup = TpsDepthAnimation(true);
            var sender = Child("TPS Sender", setup.owner.transform).AddComponent<VRCContactSender>();
            sender.collisionTags = new List<string> { SpsTags.LegacyPlugSphere };
            foreach (var clip in controller.animationClips) {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
                clip.SetCurve(setup.owner.name, typeof(Renderer), "material._TPS_BufferedDepth", AnimationCurve.Constant(0, 1, 1));
                clip.SetCurve(setup.owner.name + "/TPS Sender", typeof(VRCContactSender), "m_Enabled", new AnimationCurve(new Keyframe(1, 1)));
                clip.SetCurve("", typeof(Animator), "TPS_Pen_0_BufferedDepth", new AnimationCurve(new Keyframe(1, 1)));
            }
            controller.AddParameter("TPS_Pen_0_BufferedDepth", AnimatorControllerParameterType.Float);
            var rootRoot = controller.layers.Last().stateMachine.states[0].state.motion as BlendTree;
            rootRoot.blendType = BlendTreeType.FreeformCartesian2D;
            rootRoot.blendParameterY = "TPS_Pen_0_BufferedDepth";
            foreach (var suffix in new[] { "RootForw", "BackRoot" }) {
                var contact = Child("TPS " + suffix, setup.owner.transform).AddComponent<VRCContactReceiver>();
                contact.parameter = "TPS_Internal/Pen/0/" + suffix;
                contact.collisionTags = new List<string> { SpsTags.LegacySocket };
                controller.AddParameter(contact.parameter, AnimatorControllerParameterType.Float);
            }
            foreach (var suffix in new[] { "Comp1", "Comp2" }) {
                var parameter = "TPS_Internal/Pen/0/" + suffix;
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
                var comparison = Track(new AnimationClip());
                comparison.SetCurve("", typeof(Animator), parameter, new AnimationCurve(new Keyframe(1, 1)));
                controller.AddLayer("[TPS][Pen0] " + suffix);
                controller.layers.Last().stateMachine.AddState("Compare").motion = comparison;
            }
            Apply();
            Assert.That(Plugs.Single().depthActions2, Is.Empty);
            Assert.That(setup.receiver == null, Is.True, "SPS deformation replaces the TPS shader buffer's contacts");
            Assert.That(sender == null, Is.True);
            Assert.That(controller.layers.Any(l => l.name == setup.layer), Is.False);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
            Assert.That(controller.layers.Any(l => l.name.StartsWith("[TPS]")), Is.False);
        }

        [Test]
        public void TpsBufferCleanupPreservesUserAnimationInTheSameLayer() {
            var setup = TpsDepthAnimation(true);
            var rootRoot = controller.layers.Last().stateMachine.states[0].state.motion as BlendTree;
            rootRoot.blendType = BlendTreeType.FreeformCartesian2D;
            rootRoot.blendParameterY = "TPS_Pen_0_BufferedDepth";
            var comparison = Track(new AnimationClip());
            const string parameter = "TPS_Internal/Pen/0/Comp1";
            controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            comparison.SetCurve("", typeof(Animator), parameter, new AnimationCurve(new Keyframe(1, 1)));
            const string mathLayer = "[TPS][Pen0] Comparison";
            controller.AddLayer(mathLayer);
            controller.layers.Last().stateMachine.AddState("Compare").motion = comparison;
            Apply();
            Assert.That(Plugs.Single().depthActions2, Is.Empty);
            Assert.That(setup.receiver != null, Is.True);
            Assert.That(controller.layers.Any(l => l.name == setup.layer), Is.True);
            Assert.That(controller.layers.Any(l => l.name == mathLayer), Is.True,
                "Unsupported user animation must retain its TPS calculations and contact inputs");
        }

        [Test]
        public void TpsDepthImportRemovesWidthMathAndContactsAndCopiesSelfSetting() {
            var setup = TpsDepthAnimation(false);
            setup.receiver.allowSelf = false;
            var width = Child("TPS Width", setup.owner.transform).AddComponent<VRCContactReceiver>();
            width.parameter = "TPS_Internal/Orf/0/Width1_In";
            width.collisionTags = new List<string> { SpsTags.LegacyPlugSphere };
            controller.AddParameter(width.parameter, AnimatorControllerParameterType.Float);
            var clip = Track(new AnimationClip());
            clip.SetCurve("", typeof(Animator), "TPS_Orf_0_Width", AnimationCurve.Linear(0, 0, 1, 1));
            const string mathLayer = "[TPS][Orf0] Width Calculation";
            controller.AddLayer(mathLayer);
            var feedback = controller.layers.Last().stateMachine.AddState("Buffer Width");
            feedback.motion = clip;
            feedback.timeParameterActive = true;
            feedback.timeParameter = "TPS_Orf_0_Width";
            Apply();
            Assert.That(Sockets.Single().depthActions2.Single().enableSelf, Is.False);
            Assert.That(width == null, Is.True);
            Assert.That(controller.layers.Any(l => l.name == mathLayer), Is.False);
        }

        [Test]
        public void TpsDepthImportPreservesUnsupportedAnimatedStates() {
            var setup = TpsDepthAnimation(false);
            var extra = Track(new AnimationClip());
            extra.SetCurve("Body", typeof(SkinnedMeshRenderer), "blendShape.Enter", AnimationCurve.Linear(0, 0, 1, 100));
            controller.layers.Last().stateMachine.AddState("User Animation").motion = extra;
            Apply();
            Assert.That(Sockets.Single().depthActions2, Is.Empty);
            Assert.That(setup.receiver != null, Is.True);
            Assert.That(controller.layers.Any(l => l.name == setup.layer), Is.True);
        }

        [TestCase(0.41f, false)]
        [TestCase(0.42f, false)]
        [TestCase(0.43f, false)]
        [TestCase(0.44f, false)]
        [TestCase(0.41f, true)]
        [TestCase(0.42f, true)]
        public void DpsAndTpsLightSocketsPreservePoseAndRemoveLegacyLights(float range, bool inactive) {
            var owner = Child("Socket");
            owner.transform.localScale = Vector3.one * 2;
            owner.transform.localRotation = Quaternion.Euler(12, 30, -17);
            owner.SetActive(!inactive);
            var (main, front) = Lights(owner, range);
            var position = main.transform.position;
            var forward = (front.transform.position - position).normalized;
            var visible = Light(owner, "Visible", 0.41f, Vector3.zero);
            visible.color = Color.white;
            var tip = Light(owner, "Tip", 0.49f, Vector3.zero);
            var unrelated = Light(owner, "Unrelated", 2, Vector3.zero);
            var receiver = Child("TPS Depth", owner.transform).AddComponent<VRCContactReceiver>();
            receiver.collisionTags = new List<string> { "TPSVF_Orifice" };
            var original = EditorJsonUtility.ToJson(owner.transform);
            Apply(true);
            Assert.That(Sockets, Is.Empty);
            Assert.That(main != null && front != null && receiver != null, Is.True);
            Assert.That(EditorJsonUtility.ToJson(owner.transform), Is.EqualTo(original));
            Apply();
            var socket = Sockets.Single();
            Assert.That(socket.gameObject, Is.SameAs(owner));
            Assert.That(socket.addLight, Is.EqualTo(range == 0.41f || range == 0.43f ? LightType.Hole : LightType.Ring));
            Vector(owner.transform.TransformPoint(socket.position), position);
            Vector(owner.transform.TransformDirection(Quaternion.Euler(socket.rotation) * Vector3.forward), forward);
            Assert.That(main == null && front == null && receiver == null, Is.True);
            Assert.That(tip == null, Is.True);
            Assert.That(visible != null && unrelated != null, Is.True);
            Assert.That(owner.activeSelf, Is.EqualTo(!inactive));
            Apply();
            Assert.That(Sockets, Has.Length.EqualTo(1), "Repeated upgrades must not duplicate sockets");
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void IncompleteOrVisibleLightPairsAreNotSockets(bool missingFront, bool visible) {
            var owner = Child("Not a socket");
            var main = Light(owner, "Root", 0.41f, Vector3.zero);
            if (!missingFront) Light(owner, "Front", visible ? 0.45f : 1, Vector3.forward * 0.01f);
            if (visible) main.color = Color.white;
            Apply();
            Assert.That(Sockets, Is.Empty);
            Assert.That(main == null, Is.EqualTo(!visible));
        }

        [TestCase(0.41f)]
        [TestCase(0.42f)]
        [TestCase(0.43f)]
        [TestCase(0.44f)]
        [TestCase(0.45f)]
        [TestCase(0.46f)]
        [TestCase(0.48f)]
        [TestCase(0.49f)]
        public void RemovesUnpairedLegacyLightsWithoutCreatingSockets(float range) {
            var owner = Child("Unused legacy light");
            owner.SetActive(false);
            var light = Light(owner, "Legacy", range, Vector3.zero);
            // Stock DPS tip prefab uses RGB bytes (1, 1, 1), rather than full white.
            if (range >= 0.48f) light.color = new Color(1f / 255, 1f / 255, 1f / 255, 1);
            Apply(true);
            Assert.That(light != null, Is.True, "Preview must not delete lights");
            Assert.That(Sockets, Is.Empty);
            Apply();
            Assert.That(light == null, Is.True);
            Assert.That(Sockets, Is.Empty);
            Assert.That(Plugs, Is.Empty);
        }

        [Test]
        public void UpgradesLegacyContentAndClaimsLights() {
            var owner = Child("Socket");
            var (main, front) = Lights(owner);
            var guides = Child("GUIDES_DELETE");
            var contact = Child("OGB_Contact").AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "Legacy" };
            controller.AddLayer("DPS_Holes");
            controller.AddParameter("DPS_Hole", AnimatorControllerParameterType.Int);
            Apply();
            var socket = Sockets.Single();
            Assert.That(socket.addLight, Is.EqualTo(LightType.Hole));
            Assert.That(main == null && front == null, Is.True);
            Assert.That(guides == null && contact == null, Is.True);
            Assert.That(controller.layers.Any(l => l.name == "DPS_Holes"), Is.False);
            Assert.That(controller.parameters.Any(p => p.name == "DPS_Hole"), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PlugAndSocketUpgradesCanRunSeparatelyInEitherOrder(bool plugsFirst) {
            var renderer = PlugMesh("TPS");
            var tip = Light(renderer.gameObject, "Tip", 0.49f, Vector3.zero);
            var plugMarker = Child("OGB_Marker_Pen", renderer.transform);
            var socketOwner = Child("Socket");
            var (main, front) = Lights(socketOwner);
            var socketMarker = Child("OGB_Marker_Hole", socketOwner.transform);
            var guides = Child("GUIDES_DELETE");
            controller.AddLayer("DPS_Holes");

            string Upgrade(bool plugs, bool preview) => plugs
                ? upgrader.ApplyPlugs(root, preview) : upgrader.ApplySockets(root, preview);
            Upgrade(plugsFirst, true);
            Assert.That(main != null && front != null && tip != null, Is.True);
            Assert.That(Plugs, Is.Empty);
            Assert.That(Sockets, Is.Empty);
            Upgrade(plugsFirst, false);
            Assert.That(Plugs.Length, Is.EqualTo(plugsFirst ? 1 : 0));
            Assert.That(Sockets.Length, Is.EqualTo(plugsFirst ? 0 : 1));
            Assert.That(tip == null && plugMarker == null, Is.EqualTo(plugsFirst));
            Assert.That(main == null && front == null && socketMarker == null, Is.EqualTo(!plugsFirst));
            Assert.That(guides != null, Is.EqualTo(plugsFirst));
            Assert.That(controller.layers.Any(l => l.name == "DPS_Holes"), Is.EqualTo(plugsFirst));
            Upgrade(!plugsFirst, false);
            Assert.That(Plugs.Single().enableSps, Is.True);
            Assert.That(Sockets, Has.Length.EqualTo(1));
            Assert.That(main == null && front == null && tip == null, Is.True);
            Assert.That(plugMarker == null && socketMarker == null, Is.True);
            Assert.That(guides == null, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SocketMigrationCapturesSharedLightsBeforeAnyAreRemoved(bool deleteLights) {
            var outer = Child("Outer").AddComponent<VRCFuryHapticSocket>();
            var inner = Child("Inner", outer.transform).AddComponent<VRCFuryHapticSocket>();
            outer.addLight = inner.addLight = LightType.None;
            inner.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            var (main, front) = Lights(inner.gameObject);
            var position = main.transform.position;
            SpsSocketPreparation.ImportSettingsFromLegacySystems(new[] { outer, inner }, deleteLights);
            Assert.That(main == null && front == null, Is.EqualTo(deleteLights));
            foreach (var socket in new[] { outer, inner }) {
                Assert.That(socket.addLight, Is.EqualTo(LightType.Hole));
                Vector(socket.transform.TransformPoint(socket.position), position);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SeparateUpgradePreservesContactsOwnedByTheOtherSide(bool plugs) {
            var renderer = PlugMesh("TPS");
            var plug = renderer.gameObject.AddComponent<VRCFuryHapticPlug>();
            var socket = Child("Socket").AddComponent<VRCFuryHapticSocket>();
            var plugContact = Child("OGB_Contact", plug.transform).AddComponent<VRCContactReceiver>();
            var socketContact = Child("OGB_Contact", socket.transform).AddComponent<VRCContactReceiver>();
            var unscoped = Child("OGB_Unscoped").AddComponent<VRCContactReceiver>();
            if (plugs) upgrader.ApplyPlugs(root, false);
            else upgrader.ApplySockets(root, false);
            Assert.That(plugContact == null, Is.EqualTo(plugs));
            Assert.That(socketContact == null, Is.EqualTo(!plugs));
            Assert.That(unscoped != null, Is.True);
        }

        [Test]
        public void SocketOnlyUpgradePreservesUnscopedAndPlugParameters() {
            Lights(Child("Socket"));
            foreach (var name in new[] { "OGB/Pen/Old", "TPS/Plug/VF/Depth", "TPS/Socket/VF/Depth" })
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
            upgrader.ApplySockets(root, false);
            Assert.That(controller.parameters.Select(p => p.name), Does.Contain("OGB/Pen/Old"));
            Assert.That(controller.parameters.Select(p => p.name), Does.Contain("TPS/Plug/VF/Depth"));
            Assert.That(controller.parameters.Select(p => p.name), Does.Not.Contain("TPS/Socket/VF/Depth"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlugAssetCleanupRequiresANewPlugAndPreservesSocketAndUnscopedParameters(bool newPlug) {
            if (newPlug) PlugMesh("TPS");
            var plugParams = new[] { "TPS/Plug/VF/Depth", "TPS/Pen/VF/Depth", "OGB/Pen/Old" };
            var keepParams = new[] { "TPS/Socket/VF/Depth", "OGB/Legacy", "OGB/Plug/Native/DepthSelf", "DPS_Hole" };
            foreach (var name in plugParams.Concat(keepParams)) {
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
                parameters.parameters = parameters.parameters.Append(new VRCExpressionParameters.Parameter { name = name }).ToArray();
                menu.controls.Add(Toggle(name));
            }
            var before = EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters) + EditorJsonUtility.ToJson(menu);
            upgrader.ApplyPlugs(root, true);
            Assert.That(EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters) + EditorJsonUtility.ToJson(menu), Is.EqualTo(before));
            upgrader.ApplyPlugs(root, false);
            foreach (var name in plugParams.Concat(keepParams)) {
                var retained = !newPlug || keepParams.Contains(name);
                Assert.That(controller.parameters.Any(p => p.name == name), Is.EqualTo(retained), name);
                Assert.That(parameters.parameters.Any(p => p.name == name), Is.EqualTo(retained), name);
                Assert.That(menu.controls.Any(c => c.parameter?.name == name), Is.EqualTo(retained), name);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SeparateUpgradePreviewIncludesContactsUnderNewComponents(bool plugs) {
            var renderer = PlugMesh("TPS");
            renderer.name = "Plug";
            var socketOwner = Child("Socket");
            Lights(socketOwner);
            var plugContact = Child("OGB_Contact", renderer.transform).AddComponent<VRCContactReceiver>();
            var socketContact = Child("OGB_Contact", socketOwner.transform).AddComponent<VRCContactReceiver>();
            var preview = plugs ? upgrader.ApplyPlugs(root, true) : upgrader.ApplySockets(root, true);
            Assert.That(preview, Does.Contain((plugs ? "Plug" : "Socket") + "/OGB_Contact"));
            Assert.That(preview, Does.Not.Contain((plugs ? "Socket" : "Plug") + "/OGB_Contact"));
            Assert.That(plugContact != null && socketContact != null, Is.True);
            Assert.That(Plugs, Is.Empty);
            Assert.That(Sockets, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SeparateUpgradesAlwaysRemoveTpsVfSendersAndReceivers(bool plugs) {
            var opposite = Child("Other Side");
            if (plugs) opposite.AddComponent<VRCFuryHapticSocket>();
            else {
                var plug = opposite.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = false;
            }
            var sender = Child("Sender", opposite.transform).AddComponent<VRCContactSender>();
            sender.collisionTags = new List<string> { "Unrelated", "TPSVF_Orifice" };
            var receiver = Child("Unscoped Receiver").AddComponent<VRCContactReceiver>();
            receiver.collisionTags = new List<string> { "TPSVF_Depth" };
            var preview = plugs ? upgrader.ApplyPlugs(root, true) : upgrader.ApplySockets(root, true);
            Assert.That(preview, Does.Contain("Other Side/Sender").And.Contain("Unscoped Receiver"));
            Assert.That(sender != null && receiver != null, Is.True);
            if (plugs) upgrader.ApplyPlugs(root, false);
            else upgrader.ApplySockets(root, false);
            Assert.That(sender == null && receiver == null, Is.True);
            Assert.That(opposite != null, Is.True);
        }

        [TestCase(SpsTags.LegacyPlugSphere, true)]
        [TestCase(SpsTags.LegacyPlugWidth, true)]
        [TestCase(SpsTags.LegacySocket, false)]
        [TestCase(SpsTags.LegacySocketFront, false)]
        public void UpgraderRemovesLegacySendersOnTheSelectedSideButNotReceivers(string tag, bool plugs) {
            var sender = Child("Legacy Sender").AddComponent<VRCContactSender>();
            sender.collisionTags = new List<string> { tag };
            var receiver = Child("User Receiver").AddComponent<VRCContactReceiver>();
            receiver.collisionTags = new List<string> { tag };
            var other = Child("Other Side Sender").AddComponent<VRCContactSender>();
            other.collisionTags = new List<string> { plugs ? SpsTags.LegacySocket : SpsTags.LegacyPlugSphere };
            var preview = plugs ? upgrader.ApplyPlugs(root, true) : upgrader.ApplySockets(root, true);
            Assert.That(preview, Does.Contain("Legacy Sender"));
            Assert.That(sender != null, Is.True);
            if (plugs) upgrader.ApplyPlugs(root, false);
            else upgrader.ApplySockets(root, false);
            Assert.That(sender == null, Is.True);
            Assert.That(receiver != null && other != null, Is.True);
        }

        [Test]
        public void AutomaticUpgradeMigratesExistingInputsBeforeBaking() {
            var renderer = PlugMesh("TPS");
            var plug = renderer.gameObject.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = false;
            plug.autoRenderer = false;
            plug.configureTpsMesh.Add(renderer);
            var socket = Child("Socket").AddComponent<VRCFuryHapticSocket>();
            socket.addLight = LightType.None;
            var (main, front) = Lights(socket.gameObject);
            var position = main.transform.position;
            var tip = Light(plug.gameObject, "Tip", 0.49f, Vector3.zero);
            var sender = Child("TPS Sender").AddComponent<VRCContactSender>();
            sender.collisionTags = new List<string> { SpsTags.LegacyPlugSphere };
            var services = VRCFuryInjectorBuilder.GetInjector(avatar);
            services.GetService<ControllersService>().LoadAllControllers();
            services.GetService<SpsAutoUpgradeService>().Apply(autoUpgrade: true);
            Assert.That(plug.enableSps, Is.True);
            Assert.That(plug.autoLength || plug.autoRadius, Is.False);
            Assert.That(plug.length, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(plug.radius, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(socket.addLight, Is.EqualTo(LightType.Hole));
            Vector(socket.transform.TransformPoint(socket.position), position);
            Assert.That(main == null && front == null && tip == null && sender == null, Is.True);
        }

        [TestCase("Raliv")]
        [TestCase("TPS")]
        public void DisablingAutomaticUpgradeStillPreparesExistingComponentsWithoutConvertingOtherSetups(string kind) {
            var renderer = PlugMesh(kind, includeLength: true);
            renderer.sharedMaterial.SetFloat("_Length", 0.45f);
            renderer.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            var plug = Child("Explicit Plug").AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = plug.autoRenderer = false;
            plug.configureTpsMesh.Add(renderer);
            var socket = Child("Explicit Socket").AddComponent<VRCFuryHapticSocket>();
            socket.addLight = LightType.None;
            var (main, front) = Lights(socket.gameObject);
            var socketPosition = main.transform.position;
            var unrelatedRenderer = PlugMesh("TPS");
            var unrelatedLights = Lights(Child("Unconfigured Socket"));
            var guides = Child("GUIDES_DELETE");
            var legacyContact = Child("Old Contact").AddComponent<VRCContactReceiver>();
            legacyContact.collisionTags = new List<string> { "TPSVF_Depth" };
            controller.AddLayer("DPS_Holes");
            controller.AddParameter("DPS_Hole", AnimatorControllerParameterType.Int);
            menu.controls.Add(Toggle("DPS_Hole"));
            var before = EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters) + EditorJsonUtility.ToJson(menu);
            var services = VRCFuryInjectorBuilder.GetInjector(avatar);
            services.GetService<SpsAutoUpgradeService>().Apply(autoUpgrade: false);
            Assert.That(Plugs, Is.EqualTo(new[] { plug }));
            Assert.That(Sockets, Is.EqualTo(new[] { socket }));
            Assert.That(plug.enableSps, Is.True);
            Assert.That(plug.autoLength || plug.autoRadius, Is.False);
            Assert.That(plug.length, Is.EqualTo(kind == "Raliv" ? 0.45f : 0.3f).Within(0.0001f));
            Vector(plug.transform.TransformPoint(plug.position), renderer.transform.position);
            Assert.That(socket.addLight, Is.EqualTo(LightType.Hole));
            Vector(socket.transform.TransformPoint(socket.position), socketPosition);
            Assert.That(main == null && front == null, Is.True);
            Assert.That(unrelatedRenderer != null && unrelatedLights.main != null && unrelatedLights.front != null, Is.True);
            Assert.That(guides != null && legacyContact != null, Is.True);
            Assert.That(EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters) + EditorJsonUtility.ToJson(menu), Is.EqualTo(before));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void NonUniformLegacySocketUpgradesToConstrainedRootAndBakes(bool duringBuild, bool scaledAncestor) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            root.transform.localScale = Vector3.one * 1.2f;
            var bone = Child("Bone");
            bone.transform.localPosition = new Vector3(0.3f, 0.8f, -0.2f);
            bone.transform.localRotation = Quaternion.Euler(15, 35, 20);
            var owner = Child("DPS Socket", bone.transform);
            (scaledAncestor ? bone : owner).transform.localScale = new Vector3(1.3f, 0.7f, 2);
            var (main, front) = Lights(owner);
            var position = main.transform.position;
            var forward = (front.transform.position - position).normalized;
            var expectedName = HapticUtils.GetFallbackId(owner);
            var originalCount = root.GetComponentsInChildren<Transform>(true).Length;
            Apply(true);
            Assert.That(Sockets, Is.Empty);
            Assert.That(root.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(originalCount));
            if (duringBuild) {
                var services = VRCFuryInjectorBuilder.GetInjector(avatar);
                services.GetService<ControllersService>().LoadAllControllers();
                services.GetService<SpsAutoUpgradeService>().Apply(autoUpgrade: true);
            } else {
                Apply();
            }
            var socket = Sockets.Single();
            Apply();
            Assert.That(Sockets.Single(), Is.SameAs(socket), "Repeating the upgrade must not add another constrained socket");
            var baked = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket, avatarRoot: root);
            Vector(baked.bakeRoot.worldPosition, position);
            Vector(baked.bakeRoot.worldRotation * Vector3.forward, forward);
            Assert.That(baked.bakeRoot.parent.parent, Is.EqualTo(root.asVf()));
            Assert.That(baked.bakeRoot.parent.name, Is.EqualTo("Nonuniform SPS Sockets"));
            Vector(baked.bakeRoot.localScale, Vector3.one);
            Assert.That(owner.GetComponent<VRCFuryHapticSocket>(), Is.SameAs(socket));
            Assert.That(VRCFuryHapticSocketEditor.GetMenuName(socket), Is.EqualTo(expectedName));
            var constraint = baked.bakeRoot.GetConstraints().Single();
            Assert.That(constraint.IsParent(), Is.True);
            var target = constraint.GetSources().Single();
            Assert.That(target.parent, Is.EqualTo(owner.asVf()));
            Vector(target.worldPosition, position);
            Vector(target.worldRotation * Vector3.forward, forward);
            Vector(constraint.GetPositionOffset(0), Vector3.zero);
            Vector(constraint.GetRotationOffset(0), Vector3.zero);
            Assert.That(constraint.GetWeight(0), Is.EqualTo(1));
            Assert.That(main == null && front == null, Is.True);
        }

        [Test]
        public void RelocatingNonUniformSocketPreservesDepthAnimationsAndSettings() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var owner = Child("DPS Socket");
            owner.transform.localScale = new Vector3(1.3f, 0.7f, 2);
            Lights(owner);
            var sockets = new SpsSocketUpgrader(new SpsUpgradeContext(root, false, _ => false));
            sockets.Discover();
            var socket = Sockets.Single();
            socket.name = "Custom Socket";
            socket.addMenuItem = true;
            socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.2f, 0.03f),
                actionSet = new VF.Model.State { actions = {
                    new VF.Model.StateAction.BlendShapeAction { blendShape = "Enter" }
                } }
            });
            sockets.Migrate();
            var baked = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket, avatarRoot: root);
            var replacement = Sockets.Single();
            Assert.That(replacement, Is.SameAs(socket));
            Assert.That(baked.bakeRoot.parent.parent, Is.EqualTo(root.asVf()));
            Assert.That(replacement.name, Is.EqualTo("Custom Socket"));
            Assert.That(replacement.addMenuItem, Is.True);
            Assert.That(replacement.addLight, Is.EqualTo(LightType.Hole));
            var action = replacement.depthActions2.Single();
            Assert.That(action.range, Is.EqualTo(new Vector2(-0.2f, 0.03f)));
            Assert.That(((VF.Model.StateAction.BlendShapeAction)action.actionSet.actions.Single()).blendShape, Is.EqualTo("Enter"));
        }

        [Test]
        public void NonUniformAttachmentTargetCreatesConstrainedReplacementWithMenuSettings() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var attachment = Attachment(nullSource: true);
            var source = attachment.GetSource(0).sourceTransform;
            source.localScale = new Vector3(1.3f, 0.7f, 2);
            Apply();
            var socket = Sockets.Single();
            var baked = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket, avatarRoot: root);
            Assert.That(baked.bakeRoot.parent.parent, Is.EqualTo(root.asVf()));
            Assert.That(baked.bakeRoot.GetConstraints().Single().GetFirstSource().parent, Is.EqualTo(source.owner()));
            Assert.That(socket.addLight, Is.EqualTo(LightType.Auto));
            Assert.That(socket.addMenuItem, Is.True);
            Assert.That(VRCFuryHapticSocketEditor.GetMenuName(socket), Is.EqualTo(HapticUtils.GetFallbackId(source)));
        }

        private ParentConstraint Attachment(float firstWeight = 1, float secondWeight = 0, bool nullSource = false) {
            var moving = Child("Moving DPS");
            Lights(moving);
            var constraint = moving.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource { sourceTransform = Child("Mouth").transform, weight = firstWeight });
            constraint.AddSource(new ConstraintSource { sourceTransform = nullSource ? null : Child("Hand").transform, weight = secondWeight });
            constraint.SetTranslationOffset(0, new Vector3(0.1f, 0.2f, 0.3f));
            constraint.SetRotationOffset(0, new Vector3(0, 40, 0));
            constraint.SetTranslationOffset(1, new Vector3(-0.1f, 0.1f, 0));
            constraint.locked = true;
            constraint.constraintActive = true;
            return constraint;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AttachmentSwitchCreatesMenuSocketsAtEachNonNullTarget(bool nullSource) {
            var constraint = Attachment(nullSource: nullSource);
            var moving = constraint.gameObject;
            var target = constraint.GetSource(0).sourceTransform;
            var expectedPosition = constraint.GetTranslationOffset(0) + Quaternion.Euler(constraint.GetRotationOffset(0)) * new Vector3(0.03f, -0.02f, 0.04f);
            var expectedForward = Quaternion.Euler(constraint.GetRotationOffset(0)) * Vector3.right;
            Apply(true);
            Assert.That(Sockets, Is.Empty);
            Assert.That(moving != null, Is.True);
            Apply();
            Assert.That(moving == null, Is.True);
            Assert.That(Sockets, Has.Length.EqualTo(nullSource ? 1 : 2));
            var socket = target.GetComponent<VRCFuryHapticSocket>();
            Assert.That(socket.addMenuItem, Is.True);
            Assert.That(socket.addLight, Is.EqualTo(LightType.Auto));
            Vector(socket.position, expectedPosition);
            Vector(Quaternion.Euler(socket.rotation) * Vector3.forward, expectedForward);
        }

        [Test]
        public void AttachmentConstraintCanBeHostedOutsideItsAffectedObject() {
            var original = Attachment();
            var moving = original.gameObject;
            var sources = original.GetSources();
            var host = Child("External constraint host");
            var constraint = VFConstraint.CreateParent(host);
            var targetField = constraint.GetComponent().GetType().VFField("TargetTransform");
            if (targetField == null) Assert.Ignore("Requires VRC constraints with target overrides");
            targetField.SetValue(constraint.GetComponent(), moving.transform);
            foreach (var source in sources) constraint.AddSource(source.sourceTransform, source.weight);
            Object.DestroyImmediate(original);

            Assert.That(constraint.GetAffectedObject(), Is.EqualTo(moving.asVf()));
            Apply();
            Assert.That(moving == null, Is.True);
            Assert.That(host != null, Is.True);
            Assert.That(Sockets, Has.Length.EqualTo(2));
            Assert.That(Sockets.Select(socket => socket.owner()), Is.EquivalentTo(sources.Select(source => source.sourceTransform.asVf())));
            Assert.That(Sockets.All(socket => socket.addMenuItem), Is.True);
        }

        [TestCase(0f, 0f)]
        [TestCase(0f, 1f)]
        public void AttachmentSwitchCanStartDisabledOrAtAnyTarget(float first, float second) {
            var constraint = Attachment(first, second);
            Apply();
            Assert.That(constraint == null, Is.True);
            Assert.That(Sockets, Has.Length.EqualTo(2));
        }

        [Test]
        public void MultipleWeightedSourcesAreNotAnAttachmentSwitch() {
            var constraint = Attachment(0.5f, 0.5f);
            var targets = Enumerable.Range(0, constraint.sourceCount).Select(i => constraint.GetSource(i).sourceTransform).ToArray();
            Apply();
            Assert.That(constraint != null, Is.True);
            Assert.That(targets.All(t => t.GetComponent<VRCFuryHapticSocket>() == null), Is.True);
        }

        [Test]
        public void SingleSourceConstraintRemainsASingleSocket() {
            var constraint = Attachment();
            constraint.RemoveSource(1);
            var owner = constraint.gameObject;
            Apply();
            Assert.That(constraint != null, Is.True);
            Assert.That(Sockets.Single().gameObject, Is.SameAs(owner));
        }

        [Test]
        public void DpsAttachNamedFallbackWithoutChildLightsStillUpgrades() {
            var constraint = Attachment();
            constraint.gameObject.name = "__dps_lightobject";
            foreach (var light in constraint.GetComponentsInChildren<Light>()) Object.DestroyImmediate(light.gameObject);
            Apply();
            Assert.That(constraint == null, Is.True);
            Assert.That(Sockets, Has.Length.EqualTo(2));
            foreach (var socket in Sockets) Vector(Quaternion.Euler(socket.rotation) * Vector3.forward,
                Quaternion.Euler(socket.transform.name == "Mouth" ? new Vector3(0, 40, 0) : Vector3.zero) * Vector3.down);
        }

        [TestCase("OGB_Baked_Pen", true)]
        [TestCase("BakedOGBPenetrator", true)]
        [TestCase("BakedHapticPlug", true)]
        [TestCase("BakedSpsPlug", true)]
        [TestCase("OGB_Baked_Orf", false)]
        [TestCase("BakedOGBOrifice", false)]
        [TestCase("BakedHapticSocket", false)]
        [TestCase("BakedSpsSocket", false)]
        public void OldBakeRestoresMetadataAndRemovesEntireBake(string bakeName, bool plug) {
            var owner = Child("Old component");
            var baked = Child(bakeName, owner.transform);
            var info = Child("Info", baked.transform);
            Child("name=Custom Name", info.transform);
            Child("size", info.transform).transform.localScale = new Vector3(0.3f, 0.04f, 1);
            Child("Old nested data", baked.transform);
            Apply(true);
            Assert.That(Plugs, Is.Empty);
            Assert.That(Sockets, Is.Empty);
            Assert.That(baked != null, Is.True);
            Apply();
            Assert.That(baked == null, Is.True);
            if (plug) {
                var component = Plugs.Single();
                Assert.That(component.name, Is.EqualTo("Custom Name"));
                Assert.That(component.length, Is.EqualTo(0.3f));
                Assert.That(component.radius, Is.EqualTo(0.04f));
                Assert.That(component.enableSps, Is.False);
            } else Assert.That(Sockets.Single().name, Is.EqualTo("Custom Name"));
        }

        [TestCase("OGB_Marker_Pen")]
        [TestCase("OGB_Marker_Hole")]
        [TestCase("OGB_Marker_Ring")]
        public void OldMarkersBecomeComponentsWithoutDuplicateUpgrades(string markerName) {
            var owner = Child("Marker Owner");
            var marker = Child(markerName, owner.transform);
            Apply(true);
            Assert.That(marker != null, Is.True);
            Apply();
            Assert.That(marker == null, Is.True);
            if (markerName.EndsWith("Pen")) Assert.That(Plugs.Single().gameObject, Is.SameAs(owner));
            else Assert.That(Sockets.Single().addLight, Is.EqualTo(markerName.EndsWith("Hole") ? LightType.Hole : LightType.Ring));
            Apply();
            Assert.That(Plugs.Length + Sockets.Length, Is.EqualTo(1));
        }

        [TestCase("self")]
        [TestCase("parent")]
        [TestCase("child")]
        public void ExistingSocketPreventsDuplicatesAcrossHierarchy(string location) {
            var owner = Child("Socket");
            Lights(owner);
            var existingOwner = location == "self" ? owner : location == "parent" ? root : Child("Existing", owner.transform);
            var existing = existingOwner.AddComponent<VRCFuryHapticSocket>();
            existing.addLight = LightType.Ring;
            existing.position = Vector3.one;
            Apply();
            Assert.That(Sockets, Is.EqualTo(new[] { existing }));
            Assert.That(existing.addLight, Is.EqualTo(LightType.Ring));
            Vector(existing.position, Vector3.one);
        }

        [TestCase("OGB_Receiver", "Custom", true)]
        [TestCase("Contact", "TPSVF_Depth", true)]
        [TestCase("Contact", "Finger", false)]
        [TestCase("Contact", "TPS_Orf_Root", false)]
        public void CleanupRemovesOnlyRecognizedLegacyContacts(string name, string tag, bool remove) {
            var owner = Child(name);
            owner.SetActive(false);
            var sender = owner.AddComponent<VRCContactSender>();
            sender.collisionTags = new List<string> { tag };
            var receiver = owner.AddComponent<VRCContactReceiver>();
            receiver.collisionTags = new List<string> { tag };
            var unrelated = owner.AddComponent<BoxCollider>();
            Apply(true);
            Assert.That(sender != null && receiver != null, Is.True);
            Apply();
            Assert.That(sender == null && receiver == null, Is.EqualTo(remove));
            Assert.That(unrelated != null, Is.True);
        }

        [TestCase("DPS_Holes")]
        [TestCase("DPS_Rings")]
        [TestCase("HotDog")]
        [TestCase("DPS Orifice")]
        [TestCase("Orifice Position")]
        public void RecognizedLegacyLayersAreRemoved(string layerName) {
            Lights(Child("Socket"));
            controller.AddLayer(layerName);
            Apply(true);
            Assert.That(controller.layers.Select(l => l.name), Does.Contain(layerName));
            Apply();
            Assert.That(controller.layers.Select(l => l.name), Is.EqualTo(new[] { "Unrelated" }));
        }

        [TestCase("none")]
        [TestCase("plug")]
        [TestCase("existing socket")]
        [TestCase("incomplete lights")]
        public void NoUpgradeableSocketPreservesLegacyLayersParametersMenusAndGuides(string setup) {
            if (setup == "plug") PlugMesh("TPS");
            if (setup == "existing socket") Child("Existing Socket").AddComponent<VRCFuryHapticSocket>();
            if (setup == "incomplete lights") Light(Child("Socket"), "Root", 0.41f, Vector3.zero);
            var guides = Child("GUIDES_DELETE");
            var guide = Child("Positioning guide", guides.transform);
            foreach (var layer in new[] { "DPS_Holes", "DPS_Rings", "HotDog", "DPS Orifice", "Orifice Position" })
                controller.AddLayer(layer);
            foreach (var parameter in new[] { "DPS_Hole", "DPS_Ring", "HotDog", "fluff/dps/orifice",
                         "EZDPS/Orifice", "TPS/Socket/VF/Depth", "OGB/Legacy", "Nsfw/Ori/Test" }) {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
                parameters.parameters = parameters.parameters.Concat(new[] {
                    new VRCExpressionParameters.Parameter { name = parameter }
                }).ToArray();
                menu.controls.Add(Toggle(parameter));
            }
            var original = EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters) + EditorJsonUtility.ToJson(menu);
            Apply(true);
            Assert.That(guides != null && guide != null, Is.True);
            Assert.That(EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters)
                + EditorJsonUtility.ToJson(menu), Is.EqualTo(original));
            Apply(false);
            Assert.That(guides != null && guide != null, Is.True);
            Assert.That(EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters)
                + EditorJsonUtility.ToJson(menu), Is.EqualTo(original));
            if (setup == "plug") Assert.That(Plugs, Has.Length.EqualTo(1));
        }

        [TestCase("DPS_Hole")]
        [TestCase("DPS_Ring")]
        [TestCase("HotDog")]
        [TestCase("fluff/dps/orifice")]
        [TestCase("EZDPS/Orifice")]
        [TestCase("TPS/Socket/VF/Depth")]
        [TestCase("OGB/Legacy")]
        [TestCase("Nsfw/Ori/Test")]
        public void RecognizedLegacyParametersRemoveTheirTogglesAndEmptyMenuAncestors(string parameter) {
            Lights(Child("Socket"));
            controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            parameters.parameters = parameters.parameters.Concat(new[] { new VRCExpressionParameters.Parameter { name = parameter } }).ToArray();
            var leaf = NewMenu();
            leaf.controls.Add(Toggle(parameter));
            var parent = NewMenu();
            parent.controls.Add(Submenu("Nested", leaf));
            menu.controls.Add(Submenu("DPS", parent));
            Apply(true);
            Assert.That(menu.controls, Has.Count.EqualTo(2));
            Apply();
            Assert.That(controller.parameters.Select(p => p.name), Is.EqualTo(new[] { "Unrelated" }));
            Assert.That(parameters.parameters.Select(p => p.name), Is.EqualTo(new[] { "Unrelated" }));
            Assert.That(menu.controls.Select(c => c.name), Is.EqualTo(new[] { "Unrelated" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AttachmentSpecificLayersRequireAnActualAttachmentSwitch(bool attachment) {
            if (attachment) Attachment();
            controller.AddLayer("TPS Orifice Animations");
            controller.AddLayer("EZDPS Orifices");
            Apply();
            Assert.That(controller.layers.Select(l => l.name).Contains("TPS Orifice Animations"), Is.EqualTo(!attachment));
            Assert.That(controller.layers.Select(l => l.name).Contains("EZDPS Orifices"), Is.EqualTo(!attachment));
        }

        private MeshRenderer PlugMesh(string kind, bool includeLength = false) {
            var obj = Child("Plug Mesh");
            var mesh = Track(new Mesh {
                vertices = new[] { new Vector3(-0.02f, 0, 0.01f), new Vector3(0.02f, 0, 0.01f),
                    new Vector3(-0.02f, 0, 0.3f), new Vector3(0.02f, 0, 0.3f) },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            });
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            var shaderName = kind == "Raliv" ? "Raliv/Penetrator" : kind == "XSToon" ? "XSToon/Fixture Penetrator"
                : kind == "UnityChan" ? "Fixture/DPS" : "Fixture/" + Guid.NewGuid().ToString("N");
            var property = kind == "TPS" ? "_TPSPenetratorEnabled" : kind == "UnityChan" ? "_ReCurvature" : "_PenetratorEnabled";
            var shader = Track(ShaderUtil.CreateShaderAsset("Shader \"" + shaderName + "\" { Properties { " + property
                + " (\"Enabled\", Float) = 1 "
                + (kind == "TPS" ? "_TPS_PenetratorForward (\"Forward\", Vector) = (0,0,1,0) "
                    + "_TPS_AnimatedToggle (\"Toggle\", Float) = 1 "
                    + "_TPS_IsSkinnedMeshRenderer (\"Baked Vertices\", Float) = 0 "
                    + "_TPS_BakedMesh (\"Mask\", 2D) = \"white\" {} " : "")
                + (includeLength ? "_Length (\"Length\", Float) = 0 " : "")
                + "} SubShader { Pass { } } }"));
            var material = Track(new Material(shader));
            if (kind == "Disabled") material.SetFloat(property, 0);
            renderer.sharedMaterial = material;
            return renderer;
        }

        [TestCase(0.08f)]
        [TestCase(0.09f)]
        [TestCase(0.48f)]
        [TestCase(0.49f)]
        public void OrdinaryWhiteLightsAreNotDeletedAsDpsTips(float range) {
            var light = Light(root, "Normal white light", range, Vector3.zero);
            light.color = Color.white;
            Assert.That(Apply(true), Does.Not.Contain("Normal white light"));
            Apply();
            Assert.That(light != null, Is.True);
        }

        [Test]
        public void RemovesLegacyTipWithOutOfRangeAlphaWithoutRemovingVisibleSpotlight() {
            var tip = Light(root, "Legacy tip", 0.49f, Vector3.zero);
            tip.color = new Color(1, 1, 1, 255);
            var spotlight = Light(root, "Spotlight", 0.49f, Vector3.zero);
            spotlight.type = UnityEngine.LightType.Spot;
            Apply();
            Assert.That(tip == null, Is.True);
            Assert.That(spotlight != null, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ManualImportPersistsExistingPrefabComponentSettings(bool socket) {
            var folder = "Assets/SpsUpgradePersistence_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            GameObject instance = null;
            try {
                GameObject source;
                if (socket) {
                    source = Child("Existing Socket");
                    source.AddComponent<VRCFuryHapticSocket>().addLight = LightType.None;
                    Lights(source);
                } else {
                    var renderer = PlugMesh("TPS");
                    source = renderer.gameObject;
                    var plug = source.AddComponent<VRCFuryHapticPlug>();
                    plug.enableSps = false;
                    plug.autoRenderer = false;
                    plug.configureTpsMesh.Add(renderer);
                    AssetDatabase.CreateAsset(renderer.sharedMaterial.shader, folder + "/Shader.asset");
                    AssetDatabase.CreateAsset(renderer.sharedMaterial, folder + "/Material.mat");
                    AssetDatabase.CreateAsset(renderer.GetComponent<MeshFilter>().sharedMesh, folder + "/Mesh.asset");
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Existing.prefab");
                Object.DestroyImmediate(source);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                VRCFuryInjectorBuilder.CreateForManualEditing(instance).GetService<SpsUpgrader>().Apply(instance, false);
                Assert.That(PrefabUtility.GetPropertyModifications(instance)
                    .Any(p => p.propertyPath == (socket ? "addLight" : "enableSps")), Is.True,
                    "Imported component settings must be recorded as prefab overrides");

                // Saving a variant exercises prefab override serialization without replacing the test runner's scene.
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Upgraded.prefab");
                Object.DestroyImmediate(instance);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(saved);
                if (socket) {
                    var component = instance.GetComponent<VRCFuryHapticSocket>();
                    Assert.That(component.addLight, Is.EqualTo(LightType.Hole));
                    Vector(component.position, new Vector3(0.03f, -0.02f, 0.04f));
                } else {
                    var component = instance.GetComponent<VRCFuryHapticPlug>();
                    Assert.That(component.enableSps, Is.True);
                    Assert.That(component.autoLength, Is.False);
                    Assert.That(component.length, Is.EqualTo(0.3f).Within(0.0001f));
                }
            } finally {
                if (instance != null) Object.DestroyImmediate(instance);
                assets.RemoveAll(asset => AssetDatabase.GetAssetPath(asset).StartsWith(folder + "/"));
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(0f, false)]
        [TestCase(0.3f, false)]
        [TestCase(0f, true)]
        public void ImportsTpsAnimatedToggleWithoutOverwritingAuthoredSpsValue(float value, bool authored) {
            var renderer = PlugMesh("TPS");
            renderer.sharedMaterial.SetFloat("_TPS_AnimatedToggle", value);
            if (authored) renderer.gameObject.AddComponent<VRCFuryHapticPlug>().spsAnimatedEnabled = 0.25f;
            Apply(true);
            Apply();
            Assert.That(Plugs.Single().spsAnimatedEnabled, Is.EqualTo(authored ? 0.25f : value));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ImportsTpsTextureMaskAndKeepsItAfterLegacyMaterialRemoval(bool unreadable, bool entirelyMasked) {
            var renderer = PlugMesh("TPS");
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            mesh.uv = new[] { new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.5f),
                new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.5f) };
            var texture = Track(new Texture2D(2, 1, TextureFormat.RGBA32, false, true));
            texture.SetPixels(new[] { new Color(0, 1, 1, 1), entirelyMasked ? Color.black : Color.white });
            texture.Apply(false, unreadable);
            renderer.sharedMaterial.SetTexture("_TPS_BakedMesh", texture);
            Apply(true);
            Assert.That(Plugs, Is.Empty);
            Apply();
            var plug = Plugs.Single();
            Assert.That(plug.length, Is.GreaterThan(0));
            Assert.That(plug.radius, Is.GreaterThan(0));
            renderer.sharedMaterials = Array.Empty<Material>();
            Assert.That(PlugMaskGenerator.GetMask(renderer, plug), Is.EqualTo(entirelyMasked
                ? new[] { 0f, 0f, 0f, 0f } : new[] { 0f, 1f, 0f, 1f }));
            Assert.That(texture.isReadable, Is.EqualTo(!unreadable), "The source texture must stay unchanged");
        }

        [Test]
        public void ImportsTpsBakedVertexMaskAcrossTextureRows() {
            var renderer = PlugMesh("TPS");
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices.ToList();
            while (vertices.Count < 1366) vertices.Add(vertices[0]);
            mesh.vertices = vertices.ToArray();
            mesh.triangles = new[] { 0, 1, 1364, 1, 1365, 1364 };
            var uvs = new Vector2[vertices.Count];
            uvs[1] = new Vector2(0.25f, 0.5f);
            uvs[1364] = new Vector2(0.5f, 0.5f);
            uvs[1365] = new Vector2(0.75f, 0.5f);
            mesh.uv = uvs;
            var texture = Track(new Texture2D(8190, 2, TextureFormat.RGBA32, false, true));
            var pixels = new Color32[8190 * 2];
            // Six RGBA-encoded IEEE floats per vertex; zero normals mark excluded vertices.
            foreach (var vertex in new[] { 1, 1365 }) pixels[vertex * 6 + 5] = new Color32(0, 0, 128, 63); // 1f
            texture.SetPixels32(pixels);
            texture.Apply();
            renderer.sharedMaterial.SetFloat("_TPS_IsSkinnedMeshRenderer", 1);
            renderer.sharedMaterial.SetTexture("_TPS_BakedMesh", texture);
            Apply();
            var plug = Plugs.Single();
            renderer.sharedMaterials = Array.Empty<Material>();
            var mask = PlugMaskGenerator.GetMask(renderer, plug);
            Assert.That(mask[0], Is.Zero);
            Assert.That(mask[1], Is.EqualTo(1));
            Assert.That(mask[1364], Is.Zero);
            Assert.That(mask[1365], Is.EqualTo(1));
        }

        [TestCase(FilterMode.Point, 0f)]
        [TestCase(FilterMode.Bilinear, 0.25f)]
        public void TpsTextureMaskPreservesSamplerAndFractionalWeights(FilterMode filterMode, float expected) {
            var renderer = PlugMesh("TPS");
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            mesh.uv = Enumerable.Repeat(new Vector2(0.375f, 0.5f), mesh.vertexCount).ToArray();
            var texture = Track(new Texture2D(2, 1, TextureFormat.RGBA32, false, true));
            texture.filterMode = filterMode;
            texture.SetPixels(new[] { Color.black, Color.white });
            texture.Apply();
            renderer.sharedMaterial.SetTexture("_TPS_BakedMesh", texture);
            Apply();
            var plug = Plugs.Single();
            var imported = plug.textureMask.Get();
            Assert.That(PlugMaskGenerator.GetMask(renderer, plug), Has.All.EqualTo(expected).Within(0.001f));
            Apply();
            Assert.That(plug.textureMask.Get(), Is.SameAs(imported),
                "An unchanged mask should reuse its existing asset");
            Assert.That(PlugMaskGenerator.GetMask(renderer, plug), Has.All.EqualTo(expected).Within(0.001f),
                "Repeated imports must not multiply the mask again");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImportedMaskIsSavedBesideMaterialOnlyForManualUpgrades(bool manual) {
            var folder = "Assets/SpsUpgradeMask_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            try {
                var renderer = PlugMesh("TPS");
                var texture = Track(new Texture2D(1, 1, TextureFormat.RGBA32, false, true));
                texture.SetPixel(0, 0, Color.black);
                texture.Apply();
                renderer.sharedMaterial.SetTexture("_TPS_BakedMesh", texture);
                AssetDatabase.CreateAsset(texture, folder + "/OriginalMask.asset");
                AssetDatabase.CreateAsset(renderer.sharedMaterial.shader, folder + "/Shader.asset");
                AssetDatabase.CreateAsset(renderer.sharedMaterial, folder + "/Original.mat");
                if (manual) Apply();
                else {
                    var build = VRCFuryInjectorBuilder.GetInjector(avatar);
                    build.GetService<ControllersService>().LoadAllControllers();
                    build.GetService<SpsUpgrader>().Apply(root, false);
                }
                var imported = Plugs.Single().textureMask.Get();
                var path = AssetDatabase.GetAssetPath(imported);
                if (manual) {
                    Assert.That(path, Does.StartWith(folder + "/Original SPS Mask"));
                    Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(path), Is.EqualTo(imported));
                } else {
                    Assert.That(path, Is.Empty, "Builds must keep imported masks in memory");
                }
                Assert.That(renderer.sharedMaterial.GetTexture("_TPS_BakedMesh"), Is.EqualTo(texture));
                Assert.That(imported, Is.Not.EqualTo(texture));
            } finally {
                assets.RemoveAll(asset => AssetDatabase.GetAssetPath(asset).StartsWith(folder + "/"));
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void ExistingSpsMaskIsNotReplacedByTpsMask() {
            var renderer = PlugMesh("TPS");
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 2, 1 }, 0);
            mesh.SetTriangles(new[] { 1, 2, 3 }, 1);
            var other = PlugMesh("Poiyomi").sharedMaterial;
            renderer.sharedMaterials = new[] { renderer.sharedMaterial, other };
            renderer.sharedMaterials[0].SetTexture("_TPS_BakedMesh", Texture2D.blackTexture);
            var plug = renderer.gameObject.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = false;
            plug.configureTpsMesh.Add(renderer);
            var authored = Track(new Texture2D(1, 1, TextureFormat.RGBA32, false, true));
            authored.SetPixel(0, 0, new Color(0.25f, 0.25f, 0.25f, 1));
            authored.Apply();
            plug.textureMask = authored;
            Apply();
            renderer.sharedMaterials = Array.Empty<Material>();
            var mask = PlugMaskGenerator.GetMask(renderer, plug);
            Assert.That(plug.textureMask.Get(), Is.SameAs(authored));
            Assert.That(mask, Has.All.EqualTo(0.75f).Within(0.01f));
        }

        [Test]
        public void TpsMaskUsesFirstMaterialWithAMask() {
            var renderer = PlugMesh("TPS");
            var second = PlugMesh("TPS");
            var third = PlugMesh("TPS");
            renderer.sharedMaterial.SetTexture("_TPS_BakedMesh", null);
            second.sharedMaterial.SetTexture("_TPS_BakedMesh", Texture2D.blackTexture);
            third.sharedMaterial.SetTexture("_TPS_BakedMesh", Texture2D.whiteTexture);
            renderer.sharedMaterials = new[] { renderer.sharedMaterial, second.sharedMaterial, third.sharedMaterial };
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(third.gameObject);
            Apply();
            Assert.That(PlugMaskGenerator.GetMask(renderer, Plugs.Single()), Has.All.Zero);
        }

        [TestCase("Raliv")]
        [TestCase("XSToon")]
        [TestCase("UnityChan")]
        [TestCase("Poiyomi")]
        [TestCase("TPS")]
        public void AddsPlugsForEachSupportedLegacyMaterialWithoutChangingMaterials(string kind) {
            var renderer = PlugMesh(kind);
            renderer.gameObject.SetActive(false);
            var material = renderer.sharedMaterial;
            var json = EditorJsonUtility.ToJson(material);
            Apply(true);
            Assert.That(Plugs, Is.Empty);
            Apply();
            var plug = Plugs.Single();
            Assert.That(plug.gameObject, Is.SameAs(renderer.gameObject));
            Assert.That(plug.enableSps, Is.True);
            Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(json));
            Apply();
            Assert.That(Plugs, Has.Length.EqualTo(1));
        }

        [TestCase("Raliv")]
        [TestCase("XSToon")]
        [TestCase("UnityChan")]
        [TestCase("Poiyomi")]
        public void NewPlugImportsDpsMaterialLengthBeforeMaterialIsRemoved(string kind) {
            var renderer = PlugMesh(kind, includeLength: true);
            renderer.transform.localScale = Vector3.one * 2;
            renderer.sharedMaterial.SetFloat("_Length", 0.45f);
            var before = EditorJsonUtility.ToJson(renderer.sharedMaterial);
            Apply(true);
            Assert.That(Plugs, Is.Empty);
            Apply();
            var plug = Plugs.Single();
            Assert.That(plug.autoLength, Is.False);
            Assert.That(plug.length, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(plug.radius, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(EditorJsonUtility.ToJson(renderer.sharedMaterial), Is.EqualTo(before));
            renderer.sharedMaterials = Array.Empty<Material>();
            Assert.That(PlugSizeDetector.GetWorldSize(plug).worldLength, Is.EqualTo(0.9f).Within(0.0001f));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ExistingPlugImportsDpsLengthInItsOwnUnitsAndKeepsExplicitLength(bool meters, bool explicitLength) {
            var renderer = PlugMesh("Raliv", includeLength: true);
            renderer.transform.localScale = Vector3.one * 2;
            renderer.sharedMaterial.SetFloat("_Length", 0.45f);
            // Include an unrelated earlier slot with its own _Length property.
            var other = PlugMesh("Disabled", includeLength: true);
            other.sharedMaterial.SetFloat("_Length", 2);
            renderer.sharedMaterials = new[] { other.sharedMaterial, renderer.sharedMaterial };
            var plug = Child("Existing Plug").AddComponent<VRCFuryHapticPlug>();
            plug.transform.localScale = Vector3.one * 3;
            plug.autoRenderer = false;
            plug.configureTpsMesh.Add(renderer);
            plug.unitsInMeters = meters;
            plug.autoLength = !explicitLength;
            plug.length = 0.2f;
            Apply();
            var expected = explicitLength ? 0.2f : meters ? 0.9f : 0.3f;
            Assert.That(plug.length, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(plug.autoLength, Is.False);
            renderer.sharedMaterials[1].SetFloat("_Length", 0.7f);
            Apply();
            Assert.That(plug.length, Is.EqualTo(expected).Within(0.0001f), "Captured length is now an explicit component setting");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDpsMaterialLengthFallsBackToMesh(float length) {
            var renderer = PlugMesh("Raliv", includeLength: true);
            renderer.sharedMaterial.SetFloat("_Length", length);
            Apply();
            Assert.That(Plugs.Single().length, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void TpsMaterialDoesNotImportAnUnrelatedLengthProperty() {
            var renderer = PlugMesh("TPS", includeLength: true);
            renderer.sharedMaterial.SetFloat("_Length", 2);
            Apply();
            Assert.That(Plugs.Single().length, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void DisabledMaterialAndMissingMeshDoNotCreatePlugs() {
            PlugMesh("Disabled");
            var missingMesh = PlugMesh("TPS");
            missingMesh.GetComponent<MeshFilter>().sharedMesh = null;
            Apply();
            Assert.That(Plugs, Is.Empty);
        }

        [Test]
        public void ExistingPlugWithExplicitRendererIsNotDuplicated() {
            var renderer = PlugMesh("TPS");
            var owner = Child("Separate Plug");
            var plug = owner.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = false;
            plug.configureTpsMesh.Add(renderer);
            Apply();
            Assert.That(Plugs, Is.EqualTo(new[] { plug }));
        }

        [Test]
        public void StandalonePlugCapturesPoseWithoutMovingObject() {
            var renderer = PlugMesh("TPS");
            renderer.transform.SetPositionAndRotation(new Vector3(0.2f, 0.3f, 0.4f), Quaternion.Euler(12, 34, 56));
            var owner = Child("Standalone Plug");
            var plug = owner.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.configureTpsMesh.Add(renderer);
            plug.length = 0.3f;
            plug.radius = 0.02f;
            Apply(true);
            Vector(owner.transform.localPosition, Vector3.zero);
            Apply();
            Assert.That(Plugs.Single(), Is.SameAs(plug));
            Assert.That(plug.gameObject, Is.SameAs(owner));
            Assert.That(owner.transform.childCount, Is.Zero);
            Vector(owner.transform.localPosition, Vector3.zero);
            var size = PlugSizeDetector.GetWorldSize(plug);
            Vector(owner.transform.TransformPoint(size.localPosition), renderer.transform.position);
            Assert.That(Quaternion.Angle(owner.transform.rotation * size.localRotation, renderer.transform.rotation), Is.LessThan(0.001f));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void SharedPlugObjectKeepsComponentReferenceHierarchyAndSettings(bool otherComponent, bool hasChildren) {
            var renderer = PlugMesh("TPS");
            var owner = Child("Authored Object");
            owner.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(12, 34, 56));
            owner.transform.localScale = Vector3.one * 2;
            owner.SetActive(false);
            if (otherComponent) owner.AddComponent<BoxCollider>();
            var userChild = hasChildren ? Child("User Child", owner.transform) : null;
            var matrix = owner.transform.localToWorldMatrix;
            var plug = owner.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.configureTpsMesh.Add(renderer);
            plug.enabled = false;
            plug.name = "Authored Name";
            plug.length = 0.3f;
            plug.radius = 0.02f;
            plug.spsAnimatedEnabled = 0.25f;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "Custom Channel", allowOthers = false });
            Apply(true);
            Assert.That(owner.GetComponent<VRCFuryHapticPlug>(), Is.SameAs(plug));
            Assert.That(owner.transform.Find("SPS Plug"), Is.Null);
            Apply();
            var moved = Plugs.Single();
            Assert.That(moved, Is.SameAs(plug));
            Assert.That(moved.gameObject, Is.SameAs(owner));
            Assert.That(owner.transform.Find("SPS Plug"), Is.Null);
            Assert.That(owner.transform.childCount, Is.EqualTo(hasChildren ? 1 : 0));
            var size = PlugSizeDetector.GetWorldSize(moved);
            Vector(moved.transform.TransformPoint(size.localPosition), renderer.transform.position);
            Assert.That(Quaternion.Angle(moved.transform.rotation * size.localRotation, renderer.transform.rotation), Is.LessThan(0.001f));
            Assert.That(owner.transform.localToWorldMatrix, Is.EqualTo(matrix));
            Assert.That(owner.activeSelf, Is.False);
            Assert.That(moved.gameObject.activeInHierarchy, Is.False);
            if (userChild != null) Assert.That(userChild.transform.parent, Is.SameAs(owner.transform));
            if (otherComponent) Assert.That(owner.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(moved.enabled, Is.False);
            Assert.That(moved.name, Is.EqualTo("Authored Name"));
            Assert.That(moved.length, Is.EqualTo(0.3f));
            Assert.That(moved.radius, Is.EqualTo(0.02f));
            Assert.That(moved.spsAnimatedEnabled, Is.EqualTo(0.25f));
            Assert.That(moved.channelSettings.channels.Single().id, Is.EqualTo("Custom Channel"));
            Assert.That(moved.channelSettings.channels.Single().allowOthers, Is.False);
            Apply();
            Assert.That(Plugs.Single(), Is.SameAs(moved));
            Assert.That(owner.transform.childCount, Is.EqualTo(hasChildren ? 1 : 0));
        }

        [Test]
        public void UpgradePreservesEnableActionsAndAnimationTargets() {
            var renderer = PlugMesh("TPS");
            var plug = renderer.gameObject.AddComponent<VRCFuryHapticPlug>();
            var action = new VF.Model.StateAction.SpsOnAction { target = plug };
            var toggle = new VF.Model.Feature.Toggle();
            toggle.state.actions.Add(action);
            var feature = Child("Toggle").AddComponent<VF.Model.VRCFury>();
            feature.content = toggle;
            var clip = VFClip.Create("Enable SPS");
            clip.SetCurve(plug, "spsAnimatedEnabled", 1);
            Apply();
            var moved = Plugs.Single();
            Assert.That(moved, Is.SameAs(plug));
            Assert.That(action.target, Is.SameAs(moved));
            Assert.That(((VF.Model.StateAction.SpsOnAction)((VF.Model.Feature.Toggle)feature.content).state.actions.Single()).target,
                Is.SameAs(moved));
            var binding = clip.GetFloatBindings().Single();
            Assert.That(binding.Targets(plug.owner()), Is.True);
            Assert.That(renderer.GetComponent<VRCFuryHapticPlug>(), Is.SameAs(plug));
        }

        private static void SamePlugGeometry(VRCFuryHapticPlug plug, PlugSizeDetector.SizeResult expected, Transform originalFrame) {
            var actual = PlugSizeDetector.GetWorldSize(plug);
            Vector(plug.transform.TransformPoint(actual.localPosition), originalFrame.TransformPoint(expected.localPosition));
            Assert.That(Quaternion.Angle(plug.transform.rotation * actual.localRotation,
                originalFrame.rotation * expected.localRotation), Is.LessThan(0.001f));
            Assert.That(actual.worldLength, Is.EqualTo(expected.worldLength).Within(0.0001f));
            Assert.That(actual.worldRadius, Is.EqualTo(expected.worldRadius).Within(0.0001f));
            Assert.That(actual.renderers, Is.EquivalentTo(expected.renderers));
        }

        [Test]
        public void PlugSizingIgnoresLegacyInputsUntilUpgraded() {
            var renderer = PlugMesh("TPS");
            renderer.sharedMaterial.SetVector("_TPS_PenetratorForward", new Vector4(1, 0, 0, 0));
            var plug = renderer.gameObject.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = plug.autoLength = plug.autoRadius = false;
            plug.length = 0.3f;
            plug.radius = 0.02f;
            var reference = Child("OGBTestBase", plug.transform);
            reference.transform.localPosition = new Vector3(0.1f, 0.02f, 0.03f);
            reference.transform.localRotation = Quaternion.Euler(12, 34, 56);
            var size = PlugSizeDetector.GetWorldSize(plug);
            Vector(size.localPosition, Vector3.zero);
            Assert.That(Quaternion.Angle(size.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            Apply();
            plug = Plugs.Single();
            size = PlugSizeDetector.GetWorldSize(plug);
            Vector(plug.transform.TransformPoint(size.localPosition), reference.transform.position);
            Assert.That(Quaternion.Angle(plug.transform.rotation * size.localRotation, reference.transform.rotation), Is.LessThan(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SocketBakingUsesComponentDataAndLeavesLegacyLightsForUpgrader(bool deform) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var owner = Child("Socket");
            var socket = owner.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = deform ? LightType.Ring : LightType.None;
            socket.position = new Vector3(-0.1f, 0.02f, 0.03f);
            socket.rotation = new Vector3(12, 34, 56);
            var (main, front) = Lights(owner);
            var context = new VRCFuryInjector();
            context.ImportScan(typeof(VFServiceAttribute));
            var result = context.GetService<VF.Inspector.VRCFuryHapticSocketBaker>().Bake(socket);
            context.GetService<SpsChannelsService>().Apply(result.channelContacts);
            Vector(result.bakeRoot.localPosition, socket.position);
            Assert.That(Quaternion.Angle(result.bakeRoot.localRotation, Quaternion.Euler(socket.rotation)), Is.LessThan(0.001f));
            Assert.That(main != null && front != null, Is.True);
            Assert.That(result.legacyLights != null, Is.EqualTo(deform && BuildTargetUtils.IsDesktop()));
        }

        [TestCase("TPS", false)]
        [TestCase("TPS", true)]
        [TestCase("Raliv", false)]
        [TestCase("Raliv", true)]
        [TestCase("Poiyomi", false)]
        [TestCase("Poiyomi", true)]
        public void UpgradedPlugPreservesGeometryAfterRemovingLegacyMaterials(string kind, bool existing) {
            var renderer = PlugMesh(kind);
            renderer.transform.localScale = Vector3.one * 2;
            renderer.transform.localRotation = Quaternion.Euler(12, 23, 34);
            if (kind == "TPS") {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                mesh.vertices = mesh.vertices.Select(v => Quaternion.Euler(0, 90, 0) * v).ToArray();
                renderer.sharedMaterial.SetVector("_TPS_PenetratorForward", new Vector4(1, 0, 0, 0));
            }
            var expectedRotation = renderer.transform.rotation * (kind == "TPS" ? Quaternion.Euler(0, 90, 0) : Quaternion.identity);
            VRCFuryHapticPlug original = null;
            if (existing) {
                original = Child("Existing Plug").AddComponent<VRCFuryHapticPlug>();
                original.enableSps = false;
                original.configureTps = true;
                original.autoRenderer = false;
                original.configureTpsMesh.Add(renderer);
            }
            Apply();
            var plug = Plugs.Single();
            if (existing) Assert.That(plug, Is.SameAs(original));
            Assert.That(plug.enableSps, Is.True);
            Assert.That(plug.configureTps, Is.EqualTo(existing), "The obsolete field is retained but ignored");
            Assert.That(plug.autoRenderer || plug.autoLength || plug.autoRadius, Is.False);
            Assert.That(plug.configureTpsMesh, Is.EqualTo(new[] { renderer }));
            renderer.sharedMaterials = Array.Empty<Material>();
            var size = PlugSizeDetector.GetWorldSize(plug);
            Assert.That(Quaternion.Angle(plug.transform.rotation * size.localRotation, expectedRotation), Is.LessThan(0.001f));
            Assert.That(size.worldLength, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(size.worldRadius, Is.EqualTo(0.04f).Within(0.0001f));
            var json = EditorJsonUtility.ToJson(plug);
            Apply();
            Assert.That(EditorJsonUtility.ToJson(plug), Is.EqualTo(json), "Repeated upgrades must preserve captured overrides");
        }

        [TestCase(0.41f)]
        [TestCase(0.42f)]
        public void SocketPreparationCapturesLightFrameBeforeBuildCleanup(float range) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var owner = Child("Legacy Socket");
            owner.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(12, 34, 56));
            owner.transform.localScale = Vector3.one * 2;
            var socket = owner.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = LightType.None;
            var (main, front) = Lights(owner, range);
            var expectedPosition = main.transform.position;
            var expectedForward = (front.transform.position - main.transform.position).normalized;
            var tip = Light(owner, "Tip", 0.49f, Vector3.zero);
            var visible = Light(owner, "Visible", 0.41f, Vector3.zero);
            visible.color = Color.white;
            SpsSocketPreparation.ImportSettingsFromLegacySystems(new[] { socket }, deleteLights: true);
            Assert.That(main == null && front == null, Is.True);
            Assert.That(tip != null && visible != null, Is.True);
            Assert.That(socket.addLight, Is.EqualTo(range == 0.41f ? LightType.Hole : LightType.Ring));
            Vector(owner.transform.TransformPoint(socket.position), expectedPosition);
            Vector(owner.transform.TransformDirection(Quaternion.Euler(socket.rotation) * Vector3.forward), expectedForward);
            var before = EditorJsonUtility.ToJson(socket);
            SpsSocketPreparation.ImportSettingsFromLegacySystems(new[] { socket }, deleteLights: true);
            Assert.That(EditorJsonUtility.ToJson(socket), Is.EqualTo(before));
            var services = new VRCFuryInjector();
            services.ImportScan(typeof(VFServiceAttribute));
            var baked = services.GetService<VF.Inspector.VRCFuryHapticSocketBaker>().Bake(socket);
            Vector(baked.bakeRoot.worldPosition, expectedPosition);
            Vector(baked.bakeRoot.TransformDirection(Vector3.forward), expectedForward);
        }

        [TestCase((int)LightType.Auto)]
        [TestCase((int)LightType.Hole)]
        [TestCase((int)LightType.Ring)]
        public void SocketPreparationPreservesAuthoredSettingsWhileRemovingLegacyLights(int type) {
            var owner = Child("Authored Socket");
            var socket = owner.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = (LightType)type;
            socket.position = new Vector3(0.1f, 0.2f, 0.3f);
            socket.rotation = new Vector3(12, 34, 56);
            var (main, front) = Lights(owner);
            var before = EditorJsonUtility.ToJson(socket);
            SpsSocketPreparation.ImportSettingsFromLegacySystems(new[] { socket }, deleteLights: true);
            Assert.That(main == null && front == null, Is.True);
            Assert.That(EditorJsonUtility.ToJson(socket), Is.EqualTo(before));
        }

        [TestCase("TPS", false, false, false)]
        [TestCase("TPS", true, false, false)]
        [TestCase("TPS", false, true, false)]
        [TestCase("TPS", true, false, true)]
        [TestCase("Raliv", true, false, false)]
        public void LegacyMaterialDeterminesPlugPoseRegardlessOfOldPositionAndDeformationSettings(
            string kind, bool enableSps, bool configureTps, bool oldAutoPosition) {
            var renderer = PlugMesh(kind);
            renderer.transform.localRotation = Quaternion.Euler(12, 34, 56);
            if (kind == "TPS") renderer.sharedMaterial.SetVector("_TPS_PenetratorForward", new Vector4(1, 0, 0, 0));
            var plug = Child("Plug").AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = enableSps;
            plug.configureTps = configureTps;
            plug.autoPosition = oldAutoPosition;
            plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.configureTpsMesh.Add(renderer);
            plug.length = 0.3f;
            plug.radius = 0.02f;
            Apply();
            var moved = Plugs.Single();
            var size = PlugSizeDetector.GetWorldSize(moved);
            Vector(moved.transform.TransformPoint(size.localPosition), renderer.transform.position);
            var expectedRotation = renderer.transform.rotation * (kind == "TPS" ? Quaternion.Euler(0, 90, 0) : Quaternion.identity);
            Assert.That(Quaternion.Angle(moved.transform.rotation * size.localRotation, expectedRotation), Is.LessThan(0.001f));
            Assert.That(moved.enableSps, Is.True);
            Assert.That(moved.configureTps, Is.EqualTo(configureTps), "The obsolete field is retained but ignored");
            Assert.That(moved.autoPosition, Is.EqualTo(oldAutoPosition), "The obsolete field should not be read or rewritten");
        }

        [Test]
        public void PreparationUsesSuppliedRendererAndConvertsLegacyMaterialInLaterSlot() {
            var unrelated = PlugMesh("Disabled");
            var selected = PlugMesh("TPS");
            selected.sharedMaterials = new[] { unrelated.sharedMaterial, selected.sharedMaterial };
            selected.sharedMaterials[1].SetVector("_TPS_PenetratorForward", new Vector4(1, 0, 0, 0));
            selected.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            selected.transform.localRotation = Quaternion.Euler(12, 23, 34);
            // Ordinary auto-search would pick the renderer on the plug's own object.
            var plug = unrelated.gameObject.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = plug.autoLength = plug.autoRadius = false;
            plug.length = 0.3f;
            plug.radius = 0.02f;
            SpsPlugPreparation.ImportSettingsFromLegacySystems(plug, new[] { selected });
            Assert.That(plug.enableSps, Is.True);
            Assert.That(plug.autoRenderer, Is.False);
            Assert.That(plug.configureTpsMesh, Is.EqualTo(new[] { selected }));
            Vector(plug.transform.TransformPoint(plug.position), selected.transform.position);
            Assert.That(Quaternion.Angle(plug.transform.rotation * Quaternion.Euler(plug.rotation),
                selected.transform.rotation * Quaternion.Euler(0, 90, 0)), Is.LessThan(0.001f));
        }

        [Test]
        public void PreparationPreservesAutomaticSettingsOnOrdinarySpsPlugs() {
            var renderer = PlugMesh("Disabled");
            var plug = renderer.gameObject.AddComponent<VRCFuryHapticPlug>();
            plug.position = new Vector3(0, 0, -0.01f);
            plug.rotation = new Vector3(0, 10, 0);
            var before = EditorJsonUtility.ToJson(plug);
            SpsPlugPreparation.ImportSettingsFromLegacySystems(plug, PlugRendererFinder.GetRenderers(plug));
            Assert.That(EditorJsonUtility.ToJson(plug), Is.EqualTo(before));
        }

        [Test]
        public void LegacyAutoSearchStillFindsOrdinaryChildRenderersWithoutPreparation() {
            var owner = Child("Old Plug");
            var renderer = PlugMesh("Disabled");
            renderer.transform.SetParent(owner.transform, false);
            var plug = owner.AddComponent<VRCFuryHapticPlug>();
            plug.useLegacyRendererFinder = true;
            var before = EditorJsonUtility.ToJson(plug);
            Assert.That(PlugRendererFinder.GetRenderers(plug), Is.EqualTo(new[] { renderer }));
            SpsPlugPreparation.ImportSettingsFromLegacySystems(plug, PlugRendererFinder.GetRenderers(plug));
            Assert.That(EditorJsonUtility.ToJson(plug), Is.EqualTo(before));
            Assert.That(PlugRendererFinder.GetRenderers(plug), Is.EqualTo(new[] { renderer }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlugPreservesSceneHelperFrameAfterHelperIsRemoved(bool enableSps) {
            var plug = Child("Plug").AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = enableSps;
            plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.length = 0.3f;
            plug.radius = 0.02f;
            var reference = Child("OGBTestBase", plug.transform);
            reference.transform.localPosition = new Vector3(0.1f, -0.2f, 0.03f);
            reference.transform.localRotation = Quaternion.Euler(12, 34, 56);
            var originalFrame = plug.transform;
            var expected = new PlugSizeDetector.SizeResult {
                renderers = Array.Empty<Renderer>(), worldLength = 0.3f, worldRadius = 0.02f,
                localPosition = reference.transform.localPosition, localRotation = reference.transform.localRotation
            };
            var json = EditorJsonUtility.ToJson(plug);
            Apply(true);
            Assert.That(EditorJsonUtility.ToJson(plug), Is.EqualTo(json));
            Apply();
            plug = Plugs.Single();
            Object.DestroyImmediate(reference);
            SamePlugGeometry(plug, expected, originalFrame);
        }

        [Test]
        public void LegacyRendererSearchBecomesAnExplicitReferenceBeforeMaterialsAreRemoved() {
            var owner = Child("Old Plug");
            var ordinary = PlugMesh("Disabled");
            ordinary.transform.SetParent(owner.transform, false);
            var selected = PlugMesh("TPS");
            selected.transform.SetParent(owner.transform, false);
            var plug = owner.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = false;
            plug.useLegacyRendererFinder = true;
            Assert.That(PlugRendererFinder.GetRenderers(plug), Is.EqualTo(new[] { selected }));
            var expected = new PlugSizeDetector.SizeResult {
                renderers = new[] { selected }, worldLength = 0.3f, worldRadius = 0.02f,
                localPosition = Vector3.zero, localRotation = Quaternion.identity
            };
            Apply();
            plug = Plugs.Single();
            selected.sharedMaterials = Array.Empty<Material>();
            SamePlugGeometry(plug, expected, owner.transform);
            Assert.That(plug.autoRenderer, Is.False);
            Assert.That(plug.configureTpsMesh, Is.EqualTo(new[] { selected }));
        }

        [Test]
        public void AuthoredPlugSettingsSurviveUpgrade() {
            var renderer = PlugMesh("Disabled");
            var plug = renderer.gameObject.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = false;
            plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.configureTpsMesh.Add(renderer);
            renderer.transform.localPosition = new Vector3(0, 0, -0.01f);
            renderer.transform.localRotation = Quaternion.Euler(0, 10, 0);
            plug.position = new Vector3(0, 0, -0.01f);
            plug.rotation = new Vector3(0, 5, 0);
            plug.name = "Authored Plug";
            plug.length = 0.2f;
            plug.radius = 0.03f;
            plug.unitsInMeters = true;
            var expected = PlugSizeDetector.GetWorldSize(plug);
            Apply();
            plug = Plugs.Single();
            Assert.That(plug.name, Is.EqualTo("Authored Plug"));
            Assert.That(plug.length, Is.EqualTo(0.2f));
            Assert.That(plug.radius, Is.EqualTo(0.03f));
            Assert.That(plug.unitsInMeters, Is.True);
            Assert.That(plug.enableSps, Is.False);
            renderer.sharedMaterials = Array.Empty<Material>();
            SamePlugGeometry(plug, expected, renderer.transform);
        }

        [Test]
        public void SkinnedPlugPreservesRendererRootBoneFrame() {
            var original = PlugMesh("TPS");
            var owner = original.gameObject;
            var mesh = owner.GetComponent<MeshFilter>().sharedMesh;
            var material = original.sharedMaterial;
            Object.DestroyImmediate(original);
            var bone = Child("Plug Root Bone");
            bone.transform.localPosition = new Vector3(0.005f, 0, 0);
            bone.transform.localRotation = Quaternion.Euler(0, 10, 0);
            mesh.boneWeights = mesh.vertices.Select(_ => new BoneWeight { boneIndex0 = 0, weight0 = 1 }).ToArray();
            mesh.bindposes = new[] { bone.transform.worldToLocalMatrix * owner.transform.localToWorldMatrix };
            var renderer = owner.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.bones = new[] { bone.transform };
            renderer.rootBone = bone.transform;
            var plug = owner.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = false;
            var dimensions = PlugSizeDetector.GetAutoWorldSize(new[] { renderer }, bone.transform.position, bone.transform.rotation);
            var expected = new PlugSizeDetector.SizeResult {
                renderers = new[] { renderer }, worldLength = dimensions.Item1, worldRadius = dimensions.Item2,
                localPosition = owner.transform.InverseTransformPoint(bone.transform.position),
                localRotation = Quaternion.Inverse(owner.transform.rotation) * bone.transform.rotation
            };
            Apply();
            plug = Plugs.Single();
            renderer.rootBone = null;
            renderer.sharedMaterials = Array.Empty<Material>();
            SamePlugGeometry(plug, expected, owner.transform);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UnbakingPreservesBakedRootPose(bool isPlug) {
            var owner = Child("Old Component");
            owner.transform.localRotation = Quaternion.Euler(12, 23, 34);
            owner.transform.localScale = Vector3.one * 2;
            var baked = Child(isPlug ? "BakedHapticPlug" : "BakedHapticSocket", owner.transform);
            baked.transform.localPosition = new Vector3(0.01f, -0.02f, 0.03f);
            baked.transform.localRotation = Quaternion.Euler(21, 32, 43);
            var expectedPosition = baked.transform.localPosition;
            var expectedRotation = baked.transform.localRotation;
            if (isPlug) Child("size", baked.transform).transform.localScale = new Vector3(0.3f, 0.02f, 1);
            Apply();
            Assert.That(baked == null, Is.True);
            if (isPlug) {
                var size = PlugSizeDetector.GetWorldSize(Plugs.Single());
                Vector(size.localPosition, expectedPosition);
                Assert.That(Quaternion.Angle(size.localRotation, expectedRotation), Is.LessThan(0.001f));
            } else {
                var frame = VF.Inspector.VRCFuryHapticSocketEditor.GetSocketTransformLocal(Sockets.Single()).Item2;
                Vector(frame.position, expectedPosition);
                Assert.That(Quaternion.Angle(frame.rotation, expectedRotation), Is.LessThan(0.001f));
            }
        }

        [TestCase(0.41f)]
        [TestCase(0.42f)]
        public void ExistingSocketCopiesLightSettingsBeforeDeletingLights(float range) {
            var owner = Child("Old Socket");
            var socket = owner.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = LightType.None;
            var (main, front) = Lights(owner, range);
            var expectedPosition = main.transform.localPosition;
            var expectedForward = (front.transform.localPosition - main.transform.localPosition).normalized;
            Apply();
            Assert.That(main == null && front == null, Is.True);
            Assert.That(socket.addLight, Is.EqualTo(range == 0.41f ? LightType.Hole : LightType.Ring));
            Vector(socket.position, expectedPosition);
            Vector(Quaternion.Euler(socket.rotation) * Vector3.forward, expectedForward);
            var frame = VF.Inspector.VRCFuryHapticSocketEditor.GetSocketTransformLocal(socket).Item2;
            Vector(frame.position, expectedPosition);
            Vector(frame.rotation * Vector3.forward, expectedForward);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AttachmentOffsetsAccountForDifferentSourceAndTargetScales(bool nestedOrifice) {
            var constraint = Attachment();
            constraint.transform.localScale = Vector3.one * 2;
            var target = constraint.GetSource(0).sourceTransform;
            target.localScale = Vector3.one * 4;
            if (nestedOrifice) {
                var orifice = Child("Orifice", constraint.transform);
                foreach (var light in constraint.GetComponentsInChildren<Light>()) light.transform.SetParent(orifice.transform, false);
            }
            var expectedPosition = (constraint.GetTranslationOffset(0)
                + Quaternion.Euler(constraint.GetRotationOffset(0)) * new Vector3(0.03f, -0.02f, 0.04f)) * 0.5f;
            Apply();
            Vector(target.GetComponent<VRCFuryHapticSocket>().position, expectedPosition);
            Assert.That(Sockets, Has.Length.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AttachmentPreservesPlacementOfEditedOrificeContainer(bool rotate) {
            var constraint = Attachment();
            var orifice = Child("Orifice", constraint.transform);
            foreach (var light in constraint.GetComponentsInChildren<Light>()) light.transform.SetParent(orifice.transform, false);
            // Users position the light pair by editing its container, rather than each light.
            if (rotate) orifice.transform.localRotation = Quaternion.Euler(0, 60, 0);
            else orifice.transform.localPosition = new Vector3(0.02f, 0.03f, -0.01f);
            var lights = orifice.GetComponentsInChildren<Light>();
            var localPosition = constraint.transform.InverseTransformPoint(lights[0].transform.position);
            var localForward = constraint.transform.InverseTransformDirection(lights[1].transform.position - lights[0].transform.position).normalized;
            var target = constraint.GetSource(0).sourceTransform;
            var offsetRotation = Quaternion.Euler(constraint.GetRotationOffset(0));
            var expectedPosition = constraint.GetTranslationOffset(0) + offsetRotation * localPosition;
            var expectedForward = offsetRotation * localForward;
            Apply();
            var socket = target.GetComponent<VRCFuryHapticSocket>();
            Vector(socket.position, expectedPosition);
            Vector(Quaternion.Euler(socket.rotation) * Vector3.forward, expectedForward);
        }

        [Test]
        public void UnbakingPreservesEffectivePlugDimensionsInsteadOfRedetectingFromMesh() {
            var renderer = PlugMesh("TPS");
            var baked = Child("BakedHapticPlug", renderer.transform);
            var info = Child("Info", baked.transform);
            // The old baked size can differ from the mesh because the user configured it manually.
            Child("size", info.transform).transform.localScale = new Vector3(0.2f, 0.03f, 1);
            Apply();
            var size = PlugSizeDetector.GetWorldSize(Plugs.Single());
            Assert.That(size.worldLength, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(size.worldRadius, Is.EqualTo(0.03f).Within(0.0001f));
        }

        [Test]
        public void ArbitraryMultiSourceConstraintsWithoutSocketLightsArePreserved() {
            var constraint = Attachment();
            foreach (var light in constraint.GetComponentsInChildren<Light>()) Object.DestroyImmediate(light.gameObject);
            Apply();
            Assert.That(constraint != null, Is.True);
            Assert.That(Sockets, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplacingAttachmentWithExistingComponentDoesNotReportDeletedObject(bool socketsOnly) {
            var constraint = Attachment();
            var oldSocket = constraint.gameObject.AddComponent<VRCFuryHapticSocket>();
            var preview = socketsOnly ? upgrader.ApplySockets(root, true) : Apply(true);
            Assert.That(preview, Does.Not.Contain("SPS already exists on:"));
            Assert.That(oldSocket != null, Is.True);
            var report = socketsOnly ? upgrader.ApplySockets(root, false) : Apply();
            Assert.That(oldSocket == null && constraint == null, Is.True);
            Assert.That(Sockets, Has.Length.EqualTo(2));
            Assert.That(report, Does.Contain("Socket component will be added to:"));
            Assert.That(report, Does.Not.Contain("SPS already exists on:"));
        }

        [Test]
        public void PlugMigrationErrorIdentifiesTheComponentAndRetainsTheCause() {
            var renderer = PlugMesh("TPS");
            renderer.GetComponent<MeshFilter>().sharedMesh = null;
            var plug = Child("Broken Plug").AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = false;
            plug.configureTpsMesh.Add(renderer);
            var error = Assert.Throws<ExceptionWithCause>(() => SpsPlugPreparation.ImportSettingsFromLegacySystems(new[] { plug }));
            Assert.That(error.Message, Does.Contain("Upgrade Test/Broken Plug"));
            Assert.That(error.InnerException, Is.TypeOf<VRCFBuilderException>());
            Assert.That(error.InnerException.Message, Does.Contain("Failed to detect plug length"));
        }

        [Test]
        public void AttachmentTargetsWithExistingSocketsAreNotOverwritten() {
            var constraint = Attachment();
            var existing = constraint.GetSource(0).sourceTransform.gameObject.AddComponent<VRCFuryHapticSocket>();
            existing.addLight = LightType.Ring;
            existing.position = Vector3.one;
            existing.name = "Authored Socket";
            Apply();
            Assert.That(Sockets, Has.Length.EqualTo(2));
            Assert.That(existing.addLight, Is.EqualTo(LightType.Ring));
            Vector(existing.position, Vector3.one);
            Assert.That(existing.name, Is.EqualTo("Authored Socket"));
            Assert.That(constraint == null, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BakeMetadataMayLiveDirectlyInBakeOrBeMissing(bool hasMetadata) {
            var baked = Child("OGB_Baked_Pen");
            if (hasMetadata) {
                Child("name=Old Format", baked.transform);
                Child("size", baked.transform).transform.localScale = new Vector3(0.2f, 0.03f, 1);
            }
            Apply();
            var plug = Plugs.Single();
            Assert.That(plug.name, Is.EqualTo(hasMetadata ? "Old Format" : ""));
            if (hasMetadata) {
                Assert.That(plug.length, Is.EqualTo(0.2f));
                Assert.That(plug.radius, Is.EqualTo(0.03f));
            }
            Assert.That(baked == null, Is.True);
        }

        [Test]
        public void BakeChildrenDoNotCreateAdditionalPlugsOrSockets() {
            var owner = Child("Old component");
            var baked = Child("BakedSpsPlug", owner.transform);
            var renderer = PlugMesh("TPS");
            renderer.transform.SetParent(baked.transform, false);
            Lights(baked);
            Apply();
            Assert.That(Plugs.Single().gameObject, Is.SameAs(owner));
            Assert.That(Sockets, Is.Empty);
            Assert.That(baked == null, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingPlugPreventsDuplicatesOnRelatedRenderers(bool parent) {
            var renderer = PlugMesh("TPS");
            var owner = parent ? root : Child("Existing Plug", renderer.transform);
            var existing = owner.AddComponent<VRCFuryHapticPlug>();
            Apply();
            Assert.That(Plugs, Has.Length.EqualTo(1));
            Assert.That(Plugs.Single(), Is.SameAs(existing));
        }

        [Test]
        public void SupportedMaterialInAnySlotCreatesOnlyOnePlug() {
            var renderer = PlugMesh("TPS");
            var other = PlugMesh("Disabled");
            renderer.sharedMaterials = new[] { other.sharedMaterial, renderer.sharedMaterial, renderer.sharedMaterial };
            Object.DestroyImmediate(other.gameObject);
            Apply();
            Assert.That(Plugs.Single().gameObject, Is.SameAs(renderer.gameObject));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SyntheticDpsAttachInstallationUpgradesAsAWhole(bool build) {
            // Represent DPS_ATTACH's two switchable light pairs, 7 hole targets, 11 ring targets,
            // three FX layers, expression parameters, nested menus, guides, and optional plug.
            foreach (var (parameter, count, range) in new[] { ("DPS_Hole", 7, 0.41f), ("DPS_Ring", 11, 0.42f) }) {
                var moving = Child(parameter + " Lights");
                Lights(moving, range);
                var constraint = moving.AddComponent<ParentConstraint>();
                for (var i = 0; i < count; i++) constraint.AddSource(new ConstraintSource {
                    sourceTransform = Child(parameter + " Target " + i).transform, weight = i == 0 ? 1 : 0
                });
                Child("OGB_Baked_Orf", moving.transform);
            }
            foreach (var layer in new[] { "DPS_Holes", "DPS_Rings", "HotDog" }) controller.AddLayer(layer);
            foreach (var parameter in new[] { "DPS_Hole", "DPS_Ring", "HotDog" }) {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Int);
                parameters.parameters = parameters.parameters.Concat(new[] {
                    new VRCExpressionParameters.Parameter { name = parameter }
                }).ToArray();
            }
            var legacyMenu = NewMenu();
            var secondPage = NewMenu();
            secondPage.controls.Add(Toggle("DPS_Ring"));
            legacyMenu.controls.Add(Toggle("DPS_Hole"));
            legacyMenu.controls.Add(Submenu("More Rings", secondPage));
            legacyMenu.controls.Add(Toggle("HotDog"));
            menu.controls.Add(Submenu("DPS", legacyMenu));
            var guides = Child("GUIDES_DELETE");
            Child("Guide Mesh", guides.transform);
            var plugRenderer = PlugMesh("Raliv");
            var original = EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters)
                + EditorJsonUtility.ToJson(menu) + EditorJsonUtility.ToJson(legacyMenu) + EditorJsonUtility.ToJson(secondPage);
            var services = build ? VRCFuryInjectorBuilder.GetInjector(avatar) : injector;
            if (build) services.GetService<ControllersService>().LoadAllControllers();
            var service = services.GetService<SpsUpgrader>();
            service.Apply(root, true);
            Assert.That(Sockets, Is.Empty);
            Assert.That(Plugs, Is.Empty);
            Assert.That(root.GetComponentsInChildren<ParentConstraint>(), Has.Length.EqualTo(2));
            Assert.That(EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters)
                + EditorJsonUtility.ToJson(menu) + EditorJsonUtility.ToJson(legacyMenu) + EditorJsonUtility.ToJson(secondPage), Is.EqualTo(original));
            service.Apply(root, false);
            Assert.That(Sockets, Has.Length.EqualTo(18));
            Assert.That(Sockets.All(s => s.addMenuItem && s.addLight == LightType.Auto), Is.True);
            Assert.That(Plugs.Single().gameObject, Is.SameAs(plugRenderer.gameObject));
            Assert.That(root.GetComponentsInChildren<ParentConstraint>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Light>(), Is.Empty);
            Assert.That(guides == null, Is.True);
            var controllers = services.GetService<ControllersService>();
            Assert.That(controllers.GetFx().GetLayers().Select(l => l.name), Is.EqualTo(new[] { "Unrelated" }));
            Assert.That(controllers.GetFx().parameters.Select(p => p.name), Is.EqualTo(new[] { "Unrelated" }));
            Assert.That(services.GetService<ExpressionParamsService>().Select(p => p.name), Is.EqualTo(new[] { "Unrelated" }));
            Assert.That(services.GetService<MenuService>().GetMenu().GetRaw().controls.Select(c => c.name), Is.EqualTo(new[] { "Unrelated" }));
            if (build) Assert.That(EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters)
                + EditorJsonUtility.ToJson(menu) + EditorJsonUtility.ToJson(legacyMenu) + EditorJsonUtility.ToJson(secondPage), Is.EqualTo(original));
        }

        [Test]
        public void MixedRecursiveMenusKeepUserItemsAndControllerReferences() {
            Lights(Child("Socket"));
            controller.AddParameter("DPS_Hole", AnimatorControllerParameterType.Int);
            var state = controller.layers[0].stateMachine.AddState("Uses Legacy Parameter");
            state.AddTransition(state).AddCondition(AnimatorConditionMode.Equals, 1, "DPS_Hole");
            var shared = NewMenu();
            shared.controls.Add(Toggle("DPS_Hole"));
            shared.controls.Add(Toggle("Unrelated"));
            shared.controls.Add(Submenu("Back", menu));
            menu.controls.Add(Submenu("Mixed", shared));
            menu.controls.Add(Submenu("Shared", shared));
            Apply(true);
            Assert.That(shared.controls, Has.Count.EqualTo(3));
            Apply();
            Assert.That(controller.parameters.Select(p => p.name), Does.Contain("DPS_Hole"));
            Assert.That(shared.controls.Select(c => c.name), Is.EqualTo(new[] { "Unrelated", "Back" }));
            Assert.That(menu.controls.Select(c => c.name), Is.EqualTo(new[] { "Unrelated", "Mixed", "Shared" }));
            Assert.That(shared.controls.Last().subMenu, Is.SameAs(menu));
        }

        [Test]
        public void ManualUpgradeWorksWithoutAnAvatarDescriptor() {
            Object.DestroyImmediate(avatar);
            var renderer = PlugMesh("TPS");
            var (main, front) = Lights(Child("Socket"));
            var manual = VRCFuryInjectorBuilder.CreateForManualEditing(root).GetService<SpsUpgrader>();
            var preview = manual.Apply(root, true);
            Assert.That(preview, Does.Contain("Plug component will be added").And.Contain("Socket component will be added"));
            Assert.That(Plugs, Is.Empty);
            Assert.That(Sockets, Is.Empty);
            Assert.That(main != null && front != null, Is.True);
            manual.Apply(root, false);
            Assert.That(Plugs.Single().gameObject, Is.SameAs(renderer.gameObject));
            Assert.That(Plugs.Single().enableSps, Is.True);
            Assert.That(Sockets, Has.Length.EqualTo(1));
            Assert.That(main == null && front == null, Is.True);
        }

        [Test]
        public void UnrelatedAvatarProducesNoUpgradeReport() {
            Assert.That(Apply(true), Is.Empty);
            Assert.That(Apply(), Is.Empty);
            Assert.That(Plugs, Is.Empty);
            Assert.That(Sockets, Is.Empty);
        }
    }
}
