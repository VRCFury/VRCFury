using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Utils {
    internal static class MonoBehaviourExtensions {
        public static IEnumerable<string> GetVrcParameterNames(this MonoBehaviour component) {
            return component switch {
                VRCContactReceiver receiver => receiver.GetParameterNames(),
                VRCPhysBone physbone => physbone.GetParameterNames(),
#if VRCSDK_HAS_VRCRAYCAST
                VRCRaycast raycast => raycast.GetParameterNames(),
#endif
                _ => Array.Empty<string>()
            };
        }
    }
}
