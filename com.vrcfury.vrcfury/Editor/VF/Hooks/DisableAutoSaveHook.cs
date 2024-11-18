using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Utils;

namespace VF.Hooks {
    /**
     * Unity's Prefab "Auto-Save" option adds an extreme amount of lag when changing components in a large prefab.
     * Automatically disable it for any prefabs containing vrcfury components.
     */
    internal static class DisableAutoSaveHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            Scheduler.Schedule(() => {
                if (UnityCompatUtils.IsEditingPrefab()) {
                    var hasVrcf = VFGameObject.GetRoots()
                        .SelectMany(g => g.GetComponentsInSelfAndChildren<VRCFuryComponent>())
                        .Any();
                    if (hasVrcf) {
                        if (UnityCompatUtils.DisablePrefabAutosave()) {
                            Debug.Log("VRCFury disabled AutoSave as it causes a crazy amount of lag while editing components on large prefabs");
                        }
                    }
                }
            }, 1000);
        }
    }
}
