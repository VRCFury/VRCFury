using System;
using System.Collections.Generic;
using System.Linq;
using VF.Utils;

namespace VF.Builder.Haptics {
    // Shared inputs and hierarchy checks for one upgrade run.
    internal class SpsUpgradeContext {
        public readonly VFGameObject root;
        public readonly bool dryRun;
        private readonly Func<VFGameObject, bool> shouldRemoveObj;

        public SpsUpgradeContext(VFGameObject root, bool dryRun, Func<VFGameObject, bool> shouldRemoveObj) {
            this.root = root;
            this.dryRun = dryRun;
            this.shouldRemoveObj = shouldRemoveObj;
        }

        public bool WillSurvive(VFGameObject obj) => !obj.GetSelfAndAllParents().Any(shouldRemoveObj);

        public bool AlreadyExistsAboveOrBelow(VFGameObject obj, IEnumerable<VFGameObject> existing) {
            if (!WillSurvive(obj)) return true;
            var occupied = new HashSet<VFGameObject>(existing);
            return obj.GetSelfAndAllChildren().Concat(obj.GetSelfAndAllParents()).Any(occupied.Contains);
        }

        public string GetPath(VFGameObject obj) => obj.GetPath(root);

        public static string GetNameFromBakeInfo(VFGameObject info) {
            foreach (var child in info.Children()) {
                if (child.name.StartsWith("name=")) return child.name.Substring(5);
            }
            return "";
        }
    }
}
