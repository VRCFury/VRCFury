using System;
using UnityEngine;

namespace VF.Component {
    /**
     * This is a component that stores everything as JSON so we don't have to deal with unity's crappy serialization.
     * See JsonComponentExtensions for more details.
     */
    public abstract class JsonComponent : MonoBehaviour {
        public string json;

        [NonSerialized] public static Action<JsonComponent> onValidate;
        public void OnValidate() { onValidate?.Invoke(this); }
    }
}
