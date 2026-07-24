using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Exceptions;
using VF.Model;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Menu {
    internal static class LogExternalSceneReferencesMenuItem {
        [MenuItem(MenuItems.logExternalSceneReferences, priority = MenuItems.logExternalSceneReferencesPriority)]
        private static void Run() {
            VRCFuryBuildContext.Run(() => {
                var roots = Selection.gameObjects.AsVf().ToList();
                roots = roots.Where(go => !roots.Any(other => other != go && go.IsSameOrChildOf(other))).ToList();
                if (roots.Count == 0) return;

                var inside = roots.SelectMany(root => root.GetSelfAndAllChildren()).ToImmutableHashSet();
                var findings = new List<string>();

                foreach (var obj in inside) {
                    foreach (var component in obj.GetComponents<UnityEngine.Component>()) {
                        if (component == null) continue;
                        if (component is Transform) continue;
                        MutableManager.ForEachChildObjectReference(component, (path, target, set) => {
                            var targetGo = GetSceneTarget(target);
                            if (targetGo == null || inside.Contains(targetGo)) return;
                            findings.Add(
                                $"[VRCFury] External scene reference: {component.GetType().Name} on {obj.GetPath()} :: {path} -> {targetGo.GetPath()}"
                            );
                        });
                    }
                }

                if (findings.Count == 0) {
                    DialogUtils.DisplayDialog(
                        "Log External Scene References",
                        "No external scene references found.",
                        "Ok"
                    );
                    return;
                }

                foreach (var finding in findings) Debug.LogWarning(finding);
                DialogUtils.DisplayDialog(
                    "Log External Scene References",
                    $"Found {findings.Count} external scene reference{(findings.Count == 1 ? "" : "s")}. Logged to console.",
                    "Ok"
                );
            });
        }

        [MenuItem(MenuItems.logExternalSceneReferences, true)]
        private static bool Validate() {
            return Selection.gameObjects.Any();
        }

        private static VFGameObject GetSceneTarget(Object target) {
            switch(target) {
                case GameObject go:
                    return go;
                case UnityEngine.Component c:
                    return c.owner();
            };
            return null;
        }
    }
}
