using System;
using UnityEngine;

namespace VF.Model {
    [Serializable]
    internal class ObjectReference<T> : ObjectReference where T : UnityEngine.Component {
        [NonSerialized] public T componentOverride;

        public static implicit operator ObjectReference<T>(T component) => new ObjectReference<T> {
            useBone = false, useObj = true, componentOverride = component
        };
    }

    [Serializable]
    internal class ObjectReference {
        public enum Mode { Bone, ObjectReference, ObjectPath }

        public bool useBone = true;
        public HumanBodyBones bone = HumanBodyBones.Hips;
        public bool useObj;
        public GameObject obj;
        public string offset = "";

        public Mode mode => useBone ? Mode.Bone : useObj ? Mode.ObjectReference : Mode.ObjectPath;

        public static implicit operator ObjectReference(GameObject obj) =>
            new ObjectReference { useBone = false, useObj = true, obj = obj };

    }
}
