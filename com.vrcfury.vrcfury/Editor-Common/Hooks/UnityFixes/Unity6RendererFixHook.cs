using System.Collections.Generic;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * On Unity 6.4, importing a newly saved skinned mesh can leave its live renderer
     * with stale mesh data even though sharedMesh still points to the correct asset.
     * Reassign the mesh after import to refresh the renderer.
     */
    internal static class Unity6RendererFixHook {
        private static readonly HashSet<SkinnedMeshRenderer> pending = new();

        public static void Register(SkinnedMeshRenderer renderer) {
            pending.Add(renderer);
        }

        public static void Process() {
            if (pending.Count == 0) return;
            foreach (var renderer in pending) {
                if (renderer == null) continue;
                var mesh = renderer.sharedMesh;
                if (mesh.IsSaved() && VrcfObjectFactory.DidCreate(mesh)) renderer.sharedMesh = mesh;
            }
            pending.Clear();
        }
    }
}
