using System.Collections.Generic;
using System.Linq;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    internal static class MenuEstimator {
        public static MenuManager Estimate(VFGameObject avatarObj) {
            var features = avatarObj.GetComponentsInSelfAndChildren<VRCFury>()
                .Where(c => !EditorOnlyUtils.IsInsideEditorOnly(c.owner()))
                .SelectMany(c => c.GetAllFeatures())
                .ToList();

            var root = VrcfObjectFactory.Create<VRCExpressionsMenu>();
            var merged = new MenuManager(root, () => 0);

            var avatar = avatarObj.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) {
                var origMenu = VRCAvatarUtils.GetAvatarMenu(avatar);
                if (origMenu != null) merged.MergeMenu(origMenu);
            }

            foreach (var fullController in features.OfType<FullController>()) {
                foreach (var menuEntry in fullController.menus) {
                    var menu = menuEntry.menu.Get();
                    if (menu == null) continue;
                    var prefix = MenuManager.SplitPath(menuEntry.prefix);
                    merged.MergeMenu(prefix, menu);
                }
            }

            foreach (var toggle in features.OfType<Toggle>()) {
                var hasTitle = !string.IsNullOrEmpty(toggle.name);
                var hasIcon = toggle.enableIcon && toggle.icon?.Get() != null;
                var addMenuItem = toggle.addMenuItem && (hasTitle || hasIcon);
                if (addMenuItem) {
                    merged.NewMenuButton(toggle.name);
                }
            }

            return merged;
        }
    }
}
