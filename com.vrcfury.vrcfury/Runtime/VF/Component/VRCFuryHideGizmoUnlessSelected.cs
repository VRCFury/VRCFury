using UnityEditor;
using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    internal class VRCFuryHideGizmoUnlessSelected : VRCFuryPlayComponent {
        private void Update() {
#if UNITY_EDITOR
            if (Selection.activeGameObject == gameObject)
                Show();
            else
                Hide();
#endif
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
