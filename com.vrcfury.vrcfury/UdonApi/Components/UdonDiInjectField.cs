using UnityEngine;
using VF.Component;

namespace com.vrcfury.udon.Components {
    [AddComponentMenu("VRCFury/UdonDI - Inject Field (VRCFury)")]
    internal class UdonDiInjectField : VRCFuryComponent {
        public string targetField;
        public string registeredName;
    }
}
