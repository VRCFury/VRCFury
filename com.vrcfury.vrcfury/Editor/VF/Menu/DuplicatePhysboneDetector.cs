using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Inspector;
using VF.Utils;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Menu {
    internal static class DuplicatePhysboneDetector {
        [MenuItem(MenuItems.detectDuplicatePhysbones, priority = MenuItems.detectDuplicatePhysbonesPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(RunUnsafe);
        }
        
        private static void RunUnsafe() {
            var start = DialogUtils.DisplayDialog(
                "Duplicate Physbones",
                "This tool will load every scene and prefab, and check for any bones that have more than one physbone targeting them (which is usually bad). This may take a lot of ram and time. Continue?",
                "Yes",
                "Cancel"
            );
            if (!start) return;

            BulkUpgradeUtils.WithAllScenesOpen(() => {
                var bad = new List<string>();
                FindDupes<VRCPhysBone>(c => c.GetRootTransform(), bad);

                if (bad.Count == 0) {
                    DialogUtils.DisplayDialog(
                        "Duplicate Physbones",
                        "No duplicates found in loaded objects.",
                        "Ok"
                    );
                    return;
                }

                var split = bad.Join("\n\n").Split('\n').ToList();
                while (split.Count > 0) {
                    var numToPick = Math.Min(split.Count, 40);
                    var part = split.GetRange(0, numToPick);
                    split.RemoveRange(0, numToPick);
                    
                    var message = "Duplicate physbones found in loaded objects:\n\n" + part.Join('\n');
                    if (split.Count > 0) message += "\n... and more (will be shown in next dialog)";
                    
                    message += "\n\nDelete the duplicates?";
                    var ok = DialogUtils.DisplayDialog(
                        "Duplicate Physbones",
                        message,
                        "Ok, Delete Duplicates",
                        "Cancel"
                    );
                    if (!ok) return;
                }

                FixDupes<VRCPhysBone>(c => c.GetRootTransform());
            });
        }

        private static string GetName(VFGameObject t) {
            return t.GetPath()
                   + " (" + AssetDatabase.GetAssetPath(t) + ")";
        }
        private static string GetName<T>(T c, Dictionary<T, string> sources) where T : UnityEngine.Component {
            return c.owner().GetPath()
                   + " (" + sources[c] + ")"
                   + (IsMutable(c) ? "" : " (Immutable)");
        }

        private static void FindDupes<T>(Func<T, VFGameObject> GetTarget, List<string> badList) where T : UnityEngine.Component {
            var (map, sources) = BulkUpgradeUtils.FindAll(GetTarget);
            foreach (var ((transform,target),components) in map.Select(x => (x.Key, x.Value))) {
                if (components.Count == 1) continue;
                var mutable = components.Where(IsMutable).ToList();
                if (mutable.Count == 0) continue;
                var targetStr = GetName(target);
                var cStrs = components.Select(c => GetName(c, sources)).Join('\n');
                badList.Add( $"{targetStr}\nis targeted by\n{cStrs}");
            }
        }

        private static bool IsMutable(UnityEngine.Component c) {
            return !PrefabUtility.IsPartOfImmutablePrefab(c) && !PrefabUtility.IsPartOfPrefabInstance(c);
        }
        
        private static void FixDupes<T>(Func<T, VFGameObject> GetTarget) where T : UnityEngine.Component {
            var (map, sources) = BulkUpgradeUtils.FindAll(GetTarget);
            foreach (var ((transform,target),components) in map.Select(x => (x.Key, x.Value))) {
                if (components.Count == 1) continue;
                var mutable = components.Where(IsMutable).ToList();
                if (mutable.Count == components.Count) {
                    mutable.RemoveAt(0);
                }

                foreach (var c in mutable) {
                    var obj = c.owner();
                    Object.DestroyImmediate(c, true);
                    VRCFuryEditorUtils.MarkDirty(obj);
                }
            }
        }
    }
}
