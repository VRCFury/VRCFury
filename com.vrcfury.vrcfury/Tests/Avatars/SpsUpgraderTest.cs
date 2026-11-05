using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;
using LightType = VF.Component.VRCFuryHapticSocket.AddLight;

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsUpgraderTest {
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
            Object.DestroyImmediate(root);
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
        private string Apply(bool dryRun = false, int mode = 0) => upgrader.Apply(root, dryRun, (SpsUpgrader.Mode)mode);
        private VRCFuryHapticSocket[] Sockets => root.GetComponentsInChildren<VRCFuryHapticSocket>(true);
        private VRCFuryHapticPlug[] Plugs => root.GetComponentsInChildren<VRCFuryHapticPlug>(true);
        private static void Vector(Vector3 actual, Vector3 expected) => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));

        [TestCase(0.41f, false)]
        [TestCase(0.42f, false)]
        [TestCase(0.43f, false)]
        [TestCase(0.44f, false)]
        [TestCase(0.41f, true)]
        [TestCase(0.42f, true)]
        public void DpsAndTpsLightSocketsPreservePoseAndRemoveOnlyLegacySocketLights(float range, bool inactive) {
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
            Assert.That(visible != null && tip != null && unrelated != null, Is.True);
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
            Assert.That(main != null, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void UpgradeModesKeepTheirExistingCleanupContract(int mode) {
            var owner = Child("Socket");
            var (main, front) = Lights(owner);
            var guides = Child("GUIDES_DELETE");
            var contact = Child("OGB_Contact").AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "Legacy" };
            controller.AddLayer("DPS_Holes");
            controller.AddParameter("DPS_Hole", AnimatorControllerParameterType.Int);
            Apply(mode: mode);
            var socket = Sockets.Single();
            Assert.That(socket.fromSpsForAll, Is.EqualTo(mode == 2));
            Assert.That(socket.addLight, Is.EqualTo(mode == 2 ? LightType.None : LightType.Hole));
            Assert.That(main == null && front == null, Is.EqualTo(mode == 0));
            Assert.That(guides == null && contact == null, Is.EqualTo(mode == 0));
            Assert.That(controller.layers.Any(l => l.name == "DPS_Holes"), Is.EqualTo(mode != 0));
            Assert.That(controller.parameters.Any(p => p.name == "DPS_Hole"), Is.EqualTo(mode != 0));
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
            controller.AddLayer(layerName);
            Apply(true);
            Assert.That(controller.layers.Select(l => l.name), Does.Contain(layerName));
            Apply();
            Assert.That(controller.layers.Select(l => l.name), Is.EqualTo(new[] { "Unrelated" }));
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

        private MeshRenderer PlugMesh(string kind) {
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
                + " (\"Enabled\", Float) = 1 } SubShader { Pass { } } }"));
            var material = Track(new Material(shader));
            if (kind == "Disabled") material.SetFloat(property, 0);
            renderer.sharedMaterial = material;
            return renderer;
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
            Assert.That(plug.enableSps, Is.False);
            Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(json));
            Apply();
            Assert.That(Plugs, Has.Length.EqualTo(1));
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
            Assert.That(Plugs, Is.EqualTo(new[] { existing }));
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
            service.Apply(root, true, SpsUpgrader.Mode.Manual);
            Assert.That(Sockets, Is.Empty);
            Assert.That(Plugs, Is.Empty);
            Assert.That(root.GetComponentsInChildren<ParentConstraint>(), Has.Length.EqualTo(2));
            Assert.That(EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters)
                + EditorJsonUtility.ToJson(menu) + EditorJsonUtility.ToJson(legacyMenu) + EditorJsonUtility.ToJson(secondPage), Is.EqualTo(original));
            service.Apply(root, false, SpsUpgrader.Mode.Manual);
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
        public void UnrelatedAvatarProducesNoUpgradeReport() {
            Assert.That(Apply(true), Is.Empty);
            Assert.That(Apply(), Is.Empty);
            Assert.That(Plugs, Is.Empty);
            Assert.That(Sockets, Is.Empty);
        }
    }
}
