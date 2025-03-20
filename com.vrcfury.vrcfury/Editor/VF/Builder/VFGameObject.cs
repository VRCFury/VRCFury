using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Utils;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace VF.Builder {
    /**
     * VFGameObject is a wrapper around Transform and GameObject, combining the two into one object we can use
     * everywhere, and providing helper methods that are commonly used.
     */
    internal class VFGameObject {
        private readonly GameObject _gameObject;
        private VFGameObject(GameObject gameObject) {
            _gameObject = gameObject;
        }

        private GameObject gameObject => _gameObject;
        private Transform transform => _gameObject == null ? null : _gameObject.transform;
        public static implicit operator VFGameObject(GameObject d) => d == null ? null : new VFGameObject(d);
        public static implicit operator VFGameObject(Transform d) => d == null ? null : new VFGameObject(d.gameObject);
        public static implicit operator GameObject(VFGameObject d) => d?.gameObject;
        public static implicit operator Object(VFGameObject d) => d?.gameObject;
        public static implicit operator Transform(VFGameObject d) => d?.transform;
        public static implicit operator bool(VFGameObject d) => d?.gameObject != null;
        public static bool operator ==(VFGameObject a, VFGameObject b) => a?.Equals(b) ?? b?.Equals(null) ?? true;
        public static bool operator !=(VFGameObject a, VFGameObject b) => !(a == b);
        public override bool Equals(object other) {
            return (other is VFGameObject a && _gameObject == a._gameObject)
                   || (other is Transform b && transform == b)
                   || (other is GameObject c && gameObject == c)
                   || (other == null && gameObject == null);
        }
        public override int GetHashCode() {
            return Tuple.Create(gameObject).GetHashCode();
        }

        public override string ToString() {
            return GetPath();
        }

        public Matrix4x4 worldToLocalMatrix => transform.worldToLocalMatrix;
        public Matrix4x4 localToWorldMatrix => transform.localToWorldMatrix;

        public Vector3 localPosition {
            get => transform.localPosition;
            set => transform.localPosition = value;
        }
        public Quaternion localRotation {
            get => transform.localRotation;
            set => transform.localRotation = value;
        }
        public Vector3 localScale {
            get => transform.localScale;
            set => transform.localScale = value;
        }
        
        public Vector3 worldPosition {
            get => transform.position;
            set => transform.position = value;
        }
        public Quaternion worldRotation {
            get => transform.rotation;
            set => transform.rotation = value;
        }
        public Vector3 worldScale {
            get => transform.lossyScale;
            set {
                var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
                var newLocalScale = new Vector3(
                    value.x / parentScale.x,
                    value.y / parentScale.y,
                    value.z / parentScale.z
                );
                transform.localScale = newLocalScale;
            }
        }

        public Scene scene => gameObject.scene;

        public string name {
            get => gameObject.name;
            set => gameObject.name = value;
        }
        
        public bool active {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value);
        }

        public void SetParent(VFGameObject newParent, bool worldPositionStays) {
            transform.SetParent(newParent, worldPositionStays);
        }

        public bool activeInHierarchy => gameObject.activeInHierarchy;

        public VFGameObject parent => transform.parent;

        public VFGameObject root => transform.root;

        public VFGameObject[] GetSelfAndAllChildren() {
            return GetComponentsInSelfAndChildren<Transform>()
                .Select(Cast)
                .ToArray();
        }
        
        public VFGameObject[] GetSelfAndAllParents() {
            return GetComponentsInSelfAndParents<Transform>()
                .Select(Cast)
                .ToArray();
        }
        
        public VFGameObject[] Children() {
            return Enumerable.Range(0, transform.childCount)
                .Select(i => transform.GetChild(i))
                .Select(Cast)
                .ToArray();
        }
        
        public VFGameObject[] Parents() {
            return GetSelfAndAllParents()
                .Where(t => t != this)
                .ToArray();
        }

        public int childCount => transform.childCount;

        public UnityEngine.Component GetComponent(Type t) {
            return GetComponents(t).FirstOrDefault();
        }
        public T GetComponent<T>() {
            return GetComponents<T>().FirstOrDefault();
        }

        public VFConstraint[] GetConstraints(bool includeParents = false, bool includeChildren = false) {
            var avatar = VRCAvatarUtils.GuessAvatarObject(this);
            if (avatar == null) avatar = root;
            return avatar.GetComponentsInSelfAndChildren<UnityEngine.Component>()
                .Select(c => c.AsConstraint())
                .NotNull()
                .Where(c => {
                    var affectedObject = c.GetAffectedObject();
                    if (affectedObject == null) return false;
                    if (includeParents) return IsChildOf(affectedObject);
                    if (includeChildren) return affectedObject.IsChildOf(this);
                    return affectedObject == this;
                })
                .ToArray();
        }

        public UnityEngine.Component[] GetComponents(Type t) {
            // Components can sometimes be null for some reason. Perhaps when they're corrupt?
            // The OfType is required because unity can be tricked into blindly returning things that are NOT components
            // if someone messed with the metadata or class type.
            return gameObject.GetComponents(t).NotNull().OfType<UnityEngine.Component>().ToArray();
        }
        public T[] GetComponents<T>() {
            return gameObject.GetComponents<T>().NotNull().OfType<T>().ToArray();
        }
        
        public T GetComponentInSelfOrParent<T>() {
            return GetComponentsInSelfAndParents<T>().FirstOrDefault();
        }
        
        public UnityEngine.Component[] GetComponentsInSelfAndChildren(Type type) {
            return gameObject.GetComponentsInChildren(type, true).NotNull().OfType<UnityEngine.Component>().ToArray();
        }

        public T[] GetComponentsInSelfAndChildren<T>() {
            return gameObject.GetComponentsInChildren<T>(true).NotNull().OfType<T>().ToArray();
        }
        
        public T[] GetComponentsInSelfAndParents<T>() {
            return gameObject.GetComponentsInParent<T>(true).NotNull().OfType<T>().ToArray();
        }
        
        public UnityEngine.Component AddComponent(Type type) {
            return gameObject.AddComponent(type);
        }

        public T AddComponent<T>() where T : UnityEngine.Component {
            return gameObject.AddComponent<T>();
        }
        
        public VFGameObject Find(string search) {
            return transform.Find(search);
        }

        public bool IsChildOf(VFGameObject other) {
            return transform.IsChildOf(other);
        }

        public string GetPath(VFGameObject root = null, bool prettyRoot = false) {
            if (root == null) {
                root = transform.root;
                if (this == root) {
                    return root.name;
                }
                return root.name + "/" + AnimationUtility.CalculateTransformPath(this, root);
            }
            if (!IsChildOf(root)) {
                throw new Exception($"{GetPath()} is not a child of {root.GetPath()}");
            }
            if (this == root && prettyRoot) {
                return "Avatar Root";
            }
            return AnimationUtility.CalculateTransformPath(this, root);
        }

        public string GetAnimatedPath() {
            var avatarObject = VRCAvatarUtils.GuessAvatarObject(this);
            if (avatarObject == null) return "_avatarMissing/" + GetPath();
            return GetPath(avatarObject);
        }

        public void Destroy() {
            var b = VRCAvatarUtils.GuessAvatarObject(this) ?? root;
            foreach (var c in b.GetComponentsInSelfAndChildren<VRCPhysBoneBase>()) {
                if (c.GetRootTransform().IsChildOf(this))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in b.GetComponentsInSelfAndChildren<VRCPhysBoneColliderBase>()) {
                if (c.GetRootTransform().IsChildOf(this))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in b.GetComponentsInSelfAndChildren<ContactBase>()) {
                if (c.GetRootTransform().IsChildOf(this))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in GetConstraints(includeChildren: true)) {
                c.Destroy();
            }
            Object.DestroyImmediate(gameObject);
        }

        public VFGameObject Clone() {
            return Object.Instantiate(gameObject);
        }

        public static VFGameObject[] GetRoots(Scene scene) {
            return scene
                .GetRootGameObjects()
                .Select(Cast)
                .ToArray();
        }

        public static VFGameObject[] GetRoots() {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.isLoaded)
                .SelectMany(GetRoots)
                .ToArray();
        }

        public static VFGameObject Cast(GameObject o) {
            return o;
        }
        public static VFGameObject Cast(Transform o) {
            return o;
        }

        public Vector3 TransformPoint(Vector3 position) => transform.TransformPoint(position);
        public Vector3 InverseTransformPoint(Vector3 position) => transform.InverseTransformPoint(position);
        public Vector3 TransformDirection(Vector3 direction) => transform.TransformDirection(direction);
        
        /**
         * If two objects share the same name, animations will always target the first one.
         * If an object contains a slash in its name, it does weird things, since technically
         *   it can be animated, but it will show as "missing" in the animation editor, and VRCFury
         *   will think it's invalid because it can't find the object it's pointing to.
         * Technically we should not remove slashes (since technically they can be animated by the user's animator),
         *   but vrcfury has already been removing these animations as "invalid" for ages (because it can't find the object),
         *   so we may as well just keep it at this point because retaining the existing broken animations would be very difficult.
         */
        public void EnsureAnimationSafeName() {
            var name = this.name;
            name = name.Replace("/", "_");
            if (string.IsNullOrEmpty(name)) name = "_";

            if (parent != null) {
                for (var i = 0; ; i++) {
                    var finalName = name + (i == 0 ? "" : $" ({i})");
                    var exists = parent.Find(finalName);
                    if (exists != null && exists != this) continue; // Already used by something else
                    name = finalName;
                    break;
                }
            }

            this.name = name;
        }

        public int GetInstanceID() {
            return _gameObject.GetInstanceID();
        }

        public bool HasTag(string tag) {
            return gameObject.CompareTag(tag);
        }
    }
}
