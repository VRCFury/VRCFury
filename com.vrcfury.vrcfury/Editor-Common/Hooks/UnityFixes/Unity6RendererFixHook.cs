using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Unity 6 (maybe only some versions?) has a bug where renderers created
     * using materials which are not yet saved never actually render those materials.
     * Seemingly the only way to fix this is to turn the renderer off and back on again
     * once the contents is saved.
     *
     * Not 100% sure if it's caused by unsaved mats, it may be caused by unsaved meshes
     * or something else.
     */
    internal static class Unity6RendererFixHook {
        private static readonly List<VFGameObject> pending = new List<VFGameObject>();

        public static void Register(VFGameObject obj) {
            pending.Add(obj);
        }

        public static void Process() {
            if (pending.Count == 0) return;
            foreach (var obj in pending) {
                if (obj.activeInHierarchy) {
                    //Debug.Log("Toggling " + obj.GetDebugPath());
                    obj.active = false;
                    obj.active = true;
                }
            }
            pending.Clear();
        }
    }
}
