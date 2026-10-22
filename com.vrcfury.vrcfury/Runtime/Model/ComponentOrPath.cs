using System;
using UnityEngine;

namespace VF.Model {
    [Serializable]
    internal abstract class ComponentOrPath {
        public bool usePath;
        public string path = "";
        public abstract Type ComponentType { get; }
        public abstract UnityEngine.Component Resolve(GameObject avatarRoot);
    }

    [Serializable]
    internal class ComponentOrPath<T> : ComponentOrPath where T : UnityEngine.Component {
        public T component;
        public override Type ComponentType => typeof(T);

        public static implicit operator ComponentOrPath<T>(T component) =>
            new ComponentOrPath<T> { component = component };

        public override UnityEngine.Component Resolve(GameObject avatarRoot) {
            if (!usePath) return component;
            var target = avatarRoot == null ? null : string.IsNullOrEmpty(path)
                ? avatarRoot.transform : avatarRoot.transform.Find(path);
            return target == null ? null : target.GetComponent<T>();
        }
    }
}
