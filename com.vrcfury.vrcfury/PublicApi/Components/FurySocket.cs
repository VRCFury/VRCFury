using com.vrcfury.api.Actions;
using JetBrains.Annotations;
using UnityEngine;
using VF.Component;
using VF.Model;

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

        public FuryActionSet AddDepthActions(float startDistance, float endDistance, float smoothingSeconds, bool enableSelf = false) {
            s.enableDepthAnimations = true;
            var a = new VRCFuryHapticSocket.DepthAction();
            a.state = new State();
            a.startDistance = startDistance;
            a.endDistance = endDistance;
            a.smoothingSeconds = smoothingSeconds;
            a.enableSelf = enableSelf;
            s.depthActions.Add(a);
            return new FuryActionSet(a.state);
        }

        public FuryActionSet GetActiveActions() {
            s.enableActiveAnimation = true;
            if (s.activeActions == null) s.activeActions = new State();
            return new FuryActionSet(s.activeActions);
        }

        public enum Mode {
            None = VRCFuryHapticSocket.AddLight.None,
            Hole = VRCFuryHapticSocket.AddLight.Hole,
            Ring = VRCFuryHapticSocket.AddLight.Ring,
            Auto = VRCFuryHapticSocket.AddLight.Auto,
            RingOneWay = VRCFuryHapticSocket.AddLight.RingOneWay    
        }
    }
}