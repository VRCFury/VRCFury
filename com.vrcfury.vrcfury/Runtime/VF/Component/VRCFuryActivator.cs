using System;
using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    public abstract class VRCFuryActivator : VRCFuryComponent {
#if UNITY_EDITOR
        private void Awake()
        {
            UnityEditor.EditorApplication.delayCall += () => DestroyIfNeeded();
        }

        private void Start()
        {
            if (!this) return;
            if (DestroyIfNeeded()) return;
        }

        private bool DestroyIfNeeded()
        {
            var shouldDestroy = !UnityEditor.EditorApplication.isPlaying ||
                                UnityEditor.EditorUtility.IsPersistent(this);
            if (!shouldDestroy) return false;

            Debug.Log("Destroying ApplyOnPlayActivator in Start");
            DestroyImmediate(this);
            return true;
        }
#endif
    }
}
