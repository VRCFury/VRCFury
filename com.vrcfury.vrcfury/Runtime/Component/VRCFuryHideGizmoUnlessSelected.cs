using System;
using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    internal class VRCFuryHideGizmoUnlessSelected : VRCFuryPlayComponent {
        public static Func<VRCFuryHideGizmoUnlessSelected, bool> isSelected;

        private void Update() {
            if (isSelected?.Invoke(this) ?? false)
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
