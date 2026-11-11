using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    internal class PoiyomiWarningsTest {
        private Shader shader;
        private Material material;

        [SetUp]
        public void SetUp() {
            shader = ShaderUtil.CreateShaderAsset(@"
Shader ""Hidden/Locked/VRCFuryWarningTest"" {
    Properties {
        _Float (""Float"", Float) = 0
        _Color (""Color"", Color) = (1,1,1,1)
        _Vector (""Vector"", Vector) = (0,0,0,0)
        _Tex (""Texture"", 2D) = ""white"" {}
        _Custom_ST (""Custom vector"", Vector) = (0,0,0,0)
        _Locked_Unit (""Renamed float"", Float) = 0
        _LockedColor_Unit (""Renamed color"", Color) = (1,1,1,1)
    }
    SubShader { Pass {} }
}");
            material = new Material(shader) { name = "Unit" };
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(shader);
        }

        [TestCase("_Float", "_Float", true)]
        [TestCase("_Float.x", "_Float", false)]
        [TestCase("_Float.", "_Float", false)]
        [TestCase("_Color", "_Color", true)]
        [TestCase("_Color.r", "_Color", true)]
        [TestCase("_Color.g", "_Color", true)]
        [TestCase("_Color.b", "_Color", true)]
        [TestCase("_Color.a", "_Color", true)]
        [TestCase("_Color.x", "_Color", false)]
        [TestCase("_Color.", "_Color", false)]
        [TestCase("_Vector", "_Vector", true)]
        [TestCase("_Vector.x", "_Vector", true)]
        [TestCase("_Vector.y", "_Vector", true)]
        [TestCase("_Vector.z", "_Vector", true)]
        [TestCase("_Vector.w", "_Vector", true)]
        [TestCase("_Vector.r", "_Vector", false)]
        [TestCase("_Vector.xy", "_Vector", false)]
        [TestCase("_Tex", "_Tex", true)]
        [TestCase("_Tex_ST.x", "_Tex", true)]
        [TestCase("_Tex_ST.y", "_Tex", true)]
        [TestCase("_Tex_ST.z", "_Tex", true)]
        [TestCase("_Tex_ST.w", "_Tex", true)]
        [TestCase("_Tex_TexelSize.x", "_Tex", true)]
        [TestCase("_Tex_TexelSize.y", "_Tex", true)]
        [TestCase("_Tex_TexelSize.z", "_Tex", true)]
        [TestCase("_Tex_TexelSize.w", "_Tex", true)]
        [TestCase("_Tex_ST.r", "_Tex", false)]
        [TestCase("_Tex_ST", "_Tex", false)]
        [TestCase("_Tex.x", "_Tex", false)]
        [TestCase("_Custom_ST.x", "_Custom_ST", true)]
        [TestCase("_Missing", "_Missing", false)]
        public void ChecksOnlySupportedBindingsAndObservesTagChanges(string binding, string property, bool expected) {
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, binding), Is.EqualTo(expected));
            foreach (var tag in new[] { "0", "1", "2" }) {
                material.SetOverrideTag(property + "Animated", tag);
                Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, binding), Is.False, tag);
            }
            material.SetOverrideTag(property + "Animated", "");
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, binding), Is.EqualTo(expected));
        }

        [Test]
        public void LockedShaderPropertiesUseOriginalNamesWhenRenameSuffixIsAvailable() {
            var suffix = PoiyomiUtils.GetRenameSuffix(material);
            var renamed = suffix == "Unit";
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, "_Locked"), Is.EqualTo(renamed));
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, "_Locked_Unit"), Is.EqualTo(!renamed));
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, "_LockedColor.r"), Is.EqualTo(renamed));
            material.SetOverrideTag("_LockedAnimated", "2");
            material.SetOverrideTag("_LockedColorAnimated", "2");
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, "_Locked"), Is.False);
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, "_LockedColor.r"), Is.False);
            if (renamed) {
                Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, "_Locked_Unit"), Is.False);
                Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, "_LockedColor_Unit.r"), Is.False);
            }
        }

        [Test]
        public void MissingMaterialsAndOrdinaryShadersDoNotWarn() {
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(null, "_Color"), Is.False);
            material.shader = Shader.Find("Unlit/Color");
            Assert.That(PoiyomiUtils.IsPoiyomiWithPropNonanimated(material, "_Color"), Is.False);
        }
    }
}
