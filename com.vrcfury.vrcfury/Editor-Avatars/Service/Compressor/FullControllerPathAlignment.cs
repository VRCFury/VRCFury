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

        private static Dictionary<VFGameObject, Controller> Collect(VFGameObject root, IEnumerable<FullControllerBuilder> builders) {
            var controllers = new Dictionary<VFGameObject, Controller>();
            foreach (var group in builders.GroupBy(b => b.featureBaseObject)) {
                var parameters = group.SelectMany(b => b.GetPrivateSyncedParameters()).Distinct().ToArray();
                if (parameters.Length == 0) continue;
                var path = group.Key.GetSelfAndAllParents().TakeWhile(o => o != root).Reverse();
                controllers.Add(group.Key, new Controller {
                    path = path.Select(o => o.name).ToArray(),
                    parameters = parameters
                });
            }
            return controllers;
        }

        public static Controller[] Capture(VFGameObject root, IEnumerable<FullControllerBuilder> builders) {
            return Collect(root, builders).Values.ToArray();
        }

        public static void Align(VFGameObject root, IEnumerable<FullControllerBuilder> builders, Controller[] desktop) {
            var mobile = Collect(root, builders);
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
