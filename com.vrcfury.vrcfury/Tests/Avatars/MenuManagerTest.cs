using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Utils;
using VF.Service;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class MenuManagerTest {
        private readonly HashSet<VRCExpressionsMenu> menus = new HashSet<VRCExpressionsMenu>();

        private VRCExpressionsMenu Menu() {
            var menu = VrcfObjectFactory.Create<VRCExpressionsMenu>();
            menus.Add(menu);
            return menu;
        }

        private static void Link(VRCExpressionsMenu parent, string name, VRCExpressionsMenu child) {
            parent.controls.Add(new VRCExpressionsMenu.Control {
                name = name,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = child
            });
        }

        private static VRCExpressionsMenu Child(VRCExpressionsMenu menu, string name, int index = 0) {
            return menu.controls.Where(control => control.name == name).ElementAt(index).subMenu;
        }

        private MenuManager Manager(VRCExpressionsMenu root) {
            var controlsCreated = 0;
            return new MenuManager(root, () => {
                // Fail runaway expansion safely instead of overflowing Unity's editor stack.
                if (++controlsCreated > 100) throw new Exception("Menu merge expanded a cycle");
                return 0;
            });
        }

        [TearDown]
        public void Cleanup() {
            var pending = new Stack<VRCExpressionsMenu>(menus);
            while (pending.Count > 0) {
                var menu = pending.Pop();
                foreach (var control in menu.controls) {
                    if (control.subMenu != null && menus.Add(control.subMenu)) pending.Push(control.subMenu);
                }
            }
            foreach (var menu in menus) Object.DestroyImmediate(menu);
            menus.Clear();
        }

        [Test]
        public void MovePreservesCyclesThatReachTheDestinationRoot() {
            var root = Menu();
            var source = Menu();
            var third = Menu();
            Link(root, "B", third);
            Link(root, "B", root);
            Link(source, "B", root);
            Link(source, "A", root);
            Link(third, "A", source);
            Link(third, "B", root);

            Assert.That(Manager(root).Move("B/A", "A"), Is.True);

            Assert.That(Child(root, "A"), Is.SameAs(source));
            Assert.That(Child(root, "B"), Is.SameAs(third));
            Assert.That(Child(root, "B", 1), Is.SameAs(root));
            Assert.That(third.controls.Select(control => control.name), Is.EqualTo(new[] { "B" }));
            Assert.That(root.controls.Count, Is.EqualTo(3));
            Assert.That(source.controls.Count, Is.EqualTo(2));
            Assert.That(Child(source, "B"), Is.SameAs(root));
            Assert.That(Child(source, "A"), Is.SameAs(root));
            Assert.That(Child(third, "B"), Is.SameAs(root));
        }

        [Test]
        public void MergePreservesSelfLoopsSharedMenusAndControlMetadata() {
            var source = Menu();
            var shared = Menu();
            var root = Menu();
            Link(source, "Self", source);
            Link(source, "First", shared);
            Link(source, "Alias", shared);
            Link(shared, "Back", source);
            source.controls[2].value = 0.75f;
            source.controls[2].parameter = new VRCExpressionsMenu.Control.Parameter { name = "MenuParam" };

            Manager(root).MergeMenu(source);

            Assert.That(Child(root, "Self"), Is.SameAs(root));
            Assert.That(Child(root, "First"), Is.SameAs(Child(root, "Alias")));
            Assert.That(Child(Child(root, "First"), "Back"), Is.SameAs(root));
            Assert.That(root.controls.Select(control => control.name), Is.EqualTo(new[] { "Self", "First", "Alias" }));
            Assert.That(root.controls[2].value, Is.EqualTo(0.75f));
            Assert.That(root.controls[2].parameter.name, Is.EqualTo("MenuParam"));
            Assert.That(root.controls[2].parameter, Is.Not.SameAs(source.controls[2].parameter));
            var visited = 0;
            root.ForEachMenu((menu, path) => visited++);
            Assert.That(visited, Is.EqualTo(2));
        }

        [Test]
        public void MoveRenamesOriginalControlAndPreservesMetadataAndAliases() {
            var root = Menu();
            var source = Menu();
            Link(root, "Original", source);
            Link(root, "Alias", source);
            Link(source, "Back", root);
            var control = root.controls[0];
            control.value = 0.75f;
            control.parameter = new VRCExpressionsMenu.Control.Parameter { name = "Enabled" };

            Assert.That(Manager(root).Move("Original", "Parent/Renamed"), Is.True);

            Assert.That(Child(Child(root, "Parent"), "Renamed"), Is.SameAs(source));
            Assert.That(Child(root, "Alias"), Is.SameAs(source));
            Assert.That(Child(source, "Back"), Is.SameAs(root));
            Assert.That(Child(root, "Parent").controls.Single(), Is.SameAs(control));
            Assert.That(control.parameter.name, Is.EqualTo("Enabled"));
            Assert.That(control.value, Is.EqualTo(0.75f));
        }

        [Test]
        public void MoveRecursivelyCombinesExistingFoldersWithoutCopyingBackLinks() {
            var root = Menu();
            var source = Menu();
            var destination = Menu();
            var sourceChild = Menu();
            var destinationChild = Menu();
            Link(root, "Source", source);
            Link(root, "Destination", destination);
            Link(source, "Folder", sourceChild);
            Link(destination, "Folder", destinationChild);
            Link(sourceChild, "Back", root);
            Link(sourceChild, "Self", source);
            Link(destinationChild, "Self", destination);
            sourceChild.controls.Add(new VRCExpressionsMenu.Control { name = "Imported" });
            destinationChild.controls.Add(new VRCExpressionsMenu.Control { name = "Existing" });

            Assert.That(Manager(root).Move("Source", "Destination"), Is.True);

            Assert.That(root.controls.Select(control => control.name), Is.EqualTo(new[] { "Destination" }));
            Assert.That(Child(root, "Destination"), Is.SameAs(destination));
            Assert.That(Child(destination, "Folder"), Is.SameAs(destinationChild));
            Assert.That(Child(destinationChild, "Back"), Is.SameAs(root));
            Assert.That(Child(destinationChild, "Self"), Is.SameAs(destination));
            Assert.That(destinationChild.controls.Select(control => control.name), Is.EqualTo(new[] { "Self", "Existing", "Back", "Imported" }));
            Assert.That(sourceChild.controls.Last().name, Is.EqualTo("Imported"));
        }

        [Test]
        public void MoveCombinesDuplicateSubmenusByPosition() {
            var root = Menu();
            var source = Menu();
            var destination = Menu();
            Link(root, "Source", source);
            Link(root, "Destination", destination);
            for (var i = 0; i < 2; i++) {
                var fromChild = Menu();
                var toChild = Menu();
                Link(source, "Folder", fromChild);
                Link(destination, "Folder", toChild);
                Link(fromChild, "Back" + i, root);
            }
            var targets = destination.controls.Select(control => control.subMenu).ToArray();

            Assert.That(Manager(root).Move("Source", "Destination"), Is.True);

            Assert.That(destination.controls.Select(control => control.subMenu), Is.EqualTo(targets));
            Assert.That(targets[0].controls.Single().name, Is.EqualTo("Back0"));
            Assert.That(targets[1].controls.Single().name, Is.EqualTo("Back1"));
            Assert.That(Child(targets[0], "Back0"), Is.SameAs(root));
            Assert.That(Child(targets[1], "Back1"), Is.SameAs(root));
        }

        [Test]
        public void MoveOntoAliasOfSameMenuDoesNotDuplicateControls() {
            var root = Menu();
            var shared = Menu();
            Link(root, "Source", shared);
            Link(root, "Destination", shared);
            Link(shared, "Back", root);

            Assert.That(Manager(root).Move("Source", "Destination"), Is.True);

            Assert.That(root.controls.Single().subMenu, Is.SameAs(shared));
            Assert.That(shared.controls.Single().subMenu, Is.SameAs(root));
        }

        [Test]
        public void CombiningMenuWithItselfDoesNotDuplicateControls() {
            var root = Menu();
            Link(root, "Self", root);

            Manager(root).MergeMenu(Array.Empty<string>(), root, preserveSubmenuLinks: true);

            Assert.That(root.controls.Single().subMenu, Is.SameAs(root));
        }

        [Test]
        public void MoveRepeatedLinksCombinesEachSourceDestinationPairOnce() {
            var root = Menu();
            var source = Menu();
            var destination = Menu();
            Link(root, "Source", source);
            Link(root, "Source", source);
            Link(root, "Destination", destination);
            source.controls.Add(new VRCExpressionsMenu.Control { name = "Imported" });

            Assert.That(Manager(root).Move("Source", "Destination"), Is.True);

            Assert.That(root.controls.Single().subMenu, Is.SameAs(destination));
            Assert.That(destination.controls.Single().name, Is.EqualTo("Imported"));
        }

        [Test]
        public void MoveCombinesAllMatchingSourceControls() {
            var root = Menu();
            var destination = Menu();
            for (var i = 0; i < 2; i++) {
                var source = Menu();
                source.controls.Add(new VRCExpressionsMenu.Control { name = "Imported" + i });
                Link(root, "Source", source);
            }
            Link(root, "Destination", destination);

            Assert.That(Manager(root).Move("Source", "Destination"), Is.True);

            Assert.That(root.controls.Single().subMenu, Is.SameAs(destination));
            Assert.That(destination.controls.Select(control => control.name), Is.EqualTo(new[] { "Imported0", "Imported1" }));
        }

        [Test]
        public void MoveIntoEmptySubmenuKeepsSourceReferenceAndDestinationMetadata() {
            var root = Menu();
            var source = Menu();
            Link(root, "Source", source);
            Link(root, "Destination", null);
            var destination = root.controls[1];
            destination.value = 0.75f;
            Link(source, "Back", root);

            Assert.That(Manager(root).Move("Source", "Destination"), Is.True);

            Assert.That(root.controls.Single(), Is.SameAs(destination));
            Assert.That(destination.subMenu, Is.SameAs(source));
            Assert.That(destination.value, Is.EqualTo(0.75f));
            Assert.That(Child(source, "Back"), Is.SameAs(root));
        }

        [Test]
        public void MergeKeepsDuplicateFoldersSeparateAndAppendsInDepthFirstOrder() {
            var root = Menu();
            var firstDestination = Menu();
            var secondDestination = Menu();
            Link(root, "Folder", firstDestination);
            Link(root, "Folder", secondDestination);
            var source = Menu();
            var firstSource = Menu();
            var secondSource = Menu();
            Link(source, "Folder", firstSource);
            Link(source, "Folder", secondSource);
            Link(source, "Alias", firstSource);
            firstDestination.controls.Add(new VRCExpressionsMenu.Control { name = "Existing" });
            firstSource.controls.Add(new VRCExpressionsMenu.Control { name = "First" });
            secondSource.controls.Add(new VRCExpressionsMenu.Control { name = "Second" });
            var importedOrder = new List<string>();
            var manager = new MenuManager(root, () => {
                importedOrder.Add(firstDestination.controls.Count + ":" + secondDestination.controls.Count);
                return 0;
            });

            manager.MergeMenu(source);

            Assert.That(root.controls.Select(control => control.name), Is.EqualTo(new[] { "Folder", "Folder", "Alias" }));
            Assert.That(Child(root, "Folder"), Is.SameAs(firstDestination));
            Assert.That(Child(root, "Folder", 1), Is.SameAs(secondDestination));
            Assert.That(Child(root, "Alias"), Is.SameAs(firstDestination));
            Assert.That(firstDestination.controls.Select(control => control.name), Is.EqualTo(new[] { "Existing", "First" }));
            Assert.That(secondDestination.controls.Select(control => control.name), Is.EqualTo(new[] { "Second" }));
            Assert.That(importedOrder, Is.EqualTo(new[] { "1:0", "2:0", "2:1" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MergeNestedMenusWithBackEdge(bool move) {
            const int depth = 8;
            var source = Menu();
            var destination = Menu();
            var current = source;
            var target = destination;
            for (var i = 0; i < depth; i++) {
                var child = Menu();
                Link(current, "Next", child);
                current = child;
                if (move) {
                    var targetChild = Menu();
                    Link(target, "Next", targetChild);
                    target = targetChild;
                }
            }
            Link(current, "Back", source);
            var root = Menu();
            var manager = new MenuManager(root, () => 0);
            if (move) {
                Link(target, "Back", destination);
                Link(root, "Source", source);
                Link(root, "Destination", destination);
                Assert.That(manager.Move("Source", "Destination"), Is.True);
            } else {
                manager.MergeMenu(source);
                destination = root;
            }

            current = destination;
            for (var i = 0; i < depth; i++) current = Child(current, "Next");
            Assert.That(Child(current, "Back"), Is.SameAs(destination));
            Assert.That(menus.Contains(current), Is.EqualTo(move), "Only imports should copy submenu assets");
        }

        [Test]
        public void MenuPathsStillSupportEscapedSlashesAndDuplicateSelection() {
            var root = Menu();
            var first = Menu();
            var second = Menu();
            Link(root, "Folder", first);
            Link(root, "Folder", second);
            var manager = Manager(root);

            Assert.That(manager.GetSubmenu("Folder.dup.1"), Is.SameAs(second));
            var nested = manager.GetSubmenu(@"Group\/One/Nested");
            Assert.That(Child(Child(root, "Group/One"), "Nested"), Is.SameAs(nested));
            manager.NewMenuButton("Folder/Button");
            var button = first.controls.Single();

            Assert.That(manager.Move("Folder/Button", @"Group\/One/Nested/Renamed"), Is.True);

            Assert.That(first.controls, Is.Empty);
            Assert.That(nested.controls.Single(), Is.SameAs(button));
            Assert.That(button.name, Is.EqualTo("Renamed"));
            Assert.That(manager.SetIcon(@"Group\/One/Nested/Renamed", Texture2D.whiteTexture), Is.True);
            Assert.That(button.icon, Is.SameAs(Texture2D.whiteTexture));
            Assert.That(manager.Reorder(@"Group\/One/Nested/Renamed", 0), Is.True);
        }

        [Test]
        public void MergeKeepsDestinationMetadataAndNamesMenusAtSaveTime() {
            var root = Menu();
            var source = Menu();
            var shared = Menu();
            var nested = Menu();
            var manager = Manager(root);
            var existing = manager.GetSubmenu("Parent/Folder");
            var destinationControl = Child(root, "Parent").controls.Single();
            destinationControl.icon = Texture2D.whiteTexture;
            destinationControl.parameter = new VRCExpressionsMenu.Control.Parameter { name = "Existing" };
            Link(source, "Folder", shared);
            Link(shared, "Nested", nested);
            source.controls[0].parameter = new VRCExpressionsMenu.Control.Parameter { name = "Imported" };
            shared.controls[0].value = 0.75f;

            manager.MergeMenu(new[] { "Parent" }, source);
            var generated = Child(existing, "Nested");
            Assert.That(generated.name, Is.Empty);
            Assert.That(manager.Move("Parent", "Moved"), Is.True);
            SaveAssetsService.NameMenuAssets(root);

            Assert.That(Child(Child(root, "Moved"), "Folder"), Is.SameAs(existing));
            Assert.That(destinationControl.icon, Is.SameAs(Texture2D.whiteTexture));
            Assert.That(destinationControl.parameter.name, Is.EqualTo("Existing"));
            Assert.That(generated.name, Is.EqualTo("Moved » Folder » Nested"));
            Assert.That(existing.controls.Single().value, Is.EqualTo(0.75f));
            Assert.That(existing.controls.Single(), Is.Not.SameAs(shared.controls.Single()));
            Assert.That(Child(shared, "Nested"), Is.SameAs(nested));
        }

        [Test]
        public void SaveTimeMenuNamesHandleCyclesWithoutRenamingOriginals() {
            var root = Menu();
            var shared = Menu();
            var original = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menus.Add(original);
            original.name = "Authored menu";
            Link(root, "<b>Shared  Name</b>\nDescription", shared);
            Link(root, "Original", original);
            Link(shared, "Back", root);

            SaveAssetsService.NameMenuAssets(root);

            Assert.That(root.name, Is.EqualTo("VRCFury Menu"));
            Assert.That(shared.name, Is.EqualTo("Shared Name"));
            Assert.That(original.name, Is.EqualTo("Authored menu"));
            Assert.That(Child(shared, "Back"), Is.SameAs(root));
        }
    }
}
