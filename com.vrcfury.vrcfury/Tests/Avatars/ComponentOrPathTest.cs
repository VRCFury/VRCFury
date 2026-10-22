using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Inspector;
using VF.Model;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class ComponentOrPathTest {
        private class Fields : ScriptableObject {
            public ComponentOrPath<SkinnedMeshRenderer> skin = new ComponentOrPath<SkinnedMeshRenderer>();
            public ComponentOrPath<BoxCollider> collider = new ComponentOrPath<BoxCollider>();
        }

        [TestCase("skin", "SkinnedMeshRenderer")]
        [TestCase("collider", "BoxCollider")]
        public void DrawerUsesComponentTypeAndClearsReferenceWhenSelectingPath(string name, string typeName) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            var root = new GameObject("Avatar");
            try {
                owner.skin.component = root.AddComponent<SkinnedMeshRenderer>();
                owner.collider.component = root.AddComponent<BoxCollider>();
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty(name);
                    var gui = new ComponentOrPathDrawer().CreatePropertyGUI(property);
                    var dropdown = gui.Q<PopupField<string>>();
                    Assert.That(dropdown.choices, Is.EqualTo(new[] { typeName, "GameObject Path" }));
                    var referenceField = gui.Q<UnityEditor.UIElements.ObjectField>();
                    Assert.That(referenceField, Is.Not.Null);
                    Assert.That(referenceField.objectType.Name, Is.EqualTo(typeName));
                    Assert.That(referenceField.style.flexGrow.value, Is.EqualTo(1));
                    Assert.That(referenceField.style.flexBasis.value.value, Is.EqualTo(0));
                    ComponentOrPathDrawer.SetUsePath(property, true);
                    serialized.Update();
                    Assert.That(property.FindPropertyRelative("usePath").boolValue, Is.True);
                    Assert.That(property.FindPropertyRelative("component").objectReferenceValue, Is.Null);
                    var pathGui = new ComponentOrPathDrawer().CreatePropertyGUI(property);
                    var pathField = pathGui.Q<TextField>();
                    Assert.That(pathField, Is.Not.Null);
                    Assert.That(pathField.bindingPath, Is.EqualTo(property.FindPropertyRelative("path").propertyPath));
                    Assert.That(pathField.style.flexGrow.value, Is.EqualTo(1));
                    Assert.That(pathField.style.flexBasis.value.value, Is.EqualTo(0));
                }
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RawComponentImplicitlyConvertsToAReference() {
            var obj = new GameObject("Renderer");
            try {
                var skin = obj.AddComponent<SkinnedMeshRenderer>();
                ComponentOrPath<SkinnedMeshRenderer> reference = skin;
                Assert.That(reference.usePath, Is.False);
                Assert.That(reference.path, Is.Empty);
                Assert.That(reference.component, Is.SameAs(skin));
                Assert.That(reference.Resolve(null), Is.SameAs(skin));
                ComponentOrPath<SkinnedMeshRenderer> empty = (SkinnedMeshRenderer)null;
                Assert.That(empty, Is.Not.Null);
                Assert.That(empty.component, Is.Null);
                Assert.That(empty.Resolve(null), Is.Null);
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void ComponentAndPathResolveOnlyTheSpecifiedObject() {
            var root = new GameObject("Avatar");
            var child = new GameObject("Body");
            child.transform.SetParent(root.transform);
            try {
                var skin = child.AddComponent<SkinnedMeshRenderer>();
                var reference = new ComponentOrPath<SkinnedMeshRenderer> { component = skin };
                Assert.That(reference.Resolve(null), Is.SameAs(skin));
                reference.usePath = true;
                reference.path = "Missing";
                Assert.That(reference.Resolve(root), Is.Null);
                reference.path = "";
                Assert.That(reference.Resolve(root), Is.Null);
                reference.path = "Body";
                Assert.That(reference.Resolve(root), Is.SameAs(skin));
                Assert.That(reference.Resolve(null), Is.Null);
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
