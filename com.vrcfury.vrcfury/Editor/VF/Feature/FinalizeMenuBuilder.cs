using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {
    public class FinalizeMenuBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FinalizeMenu)]
        public void Apply() {
            var menuSettings = allFeaturesInRun.OfType<OverrideMenuSettings>().FirstOrDefault();
            var menu = manager.GetMenu();
            menu.SortMenu();
            MenuSplitter.SplitMenus(menu.GetRaw(), menuSettings);

            var iconsTooLarge = new HashSet<string>();

            void CheckIcon(Texture2D icon) {
                if (icon == null) return;

                var path = AssetDatabase.GetAssetPath(icon);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) {
                    return;
                }
                var settings = importer.GetDefaultPlatformTextureSettings();

                var MAX_ACTION_TEXTURE_SIZE = 256;
                if ((icon.width > MAX_ACTION_TEXTURE_SIZE || icon.height > MAX_ACTION_TEXTURE_SIZE) && settings.maxTextureSize > MAX_ACTION_TEXTURE_SIZE) {
                    iconsTooLarge.Add(path);
                    return;
                }

                //Compression
                if (settings.textureCompression == TextureImporterCompression.Uncompressed) {
                    iconsTooLarge.Add(path);
                    return;
                }
            }
            
            menu.GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                // VRChat doesn't care, but SDK3ToCCKConverter crashes if there are any null parameters
                // on a submenu. GestureManager crashes if there's any null parameters on ANYTHING.
                if (control.parameter == null) {
                    control.parameter = new VRCExpressionsMenu.Control.Parameter() {
                        name = ""
                    };
                }

                // Av3emulator crashes if subParameters is null
                if (control.subParameters == null) {
                    control.subParameters = new VRCExpressionsMenu.Control.Parameter[] { };
                }
                
                // The build will include assets and things from the linked submenu, even if the control
                // has been changed to something that isn't a submenu
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) {
                    control.subMenu = null;
                }
                
                //Check controls
                CheckIcon(control.icon);
                if (control.labels != null) {
                    foreach (var label in control.labels) {
                        CheckIcon(label.icon);
                    }
                }

                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });

            if (iconsTooLarge.Count > 0) {
                throw new Exception(
                    "You have some VRCFury props that are using menu icons larger than the VRCSDK will allow. Find these icons, and make" +
                    " sure the Max Size is set to 256:\n\n" + string.Join("\n", iconsTooLarge.OrderBy(n => n)));
            }
        }
    }
}
