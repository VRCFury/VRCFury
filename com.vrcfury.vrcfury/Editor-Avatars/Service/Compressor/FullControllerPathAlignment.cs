using System;
using System.Collections.Generic;
using System.Linq;
using VF.Feature;
using VF.Utils;

namespace VF.Service.Compressor {
    internal static class FullControllerPathAlignment {
        [Serializable]
        internal class Controller {
            public string[] path;
            public string[] parameters;
        }

        internal static bool HasPrivateMenuPath(ToggleBuilder builder) {
            var toggle = builder.model;
            return toggle.addMenuItem && (!toggle.useInt || toggle.slider)
                && toggle.usePrefixOnParam && toggle.paramOverride == null && !toggle.useGlobalParam
                && !string.IsNullOrWhiteSpace(toggle.name);
        }

        // Keep menu paths distinct from actual animator parameter names in the saved list.
        private static string MenuPathKey(ToggleBuilder builder) => "\u001fToggleMenu:" + builder.model.name;

        private static Dictionary<VFGameObject, Controller> Collect(
            VFGameObject root,
            IEnumerable<FullControllerBuilder> builders,
            IEnumerable<ToggleBuilder> toggles
        ) {
            var controllerParameters = builders.SelectMany(b => b.GetPrivateSyncedParameters()
                .Select(parameter => (owner: b.featureBaseObject, parameter)));
            var toggleParameters = (toggles ?? Array.Empty<ToggleBuilder>()).Where(HasPrivateMenuPath)
                .Select(b => (owner: b.featureBaseObject, parameter: MenuPathKey(b)));
            var controllers = new Dictionary<VFGameObject, Controller>();
            foreach (var group in controllerParameters.Concat(toggleParameters).GroupBy(entry => entry.owner)) {
                var parameters = group.Select(entry => entry.parameter).Distinct().ToArray();
                var path = group.Key.GetSelfAndAllParents().TakeWhile(o => o != root).Reverse();
                controllers.Add(group.Key, new Controller {
                    path = path.Select(o => o.name).ToArray(),
                    parameters = parameters
                });
            }
            return controllers;
        }

        public static Controller[] Capture(
            VFGameObject root,
            IEnumerable<FullControllerBuilder> builders,
            IEnumerable<ToggleBuilder> toggles = null
        ) {
            return Collect(root, builders, toggles).Values.ToArray();
        }

        public static void Align(
            VFGameObject root,
            IEnumerable<FullControllerBuilder> builders,
            Controller[] desktop,
            IEnumerable<ToggleBuilder> toggles = null
        ) {
            var desktopParameters = desktop.SelectMany(d => d.parameters).ToHashSet();
            var mobile = Collect(root, builders, toggles?.Where(b => desktopParameters.Contains(MenuPathKey(b))));
            var unmatched = desktop.Where(d => !mobile.Values.Any(m => m.path.SequenceEqual(d.path))).ToArray();
            var proposals = new Dictionary<VFGameObject, (VFGameObject obj, string name)>();
            foreach (var (owner, controller) in mobile) {
                if (desktop.Any(d => d.path.SequenceEqual(controller.path))) continue;
                var matches = unmatched.Where(d =>
                    d.path.Length == controller.path.Length
                    && d.path.Where((name, i) => name != controller.path[i]).Count() == 1
                    && !controller.parameters.Except(d.parameters).Any()
                ).Take(2).ToArray();
                if (matches.Length != 1) continue;

                var target = matches[0].path;
                var obj = owner;
                for (var i = target.Length - 1; i >= 0; i--, obj = obj.parent) {
                    if (target[i] == obj.name) continue;
                    proposals.Add(owner, (obj, target[i]));
                    break;
                }
            }

            // A rename can affect several controllers. They must agree, and two siblings
            // cannot claim the same name. Check every proposal before changing any names.
            var renames = proposals.Values.Distinct().Where(rename =>
                !rename.obj.parent.Children().Any(sibling => sibling.name == rename.name)
                && !proposals.Values.Any(other => other.obj != rename.obj
                    && other.obj.parent == rename.obj.parent && other.name == rename.name)
                && mobile.Where(pair => pair.Key.IsSameOrChildOf(rename.obj))
                    .All(pair => proposals.TryGetValue(pair.Key, out var other) && other == rename)
            ).ToArray();
            foreach (var (obj, name) in renames) obj.name = name;
        }
    }
}
