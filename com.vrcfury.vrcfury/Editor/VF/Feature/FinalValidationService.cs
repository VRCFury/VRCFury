using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Feature {
    /**
     * Many of these checks are copied from or modified from the validation checks in the VRCSDK
     */
    [VFService]
    internal class FinalValidationService : FeatureBuilder {
        [VFAutowired] private readonly ExceptionService excService;

        [FeatureBuilderAction(FeatureOrder.Validation)]
        public void Apply() {
            CheckParams();
            CheckContacts();
            CheckMenus();
            CheckMipmap();
        }

        private void CheckParams() {
            var p = manager.GetParams();
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some modified versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }
            if (p.GetRaw().CalcTotalCost() > maxBits) {
                excService.ThrowIfActuallyUploading(new SneakyException(
                    "Your avatar is out of space for parameters! Used "
                    + p.GetRaw().CalcTotalCost() + "/" + maxBits
                    + " bits. Ask your avatar creator, or the creator of the last prop you've added, if there are any parameters you can remove to make space."));
            }

            if (p.GetRaw().parameters.Length > 8192) {
                excService.ThrowIfActuallyUploading(new SneakyException(
                    $"Your avatar is using too many synced and unsynced expression parameters ({p.GetRaw().parameters.Length})!"
                    + " There's a limit of 8192 total expression parameters."));
            }
        }

        private void CheckContacts() {
            var contacts = avatarObject.GetComponentsInSelfAndChildren<ContactBase>().ToArray();
            var contactLimit = 256;
            if (contacts.Length > contactLimit) {
                var contactPaths = contacts
                    .Select(c => c.owner().GetPath(avatarObject))
                    .OrderBy(path => path)
                    .ToArray();
                Debug.Log("Contact report:\n" + string.Join("\n", contactPaths));
                var usesSps = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>().Any()
                              || avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any();
                if (usesSps) {
                    excService.ThrowIfActuallyUploading(new SneakyException(
                        "Your avatar is using more than the allowed number of contacts! Used "
                        + contacts.Length + "/" + contactLimit
                        + ". Delete some contacts or DPS/SPS items from your avatar."));
                } else {
                    excService.ThrowIfActuallyUploading(new SneakyException(
                        "Your avatar is using more than the allowed number of contacts! Used "
                        + contacts.Length + "/" + contactLimit
                        + ". Delete some contacts from your avatar."));
                }
            }
        }

        private void CheckMenus() {
            var menu = manager.GetMenu();

            // These methods were copied from the VRCSDK
            const int MAX_ACTION_TEXTURE_SIZE = 256;
            bool ValidateTexture(Texture2D texture)
            {
                string path = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    return true;
                TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();

                //Max texture size
                if ((texture.width > MAX_ACTION_TEXTURE_SIZE || texture.height > MAX_ACTION_TEXTURE_SIZE) &&
                    settings.maxTextureSize > MAX_ACTION_TEXTURE_SIZE)
                    return false;

                //Compression
                if (settings.textureCompression == TextureImporterCompression.Uncompressed)
                    return false;

                //Success
                return true;
            }
            void FixTexture(Texture2D texture)
            {
                string path = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    return;
                TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();

                //Max texture size
                if (texture.width > MAX_ACTION_TEXTURE_SIZE || texture.height > MAX_ACTION_TEXTURE_SIZE)
                    settings.maxTextureSize = Math.Min(settings.maxTextureSize, MAX_ACTION_TEXTURE_SIZE);

                //Compression
                if (settings.textureCompression == TextureImporterCompression.Uncompressed)
                    settings.textureCompression = TextureImporterCompression.Compressed;

                //Set & Reimport
                importer.SetPlatformTextureSettings(settings);
                AssetDatabase.ImportAsset(path);
            }

            var iconsTooLarge = new HashSet<Texture2D>();
            void CheckIcon(Texture2D icon) {
                if (icon == null) return;
                if (!ValidateTexture(icon)) {
                    iconsTooLarge.Add(icon);
                }
            }
            
            menu.GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                //Check controls
                CheckIcon(control.icon);
                if (control.labels != null) {
                    foreach (var label in control.labels) {
                        CheckIcon(label.icon);
                    }
                }

                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });

            if (iconsTooLarge.Any()) {
                var msg =
                    "You have some VRCFury props that are using menu icons larger than the VRCSDK will allow. Find these icons, and make" +
                    " sure the Max Size is set to 256 and that compression is enabled:\n\n" +
                    string.Join("\n", iconsTooLarge.Select(AssetDatabase.GetAssetPath).OrderBy(n => n));
                void autofix() {
                    foreach (var texture in iconsTooLarge) {
                        FixTexture(texture);
                    }
                };
                OfferAutoFix(msg, autofix);
            }
        }

        private void CheckMipmap() {
            var materials = new HashSet<Material>();
            materials.UnionWith(manager.AvatarObject.GetComponentsInSelfAndChildren<Renderer>()
                .SelectMany(r => r.sharedMaterials)
                .NotNull()
            );
            var materialsFromClips = manager.GetAllUsedControllers()
                .SelectMany(c => c.GetClips())
                .SelectMany(clip => clip.GetObjectCurves())
                .SelectMany(pair => pair.Item2)
                .Select(frame => frame.value)
                .OfType<Material>();
            materials.UnionWith(materialsFromClips);

            var textures = materials.SelectMany(m => {
                int[] texIDs = m.GetTexturePropertyNameIDs();
                if (texIDs == null) return new Texture[] { };
                return texIDs.Select(m.GetTexture).NotNull();
            });
            
            List<TextureImporter> badTextureImporters = new List<TextureImporter>();
            List<Texture> badTextures = new List<Texture>();
            foreach (var t in textures) {
                var path = AssetDatabase.GetAssetPath(t);
                if (string.IsNullOrEmpty(path)) continue;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.mipmapEnabled && !importer.streamingMipmaps) {
                    badTextureImporters.Add(importer);
                    badTextures.Add(t);
                }
            }

            if (!badTextureImporters.Any())
                return;

            var msg = "This avatar has mipmapped textures without 'Streaming Mip Maps' enabled:\n\n"
                + string.Join("\n", badTextures.Select(AssetDatabase.GetAssetPath).OrderBy(n => n));
            void autofix() {
                List<string> paths = new List<string>();
                foreach (TextureImporter t in badTextureImporters) {
                    Undo.RecordObject(t, "Set Mip Map Streaming");
                    t.streamingMipmaps = true;
                    t.streamingMipmapsPriority = 0;
                    EditorUtility.SetDirty(t);
                    paths.Add(t.assetPath);
                }

                AssetDatabase.ForceReserializeAssets(paths);
                AssetDatabase.Refresh();
            }
            OfferAutoFix(msg, autofix);
        }

        private void OfferAutoFix(string msg, Action autofix) {
            var ok = EditorUtility.DisplayDialog(
                "Warning",
                msg,
                "Auto-Fix",
                "Ignore (upload will fail)"
            );
            if (ok) {
                autofix();
            } else {
                excService.ThrowIfActuallyUploading(new Exception(msg));
            }
        }
    }
}
