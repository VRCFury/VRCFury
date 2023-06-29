using System;
using UnityEngine;

namespace VF.Component {
    /**
     * This is a component that stores everything as JSON so we don't have to deal with unity's crappy serialization.
     * See JsonComponentExtensions for more details.
     */
    [ExecuteInEditMode]
    public abstract class JsonComponent : MonoBehaviour {
        public string json;

        [NonSerialized] public static Action<JsonComponent> onValidate;
        public void OnValidate() { onValidate?.Invoke(this); }
        [NonSerialized] public static Action<JsonComponent> onDestroy;
        public void OnDestroy() { onDestroy?.Invoke(this); }
    }
}
