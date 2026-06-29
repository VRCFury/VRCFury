using System;
using UnityEngine;
using VF.VrcfEditorOnly;

namespace VF.Component {
    [AddComponentMenu("")]
    [ExecuteAlways]
    internal class VRCFuryHideGizmoUnlessSelected : MonoBehaviour, IVrcfEditorOnly {
        public void OnSelectionChanged(bool isSelected) {
            if (isSelected)
                Show();
            else
                Hide();
        }

        private void OnDisable() {
            Show();
        }

        private void Show() {
            if (gameObject == null) return;
            foreach (var c in gameObject.GetComponents<UnityEngine.Component>()) {
                if (c is Transform || c == this) continue;
                c.hideFlags &= ~HideFlags.HideInHierarchy;
            }
        }
        private void Hide() {
            if (gameObject == null) return;
            foreach (var c in gameObject.GetComponents<UnityEngine.Component>()) {
                if (c is Transform || c == this) continue;
                c.hideFlags |= HideFlags.HideInHierarchy;
            }
        }
    }
}
