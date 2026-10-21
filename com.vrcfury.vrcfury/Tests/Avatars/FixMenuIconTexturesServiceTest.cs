using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class FixMenuIconTexturesServiceTest {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void FixesActualOrSdkInvalidCompressionWithoutChangingTheImporter(bool defaultCompressed, bool platformCompressed) {
            var folder = "Assets/VRCFuryIconTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var path = folder + "/Icon.png";
            var pixels = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            Texture2D output = null;
            Texture2D original = null;
            try {
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.mipmapEnabled = false;
                var defaults = importer.GetDefaultPlatformTextureSettings();
                defaults.maxTextureSize = 512;
                defaults.textureCompression = defaultCompressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(defaults);
                var android = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
                var platform = importer.GetPlatformTextureSettings(android ? "Android" : "Standalone");
                platform.overridden = true;
                platform.maxTextureSize = 256;
                platform.format = platformCompressed
                    ? android ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.DXT5
                    : TextureImporterFormat.RGBA32;
                importer.SetPlatformTextureSettings(platform);
                importer.SaveAndReimport();
                original = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(IsCompressed(original), Is.EqualTo(platformCompressed), "Reproduction import settings");

                var control = new VRCExpressionsMenu.Control {
                    name = "Icon", icon = original,
                    labels = new[] { new VRCExpressionsMenu.Control.Label { icon = original } }
                };
                menu.controls.Add(control);
                var menuService = new MenuService();
                typeof(MenuService).VFField("_menu").SetValue(menuService, new MenuManager(menu, () => 0));
                var fix = new FixMenuIconTexturesService();
                typeof(FixMenuIconTexturesService).VFField("menuService").SetValue(fix, menuService);
                fix.Apply();
                output = control.icon;

                Assert.That(output == original, Is.EqualTo(defaultCompressed && platformCompressed));
                Assert.That(output.width, Is.LessThanOrEqualTo(256));
                Assert.That(output.height, Is.LessThanOrEqualTo(256));
                Assert.That(IsCompressed(output), Is.True);
                Assert.That(control.labels[0].icon, Is.SameAs(output), "Repeated icons should reuse the optimized texture");
                Assert.That(PassesSdkValidation(output), Is.True);
                Assert.That(((TextureImporter)AssetImporter.GetAtPath(path)).GetDefaultPlatformTextureSettings().textureCompression,
                    Is.EqualTo(defaults.textureCompression), "Do not change source importer settings");
            } finally {
                if (output != null && output != original) Object.DestroyImmediate(output);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(menu);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static bool IsCompressed(Texture2D texture) => GraphicsFormatUtility.IsCompressedFormat(
            GraphicsFormatUtility.GetGraphicsFormat(texture.format, true));

        // Mirrors the SDK's default-importer validation, not the active platform settings.
        private static bool PassesSdkValidation(Texture2D texture) {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer == null) return true;
            var settings = importer.GetDefaultPlatformTextureSettings();
            return !((texture.width > 256 || texture.height > 256) && settings.maxTextureSize > 256)
                && settings.textureCompression != TextureImporterCompression.Uncompressed;
        }
    }
}
