using com.vrcfury.api.Actions;
using JetBrains.Annotations;
using UnityEngine;
using VF.Component;
using VF.Model;
using VF.Utils;

namespace com.vrcfury.api.Components {
    /** Create an instance using <see cref="FuryComponents"/> */
    [PublicAPI]
    public class FurySocket {
        private readonly VRCFuryHapticSocket s;

        internal FurySocket(GameObject obj) {
            s = obj.AddComponent<VRCFuryHapticSocket>();
        }

        public void SetName(string name) {
            s.name = name;
        }

        public void SetMode(Mode mode) {
            s.addLight = (VRCFuryHapticSocket.AddLight) mode;
        }

        public void SetAutoOff() {
            s.enableAuto = false;
        }

        public FuryActionSet AddDepthActions(Vector2 range, float smoothingSeconds, bool enableSelf = false) {
            var a = new VRCFuryHapticSocket.DepthActionNew {
                range = range.Ordered(),
                smoothingSeconds = smoothingSeconds,
                enableSelf = enableSelf
            };
            s.depthActions2.Add(a);
            return new FuryActionSet(a.actionSet);
        }

        public FuryActionSet GetActiveActions() {
            return new FuryActionSet(s.activeActions);
        }

        [PublicAPI]
        public enum Mode {
            None = VRCFuryHapticSocket.AddLight.None,
            Hole = VRCFuryHapticSocket.AddLight.Hole,
            Ring = VRCFuryHapticSocket.AddLight.Ring,
            Auto = VRCFuryHapticSocket.AddLight.Auto,
            RingOneWay = VRCFuryHapticSocket.AddLight.RingOneWay    
        }
    }
}
