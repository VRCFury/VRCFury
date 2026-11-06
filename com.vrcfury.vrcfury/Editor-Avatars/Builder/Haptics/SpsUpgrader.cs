using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using VF.Component;
using VF.Injector;
using VF.Menu;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SpsUpgrader {
        [VFAutowired] private readonly AvatarCleaner cleaner;
        [VFAutowired] private readonly GlobalsService globals;

        [MenuItem(MenuItems.upgradeLegacyHaptics, priority = MenuItems.upgradeLegacyHapticsPriority)]
        private static void MenuRun() {
            VRCFuryBuildContext.Run(Run);
        }

        [MenuItem(MenuItems.upgradeLegacyHaptics, true)]
        private static bool MenuCheck() {
            if (Selection.activeGameObject == null) return false;
            return true;
        }

        private const string dialogTitle = "SPS Upgrade";
        
        public static void Run() {
            var avatarObject = MenuUtils.GetSelectedAvatar();
            if (avatarObject == null) return;

            var upgrader = VRCFuryInjectorBuilder.CreateForManualEditing(avatarObject).GetService<SpsUpgrader>();
            var messages = upgrader.Apply(avatarObject, true);
            if (string.IsNullOrWhiteSpace(messages)) {
                DialogUtils.DisplayDialog(
                    dialogTitle,
                    "VRCFury failed to find any parts to upgrade! Ask on the discord?",
                    "Ok"
                );
                return;
            }
        
            var doIt = DialogUtils.DisplayDialog(
                dialogTitle,
                messages + "\n\nContinue?",
                "Yes, Do it!",
                "Cancel"
            );
            if (!doIt) return;

            upgrader.Apply(avatarObject, false);
            DialogUtils.DisplayDialog(
                dialogTitle,
                "Upgrade complete!",
                "Ok"
            );

            var sv = EditorWindowFinder.GetWindows<SceneView>().FirstOrDefault();
            if (sv != null) sv.drawGizmos = true;
        }

        public string Apply(VFGameObject avatarObject, bool dryRun) => Apply(avatarObject, dryRun, true, true);
        public string ApplyPlugs(VFGameObject avatarObject, bool dryRun) => Apply(avatarObject, dryRun, true, false);
        public string ApplySockets(VFGameObject avatarObject, bool dryRun) => Apply(avatarObject, dryRun, false, true);

        private string Apply(VFGameObject avatarObject, bool dryRun, bool upgradePlugs, bool upgradeSockets) {
            SpsPlugUpgrader plugs = null;
            SpsSocketUpgrader sockets = null;
            var context = new SpsUpgradeContext(avatarObject, dryRun,
                obj => (plugs?.ShouldRemoveObj(obj) ?? false) || (sockets?.ShouldRemoveObj(obj) ?? false));
            if (upgradePlugs) plugs = new SpsPlugUpgrader(context);
            if (upgradeSockets) sockets = new SpsSocketUpgrader(context);

            // Restore old baked/attachment setups before scanning their remaining renderers and lights.
            sockets?.RestoreAttachments();
            plugs?.RestoreBakedComponents();
            sockets?.RestoreBakedComponents();
            plugs?.Discover();
            sockets?.Discover();

            // Markers are fallback recovery: complete renderer/light setups take precedence.
            plugs?.RestoreMarkers();
            sockets?.RestoreMarkers();

            // Both sides finish reading legacy inputs before any source is removed.
            plugs?.Migrate(shouldSaveMasks: globals.useOriginalAssets);
            sockets?.Migrate();

            bool ShouldRemoveContact(UnityEngine.Component contact, List<string> tags) {
                if (tags.Any(t => t.StartsWith("TPSVF_"))) return true;
                if (!contact.owner().name.StartsWith("OGB_")) return false;
                if (plugs != null && sockets != null) return true;
                var owner = contact.owner().GetSelfAndAllParents()
                    .FirstOrDefault(obj => obj.GetComponent<VRCFuryHapticPlug>() || obj.GetComponent<VRCFuryHapticSocket>()
                        || (plugs?.Owns(obj) ?? false) || (sockets?.Owns(obj) ?? false));
                // Preserve unscoped contacts when their plug/socket ownership is unknown.
                if (owner == null) return false;
                return (plugs?.Owns(owner) ?? false) || (sockets?.Owns(owner) ?? false);
            }

            // Capture report text before cleanup destroys any of the referenced objects.
            var parts = new List<string>();
            var plugAdditions = plugs?.DescribeAdditions();
            var socketAdditions = sockets?.DescribeAdditions();
            if (plugAdditions != null) parts.Add(plugAdditions);
            if (socketAdditions != null) parts.Add(socketAdditions);
            var existing = new HashSet<VFGameObject>();
            if (sockets != null) existing.UnionWith(sockets.Existing);
            if (plugs != null) existing.UnionWith(plugs.Existing);
            var existingPaths = existing.Where(context.WillSurvive).Select(context.GetPath).ToArray();

            var parameterFilters = new[] { plugs?.ParameterFilter, sockets?.GetParameterFilter(includeUnscoped: plugs != null) }
                .Where(filter => filter != null).ToArray();
            var deletions = cleaner.Cleanup(
                avatarObject,
                perform: !dryRun,
                ShouldRemoveObj: obj => (plugs?.ShouldRemoveObj(obj) ?? false) || (sockets?.ShouldRemoveObj(obj) ?? false),
                ShouldRemoveLayer: sockets?.LayerFilter,
                ShouldRemoveParam: parameterFilters.Length == 0 ? null
                    : (System.Func<string, bool>)(param => parameterFilters.Any(filter => filter(param))),
                ShouldRemoveComponent: component => {
                    if ((plugs?.ShouldRemoveComponent(component) ?? false)
                        || (sockets?.ShouldRemoveComponent(component) ?? false)) return true;
                    if (component is VRCContactSender sender) return ShouldRemoveContact(sender, sender.collisionTags);
                    if (component is VRCContactReceiver receiver) return ShouldRemoveContact(receiver, receiver.collisionTags);
                    return false;
                }
            );

            if (!dryRun && globals.useOriginalAssets) {
                foreach (var plug in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) plug.Dirty();
                foreach (var socket in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) socket.Dirty();
            }

            if (deletions.Count > 0) parts.Add("These objects will be deleted:\n" + deletions.Join('\n'));
            if (existingPaths.Length > 0)
                parts.Add("SPS already exists on:\n" + existingPaths.Join('\n'));
            return parts.Join("\n\n");
        }
    }
}
