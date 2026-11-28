using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Model;
using VF.Utils;

namespace VF.Menu {
    internal static class ListComponentsMenuItem {
        [VFInit]
        private static void InitHierarchyMenu() {
            MenuItems.AddHierarchyCommand("List All Components", _ => true, obj => ListChildComponents(obj));
        }

        private static void ListChildComponents(VFGameObject obj) {
            VRCFuryBuildContext.Run(() => {
                if (obj == null) return;
                var list = new List<string>();
                foreach (var c in obj.GetComponentsInSelfAndChildren()) {
                    if (c == null || c is Transform) continue;
                    var type = c.GetType().Name;
                    if (c is VRCFury vf) {
                        type += " (" + vf.GetAllFeatures().Select(f => f.GetType().Name).Join(',') + ")";
                    }
                    list.Add(type  + " in " + c.owner().GetPath(obj));
                }

                var output = $"List of components on {obj.GetDebugPath()}:\n" + list.Join('\n');
                GUIUtility.systemCopyBuffer = output;

                DialogUtils.DisplayDialog(
                    "Debug",
                    $"Found {list.Count} components in {obj.name} and copied them to clipboard",
                    "Ok"
                );
            });
        }
    }
}
