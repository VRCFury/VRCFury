using System;
using System.Linq;
using com.vrcfury.udon;
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
                SpsDetachedBakeAndSave.Run(
                    sockets,
                    plugs,
                    (socket, result) => {
                        var udonSupport = socket.GetComponent<SpsSocketUdonSupport>();
                        if (udonSupport != null) SpsUdonSupportBuilder.Apply(udonSupport, result);
                    },
                    (plug, result) => {
                        var udonSupport = plug.GetComponent<SpsPlugUdonSupport>();
                        if (udonSupport != null) SpsUdonSupportBuilder.Apply(udonSupport, result);
                    }
                );
            }
        }
    }
}
