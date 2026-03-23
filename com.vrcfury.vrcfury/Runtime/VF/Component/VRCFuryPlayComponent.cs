using System;
using UnityEngine;
using VF.VrcfEditorOnly;

namespace VF.Component {
    internal abstract class VRCFuryPlayComponent : MonoBehaviour, IVrcfEditorOnly {
        public static Action<VRCFuryPlayComponent> onValidate;

        private void OnValidate() {
            onValidate?.Invoke(this);
        }
    }
}
