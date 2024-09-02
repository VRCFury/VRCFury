using UnityEditor;
using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    internal class VRCFuryHideGizmoUnlessSelected : VRCFuryPlayComponent {
        private void Update() {
            if (Selection.activeGameObject == gameObject)
                Show();
            else
                Hide();
        }

        private void OnDisable() {
            Show();
        }

        private void Show() {
            foreach (var c in gameObject.GetComponents<UnityEngine.Component>()) {
                if (c is Transform || c == this) continue;
                c.hideFlags &= ~HideFlags.HideInHierarchy;
            }
        }
        private void Hide() {
            foreach (var c in gameObject.GetComponents<UnityEngine.Component>()) {
                if (c is Transform || c == this) continue;
                c.hideFlags |= HideFlags.HideInHierarchy;
            }
        }
    }
}
