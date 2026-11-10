using System;
using UnityEngine;
using VF.VrcfEditorOnly;

namespace VF.Component {
    [AddComponentMenu("")]
    [ExecuteAlways]
    internal class VRCFuryHideGizmoUnlessSelected : MonoBehaviour, IVrcfEditorOnly {
        public static Func<GameObject, bool> IsSelected;

        private void OnEnable() {
            OnSelectionChanged();
        }

        public void OnSelectionChanged() {
            if (!isActiveAndEnabled || IsSelected?.Invoke(gameObject) == true)
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
