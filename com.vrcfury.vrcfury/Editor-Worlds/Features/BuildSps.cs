using System;
using System.Linq;
using UnityEngine.SceneManagement;
using VF.Builder.Haptics;
using VF.Component;
using VF.Exceptions;
using VF.Inspector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Features {
    internal static class BuildSps {
        public static void Process(Scene scene) {
            using (new VRCFuryBuildContext()) {
                var sockets = scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()).ToArray();
                var plugs = scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()).ToArray();
                SpsBakeAndSave.Run(sockets, plugs);
            }
        }
    }
}
