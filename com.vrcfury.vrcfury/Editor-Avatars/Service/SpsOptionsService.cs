using System.Linq;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class SpsOptionsService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly MenuChangesService menuChanges;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly MenuService menuService;
        [VFAutowired] private readonly DbtLayerService trees;
        private VFAParam legacyParameter;
        private VFAParam autoParameter;
        private VFClip legacyOnClip;

        public VFAParam GetAutoParameter() {
            var options = GetOptions();
            if (!options.autoModeAvailable) return null;
            if (autoParameter != null) return autoParameter;
            autoParameter = controllers.GetFx().CreateUniqueBool("autoMode", def: options.autoModeDefaultOn);
            autoParameter.MakeExpressionBool(synced: false, saved: options.autoModeSaved);
            var path = $"{GetMenuPath()}/<b>Auto<\\/b>";
            menuService.GetMenu().NewMenuToggle(path, autoParameter);
            return autoParameter;
        }

        public VFAParam GetLegacyParameter() {
            var options = GetOptions();
            if (!options.NeedsLegacyMode) return null;
            if (legacyParameter != null) return legacyParameter;
            legacyParameter = controllers.GetFx().CreateUniqueBool("legacy", def: options.legacyModeEnabledOnAvatarLoad);
            legacyParameter.MakeExpressionBool(saved: options.legacyModeSaved);
            legacyOnClip = VFClip.Create("Turn on SPS Legacy compatibility");
            trees.Create("SPS Legacy").Add(BlendtreeMath.GreaterThan(legacyParameter, 0).create(legacyOnClip, null));
            return legacyParameter;
        }

        public VFClip GetLegacyOnClip() {
            GetLegacyParameter();
            return legacyOnClip;
        }

        public void RegisterLegacyObject(VFGameObject obj) {
            var gate = GameObjects.Create("Legacy Mode", obj.parent);
            obj.SetParent(gate, false);
            gate.active = false;
            GetLegacyOnClip()?.SetEnabled(gate, true);
        }

        [FeatureBuilderAction(FeatureOrder.MoveSpsMenus)]
        public void MoveMenus() {
            var mainPath = GetMenuPath();
            // Add this after socket entries, including when only a plug's DPS tip needs it.
            if (legacyParameter != null) {
                var menu = menuService.GetMenu();
                menu.GetSubmenu(mainPath);
                // Menus sort by service ID, whose order is independent of build phase order.
                menu.OverrideSortPosition(int.MaxValue, () => menu.NewMenuToggle($"{mainPath}/<b>Legacy Support<\\/b>\n<size=20>DPS\\/TPS\\/SPS1\\/SPS2\nOne socket at a time", legacyParameter));
            }
            
            menuChanges.AddExtraAction(new MoveMenuItem {
                fromPath = "Holes",
                toPath = mainPath
            });
            menuChanges.AddExtraAction(new MoveMenuItem {
                fromPath = "Sockets",
                toPath = mainPath
            });
            if (mainPath != "SPS") {
                menuChanges.AddExtraAction(new MoveMenuItem {
                    fromPath = "SPS",
                    toPath = mainPath
                });
            }

            var icon = GetOptions().menuIcon?.Get()
                       ?? VRCFuryEditorUtils.GetResource<Texture2D>("sps_icon.png");
            menuChanges.AddExtraAction(new SetIcon {
                path = mainPath,
                icon = icon
            });

        }

        public SpsOptions GetOptions() {
            var opts = globals.allFeaturesInRun.OfType<SpsOptions>().FirstOrDefault();
            return opts ?? new SpsOptions();
        }

        public string GetMenuPath() {
            var path = GetOptions().menuPath;
            if (string.IsNullOrWhiteSpace(path)) {
                path = "SPS";
            }
            return path;
        }

    }
}
