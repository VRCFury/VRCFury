using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Exceptions;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class AvatarCleanerTest {
        private GameObject root;
        private VRCAvatarDescriptor avatar;
        private string folder;
        private AnimatorController controller;
        private VRCExpressionParameters parameters;
        private VRCExpressionsMenu menu;
        private VRCFuryInjector injector;

        [SetUp]
        public void SetUp() {
            folder = "Assets/AvatarCleaner_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            root = new GameObject("Avatar Cleaner");
            avatar = root.AddComponent<VRCAvatarDescriptor>();
            controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Original.controller");
            var layers = controller.layers;
            layers[0].name = "Keep";
            controller.layers = layers;
            controller.AddLayer("Legacy");
            controller.AddParameter("Keep", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Legacy", AnimatorControllerParameterType.Bool);
            avatar.baseAnimationLayers = new[] {
                new VRCAvatarDescriptor.CustomAnimLayer {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    animatorController = controller,
                    isDefault = false
                }
            };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            avatar.customizeAnimationLayers = true;
            parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = new[] { "Keep", "Legacy" }.Select(name =>
                new VRCExpressionParameters.Parameter { name = name, valueType = VRCExpressionParameters.ValueType.Bool }).ToArray();
            AssetDatabase.CreateAsset(parameters, folder + "/OriginalParameters.asset");
            VRCAvatarUtils.SetAvatarParams(avatar, parameters);
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            foreach (var name in new[] { "Keep", "Legacy" }) menu.controls.Add(Toggle(name));
            AssetDatabase.CreateAsset(menu, folder + "/OriginalMenu.asset");
            VRCAvatarUtils.SetAvatarMenu(avatar, menu);
            AssetDatabase.SaveAssets();
            injector = VRCFuryInjectorBuilder.GetInjector(avatar);
        }

        private static VRCExpressionsMenu.Control Toggle(string name) => new VRCExpressionsMenu.Control {
            name = name,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter { name = name }
        };

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(folder);
        }

        private VFAvatarController LoadBuild() {
            var controllers = injector.GetService<ControllersService>();
            controllers.LoadAllControllers();
            var fx = controllers.GetFx();
            fx.NewLayer("Generated");
            fx.GetOrCreateBool("Generated");
            injector.GetService<ExpressionParamsService>().AddOrMerge(
                new VRCExpressionParameters.Parameter { name = "Generated", valueType = VRCExpressionParameters.ValueType.Bool });
            injector.GetService<MenuService>().GetMenu().NewMenuToggle("Generated", fx.GetOrCreateBool("Generated"));
            return fx;
        }

        private AvatarCleaner Cleaner(bool build) {
            if (!build) return VRCFuryInjectorBuilder.CreateForManualEditing(root).GetService<AvatarCleaner>();
            injector.GetService<ControllersService>().LoadAllControllers();
            return injector.GetService<AvatarCleaner>();
        }

        private string OriginalAssets() => EditorJsonUtility.ToJson(controller) + EditorJsonUtility.ToJson(parameters)
            + EditorJsonUtility.ToJson(menu);

        [TestCase(typeof(VRCContactReceiver), "rootTransform")]
        [TestCase(typeof(VRCContactSender), "rootTransform")]
        [TestCase(typeof(VRCPhysBone), "rootTransform")]
        [TestCase(typeof(VRCPhysBoneCollider), "rootTransform")]
        public void CleanupReportsComponentWithMalformedTransform(Type type, string fieldName) {
            var source = new GameObject("Dynamics");
            source.SetActive(false);
            source.transform.SetParent(root.transform, false);
            var target = new GameObject("Target");
            target.transform.SetParent(source.transform, false);
            var rootField = type.GetField(fieldName);
            rootField.SetValue(source.AddComponent(type), target.transform);
            var path = folder + "/Dynamics.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                prefab.transform.Find("Target").gameObject,
                out _,
                out long gameObjectId
            );
            // Editor assignments sanitize this. Loading malformed YAML creates a Transform
            // wrapper whose instance ID belongs to a GameObject, so Unity's null check passes.
            File.WriteAllText(
                path,
                Regex.Replace(
                    File.ReadAllText(path),
                    fieldName + @": \{fileID: -?\d+\}",
                    fieldName + ": {fileID: " + gameObjectId + "}"
                )
            );
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            instance.transform.SetParent(root.transform, false);
            target = instance.transform.Find("Target").gameObject;
            var component = instance.GetComponent(type);
            var brokenRoot = (Transform)rootField.GetValue(component);
            Assert.That(brokenRoot != null, Is.True);
            Assert.Throws<MissingReferenceException>(() => brokenRoot.IsChildOf(root.transform));
            var unrelated = new GameObject("Unrelated legacy object");
            unrelated.transform.SetParent(root.transform, false);
            var error = Assert.Throws<ExceptionWithCause>(() => Cleaner(false).Cleanup(
                root,
                perform: true,
                ShouldRemoveObj: obj => obj == unrelated.asVf()
            ));
            Assert.That(error.Message, Does.Contain(type.Name).And.Contain(component.owner().GetDebugPath()));
            Assert.That(error.InnerException, Is.TypeOf<MissingReferenceException>());
            Assert.That(unrelated != null, Is.True);
            Assert.That(component != null, Is.True);
            Assert.That(
                rootField.GetValue(component),
                Is.SameAs(brokenRoot),
                "Malformed references must not be rewritten"
            );
        }

        [Test]
        public void CleanupReportsConstraintWithMalformedTransform() {
            var type = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint");
            if (type == null) Assert.Ignore("VRC constraints are not available in this SDK");
            CleanupReportsComponentWithMalformedTransform(type, "TargetTransform");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CleansRawOrBuildState(bool build) {
            var original = OriginalAssets();
            var fx = build ? LoadBuild() : null;
            Cleaner(build).Cleanup(root, perform: true, ShouldRemoveLayer: name => name == "Legacy",
                ShouldRemoveParam: name => name == "Legacy");
            if (build) {
                Assert.That(OriginalAssets(), Is.EqualTo(original));
                Assert.That(fx.GetLayers().Select(l => l.name), Does.Not.Contain("Legacy").And.Contain("[VF0] Generated"));
                Assert.That(fx.parameters.Select(p => p.name), Does.Not.Contain("Legacy").And.Contain("Generated"));
                Assert.That(injector.GetService<ExpressionParamsService>().Select(p => p.name),
                    Is.EquivalentTo(new[] { "Keep", "Generated" }));
                Assert.That(injector.GetService<MenuService>().GetMenu().GetRaw().controls.Select(c => c.name),
                    Is.EquivalentTo(new[] { "Keep", "Generated" }));
            } else {
                Assert.That(controller.layers.Select(l => l.name), Is.EqualTo(new[] { "Keep" }));
                Assert.That(controller.parameters.Select(p => p.name), Is.EqualTo(new[] { "Keep" }));
                Assert.That(parameters.parameters.Select(p => p.name), Is.EqualTo(new[] { "Keep" }));
                Assert.That(menu.controls.Select(c => c.name), Is.EqualTo(new[] { "Keep" }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DryRunPreservesAssetsAndDescriptorReferences(bool initialized) {
            var fx = initialized ? LoadBuild() : null;
            var original = OriginalAssets();
            var originalParams = VRCAvatarUtils.GetAvatarParams(avatar);
            var originalMenu = VRCAvatarUtils.GetAvatarMenu(avatar);
            var menuJson = EditorJsonUtility.ToJson(originalMenu);
            var paramsJson = EditorJsonUtility.ToJson(originalParams);
            var result = Cleaner(true).Cleanup(root, ShouldRemoveLayer: _ => true, ShouldRemoveParam: _ => true);
            Assert.That(result, Is.Not.Empty);
            Assert.That(OriginalAssets(), Is.EqualTo(original));
            Assert.That(VRCAvatarUtils.GetAvatarParams(avatar), Is.SameAs(originalParams));
            Assert.That(VRCAvatarUtils.GetAvatarMenu(avatar), Is.SameAs(originalMenu));
            Assert.That(EditorJsonUtility.ToJson(originalMenu), Is.EqualTo(menuJson));
            Assert.That(EditorJsonUtility.ToJson(originalParams), Is.EqualTo(paramsJson));
            Assert.That(EditorUtility.IsDirty(controller), Is.False);
            Assert.That(EditorUtility.IsDirty(parameters), Is.False);
            Assert.That(EditorUtility.IsDirty(menu), Is.False);
            if (fx != null) Assert.That(fx.GetLayers().Count, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneOnlyCleanupDoesNotDirtyOrCloneAssets(bool build) {
            var child = GameObjects.Create("Remove", root);
            Cleaner(build).Cleanup(root, perform: true, ShouldRemoveObj: obj => obj == child);
            Assert.That(child == null, Is.True);
            Assert.That(VRCAvatarUtils.GetAvatarParams(avatar), Is.SameAs(parameters));
            Assert.That(VRCAvatarUtils.GetAvatarMenu(avatar), Is.SameAs(menu));
            Assert.That(EditorUtility.IsDirty(controller), Is.False);
            Assert.That(EditorUtility.IsDirty(parameters), Is.False);
            Assert.That(EditorUtility.IsDirty(menu), Is.False);
        }

        [Test]
        public void BuildCleanupUsesNamesAndKeepsGeneratedState() {
            var original = OriginalAssets();
            var fx = LoadBuild();
            Cleaner(true).Cleanup(root, perform: true, ShouldRemoveLayer: name => name == "Keep" || name == "Legacy",
                ShouldRemoveParam: name => name == "Keep" || name == "Legacy");
            Assert.That(OriginalAssets(), Is.EqualTo(original));
            Assert.That(fx.GetLayers().Select(l => l.name), Is.EqualTo(new[] { "[VF0] Generated" }));
            Assert.That(fx.parameters.Select(p => p.name), Is.EqualTo(new[] { "Generated" }));
            Assert.That(injector.GetService<ExpressionParamsService>().Select(p => p.name), Is.EqualTo(new[] { "Generated" }));
            Assert.That(injector.GetService<MenuService>().GetMenu().GetRaw().controls.Select(c => c.name),
                Is.EqualTo(new[] { "Generated" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KeepsControllerParametersUsedByRemainingLayers(bool build) {
            var state = controller.layers[0].stateMachine.AddState("Uses Legacy");
            state.AddTransition(state).AddCondition(AnimatorConditionMode.If, 0, "Legacy");
            var fx = build ? LoadBuild() : null;
            Cleaner(build).Cleanup(root, perform: true, ShouldRemoveLayer: name => name == "Legacy", ShouldRemoveParam: name => name == "Legacy");
            Assert.That(build ? fx.parameters.Select(p => p.name) : controller.parameters.Select(p => p.name), Does.Contain("Legacy"));
        }

        [Test]
        public void FirstBuildParameterCleanupDoesNotReuseAuthoredEntries() {
            Cleaner(true).Cleanup(root, perform: true, ShouldRemoveParam: name => name == "Legacy");
            var writable = injector.GetService<ExpressionParamsService>().Single(p => p.name == "Keep");
            Assert.That(writable, Is.Not.SameAs(parameters.parameters.Single(p => p.name == "Keep")));
            writable.defaultValue = 1;
            Assert.That(parameters.parameters.Single(p => p.name == "Keep").defaultValue, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DryRunWithParameterFilterDoesNotDirtySourceMenus(bool build) {
            Cleaner(build).Cleanup(root, ShouldRemoveParam: name => name == "Legacy");
            Assert.That(EditorUtility.IsDirty(menu), Is.False);
            Assert.That(EditorUtility.IsDirty(parameters), Is.False);
            Assert.That(EditorUtility.IsDirty(controller), Is.False);
        }

        [Test]
        public void CleansNestedMenusWithoutMutatingSharedOrRecursiveSourceMenus() {
            var nested = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            nested.controls.Add(Toggle("Legacy"));
            nested.controls.Add(Toggle("Keep"));
            nested.controls.Add(new VRCExpressionsMenu.Control {
                name = "Back", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = menu
            });
            AssetDatabase.CreateAsset(nested, folder + "/Nested.asset");
            menu.controls.Add(new VRCExpressionsMenu.Control {
                name = "Nested", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = nested
            });
            var original = OriginalAssets();
            var nestedJson = EditorJsonUtility.ToJson(nested);
            LoadBuild();
            Cleaner(true).Cleanup(root, perform: true, ShouldRemoveParam: name => name == "Legacy");
            var built = injector.GetService<MenuService>().GetMenu().GetRaw();
            Assert.That(OriginalAssets(), Is.EqualTo(original));
            Assert.That(EditorJsonUtility.ToJson(nested), Is.EqualTo(nestedJson));
            var copy = built.controls.Single(c => c.name == "Nested").subMenu;
            Assert.That(copy.controls.Select(c => c.name), Is.EquivalentTo(new[] { "Keep", "Back" }));
            Assert.That(copy.controls.Single(c => c.name == "Back").subMenu, Is.SameAs(built));
        }

        [Test]
        public void CleansImportedLayersByTheirCurrentNames() {
            var imported = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Imported.controller");
            var original = EditorJsonUtility.ToJson(imported);
            var fx = LoadBuild();
            var previousLayers = fx.GetLayers();
            fx.TakeOwnershipOf(VFController.Load(imported, new VFLoadContext()));
            var importedLayer = fx.GetLayers().Except(previousLayers).Single();
            var originalName = importedLayer.name;
            importedLayer.name = "Renamed Imported Layer";
            Cleaner(true).Cleanup(root, perform: true, ShouldRemoveLayer: name => name == originalName);
            Assert.That(fx.GetLayers(), Does.Contain(importedLayer));
            Cleaner(true).Cleanup(root, perform: true, ShouldRemoveLayer: name => name == "Renamed Imported Layer");
            Assert.That(fx.GetLayers().Select(l => l.name), Does.Not.Contain("Renamed Imported Layer").And.Contain("Keep"));
            Assert.That(EditorJsonUtility.ToJson(imported), Is.EqualTo(original));
        }

        [Test]
        public void DeletedObjectsDoNotHaveTheirSubAnimatorsRestoredLater() {
            LoadBuild();
            var child = GameObjects.Create("Child", root);
            var animator = child.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            var holder = injector.GetService<AnimatorHolderService>();
            holder.ApplyBefore();
            holder.GetSubControllers();
            Cleaner(true).Cleanup(root, perform: true, ShouldRemoveObj: obj => obj == child);
            holder.ApplyAfter();
            Assert.That(child == null, Is.True);
            Assert.That(controller.layers, Has.Length.EqualTo(2));
        }

        [Test]
        public void AutomaticSpsUpgradeCleansBuildCopiesWithoutChangingSourceAssets() {
            foreach (var range in new[] { 0.41f, 0.45f }) {
                var light = GameObjects.Create("Legacy Socket Light", root).AddComponent<Light>();
                light.color = Color.black;
                light.range = range;
                light.transform.localPosition = range == 0.41f ? Vector3.zero : Vector3.forward * 0.01f;
            }
            var layers = controller.layers;
            layers[1].name = "DPS Orifice";
            controller.layers = layers;
            controller.AddParameter("DPS_Hole", AnimatorControllerParameterType.Bool);
            parameters.parameters[1].name = "DPS_Hole";
            menu.controls[1].parameter.name = "DPS_Hole";
            var original = OriginalAssets();
            var fx = LoadBuild();
            injector.GetService<SpsUpgrader>().Apply(root, false);
            Assert.That(OriginalAssets(), Is.EqualTo(original));
            Assert.That(fx.GetLayers().Select(l => l.name), Does.Not.Contain("DPS Orifice").And.Contain("[VF0] Generated"));
            Assert.That(fx.parameters.Select(p => p.name), Does.Not.Contain("DPS_Hole").And.Contain("Generated"));
            Assert.That(injector.GetService<ExpressionParamsService>().Select(p => p.name), Does.Not.Contain("DPS_Hole").And.Contain("Generated"));
            Assert.That(injector.GetService<MenuService>().GetMenu().GetRaw().controls.Select(c => c.parameter.name),
                Does.Not.Contain("DPS_Hole").And.Contain("Generated"));
        }

        [Test]
        public void ManualServicesUseOriginalMenuAndExpressionParameters() {
            var manual = VRCFuryInjectorBuilder.CreateForManualEditing(root);
            Assert.That(manual.GetService<MenuService>().GetMenu().GetRaw(), Is.SameAs(menu));
            Assert.That(manual.GetService<ExpressionParamsService>().First(), Is.SameAs(parameters.parameters[0]));
            Assert.That(manual.GetService<GlobalsService>().useOriginalAssets, Is.True);
            Assert.That(injector.GetService<GlobalsService>().useOriginalAssets, Is.False);
        }

        [Test]
        public void MissingDescriptorIsNotRegisteredAsNull() {
            Object.DestroyImmediate(avatar);
            var manual = VRCFuryInjectorBuilder.CreateForManualEditing(root);
            Assert.Throws<Exception>(() => manual.GetService<VRCAvatarDescriptor>());
            Assert.Throws<Exception>(() => manual.GetService<ControllersService>());
            Assert.DoesNotThrow(() => manual.GetService<AvatarCleaner>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CleanupWithoutDescriptorHandlesSceneObjectsAndSkipsAvatarAssets(bool perform) {
            Object.DestroyImmediate(avatar);
            var manual = VRCFuryInjectorBuilder.CreateForManualEditing(root);
            var child = GameObjects.Create("Remove", root);
            var original = OriginalAssets();
            var result = manual.GetService<AvatarCleaner>().Cleanup(root, perform: perform,
                ShouldRemoveObj: obj => obj == child,
                ShouldRemoveLayer: _ => throw new Exception("A root without a descriptor has no avatar layers"),
                ShouldRemoveParam: _ => throw new Exception("A root without a descriptor has no avatar parameters"));
            Assert.That(result, Does.Contain("Object: Remove"));
            Assert.That(child == null, Is.EqualTo(perform));
            Assert.That(OriginalAssets(), Is.EqualTo(original));
        }

        [Test]
        public void ManualCleanupKeepsAssetReferences() {
            Cleaner(false).Cleanup(root, perform: true, ShouldRemoveLayer: _ => true, ShouldRemoveParam: _ => true);
            Assert.That(VRCAvatarUtils.GetAvatarParams(avatar), Is.SameAs(parameters));
            Assert.That(VRCAvatarUtils.GetAvatarMenu(avatar), Is.SameAs(menu));
            Assert.That(VRCAvatarUtils.GetAllControllers(avatar).Single().controller, Is.SameAs(controller));
            Assert.That(parameters.parameters, Is.Empty);
            Assert.That(menu.controls, Is.Empty);
            Assert.That(controller.layers, Is.Empty);
        }

        [Test]
        public void ManualCommitPreservesUntouchedControllerData() {
            controller.AddParameter("UnusedInt", AnimatorControllerParameterType.Int);
            var authored = controller.parameters;
            authored.Last().defaultInt = -123;
            controller.parameters = authored;
            var layers = controller.layers;
            layers[0].defaultWeight = 0.375f;
            controller.layers = layers;
            var originalMachine = controller.layers[0].stateMachine;
            Cleaner(false).Cleanup(root, perform: true,
                ShouldRemoveLayer: name => name == "Legacy", ShouldRemoveParam: name => name == "Legacy");
            Assert.That(controller.layers[0].defaultWeight, Is.EqualTo(0.375f));
            Assert.That(controller.layers[0].stateMachine, Is.SameAs(originalMachine));
            Assert.That(controller.parameters.Select(p => p.name), Is.EqualTo(new[] { "Keep", "UnusedInt" }));
            Assert.That(controller.parameters[1].type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(controller.parameters[1].defaultInt, Is.EqualTo(-123));
        }
    }
}
