using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Exceptions;
using VF.Hooks.UnityFixes;
using VF.Utils;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace VF.Utils {
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
        public static bool operator ==(VFGameObject a, VFGameObject b) => a?._gameObject == b?._gameObject;
        public static bool operator !=(VFGameObject a, VFGameObject b) => !(a == b);
        public override bool Equals(object other) {
            return (other is VFGameObject a && _gameObject == a._gameObject)
                   || (other is Transform b && transform == b)
                   || (other is GameObject c && gameObject == c)
                   || (other == null && gameObject == null);
        }
        public override int GetHashCode() {
            return _gameObject?.GetHashCode() ?? 0;
        }

        public override string ToString() {
            return GetPath();
        }

        public void Dirty() {
            gameObject.Dirty();
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
        public UnityEngine.Component[] GetComponents() {
            return GetComponents<UnityEngine.Component>();
        }
        public UnityEngine.Component[] GetComponents(Type t) {
            return FilterComponents(gameObject.GetComponents(t));
        }
        public T[] GetComponents<T>() {
            return FilterComponents(gameObject.GetComponents<T>());
        }

        public UnityEngine.Component[] GetComponentsInSelfAndChildren() {
            return GetComponentsInSelfAndChildren<UnityEngine.Component>();
        }
        public UnityEngine.Component[] GetComponentsInSelfAndChildren(Type type) {
            return FilterComponents(gameObject.GetComponentsInChildren(type, true));
        }
        public T[] GetComponentsInSelfAndChildren<T>() {
            return FilterComponents(gameObject.GetComponentsInChildren<T>(true));
        }

        public T GetComponentInSelfOrParent<T>() {
            return GetComponentsInSelfAndParents<T>().FirstOrDefault();
        }
        public T[] GetComponentsInSelfAndParents<T>() {
            return FilterComponents(gameObject.GetComponentsInParent<T>(true));
        }

        /**
         * Components can sometimes be null for some reason. Perhaps when they're corrupt?
         * The OfType is required because unity can be tricked into blindly returning things that are NOT components
         * if someone messed with the metadata or class type.
         */
        private static T[] FilterComponents<T>(T[] components) {
            foreach (object component in components) {
                if (component is T && component is Object unityObject && unityObject != null) continue;
                return components.NotNull().OfType<T>().ToArray();
            }
            return components;
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

        public bool IsSameOrChildOf(VFGameObject other) {
            return transform.IsChildOf(other);
        }

        public string GetPath(VFGameObject root = null, bool prettyRoot = false, bool removeCloneFromRoot = false) {
            if (root == null) {
                root = transform.root;
                var rootName = root.name;
                if (removeCloneFromRoot && rootName.EndsWith("(Clone)")) {
                    rootName = rootName.Substring(0, rootName.Length - "(Clone)".Length).TrimEnd();
                }
                if (this == root) {
                    return rootName;
                }
                return rootName + "/" + AnimationUtility.CalculateTransformPath(this, root);
            }
            if (!IsSameOrChildOf(root)) {
                throw new Exception($"{GetDebugPath()} is not a child of {root.GetDebugPath()}");
            }
            if (this == root && prettyRoot) {
                return "Avatar Root";
            }
            return AnimationUtility.CalculateTransformPath(this, root);
        }

        public string GetDebugPath() {
            return GetPath(removeCloneFromRoot: true);
        }

        public VFGameObject Clone() {
            return Object.Instantiate(gameObject);
        }

        public VFGameObject CloneDisabled() {
            var inactiveParent = new GameObject("VRCFury Inactive Clone Parent");
            inactiveParent.SetActive(false);
            try {
                SceneManager.MoveGameObjectToScene(inactiveParent, scene);
                VFGameObject clone = Object.Instantiate(gameObject, inactiveParent.transform, true);
                clone.active = false;
                clone.SetParent(null, true);
                return clone;
            } finally {
                Object.DestroyImmediate(inactiveParent);
            }
        }

        public static VFGameObject[] GetRoots() {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.isLoaded)
                .SelectMany(scene => scene.Roots())
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
                name = name.MakeUnique(finalName => {
                    var exists = parent.Find(finalName);
                    return exists != null && exists != this;
                });
            }

            this.name = name;
        }

        public bool HasTag(string tag) {
            return gameObject.CompareTag(tag);
        }

        public bool IsAssetTransform() {
            return !transform.gameObject.scene.IsValid();
        }

        public void Destroy() {
            DestroyDependentComponents(uploadRoots, root => root.IsChildOf(this));
            Object.DestroyImmediate(this);
        }

        public static void DestroyAll(IEnumerable<VFGameObject> objects) {
            var targets = objects.Where(obj => obj != null).ToArray();
            var targetsByRoot = targets
                .SelectMany(obj => obj.uploadRoots.Select(root => (root, obj)))
                .ToLookup(pair => pair.root, pair => (Transform)pair.obj);
            foreach (var group in targetsByRoot) {
                var removed = group.ToHashSet();
                DestroyDependentComponents(new[] { group.Key }, transform => {
                    for (var current = transform; current != null; current = current.parent) {
                        if (removed.Contains(current)) return true;
                    }
                    return false;
                });
            }
            foreach (var target in targets) {
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        private static void DestroyDependentComponents(IEnumerable<VFGameObject> roots, Func<Transform, bool> matches) {
            foreach (var c in roots.SelectMany(root => root.GetComponentsInSelfAndChildren<VRCPhysBoneBase>())) {
                if (RootMatches(c.GetRootTransform(), c, matches))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in roots.SelectMany(root => root.GetComponentsInSelfAndChildren<VRCPhysBoneColliderBase>())) {
                if (RootMatches(c.GetRootTransform(), c, matches))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in roots.SelectMany(root => root.GetComponentsInSelfAndChildren<ContactBase>())) {
                if (RootMatches(c.GetRootTransform(), c, matches))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in GetConstraints(roots, obj => matches(obj))) {
                c.Destroy();
            }
        }

        // Due to unity serialization goofs in some really old broken assets, in some rare situations, a Transform handle
        // can actually be holding a GameObject. If you try to use it as a transform, it'll throw MissingReferenceException.
        // With this try catch, at least you'll be able to find where it is.
        private static bool RootMatches(Transform root, UnityEngine.Component component, Func<Transform, bool> matches) {
            try {
                return matches(root);
            } catch (Exception e) {
                throw new ExceptionWithCause(
                    $"Failed to check root transform of {component.GetType().Name} on {component.owner().GetDebugPath()}",
                    e
                );
            }
        }

        public VFConstraint[] GetConstraints(bool includeParents = false, bool includeChildren = false) {
            return GetConstraints(uploadRoots, affectedObject => {
                if (includeParents) return IsSameOrChildOf(affectedObject);
                if (includeChildren) return affectedObject.IsSameOrChildOf(this);
                return affectedObject == this;
            });
        }

        public ILookup<VFGameObject, VFConstraint> GetConstraintsByTarget() {
            return FindConstraintTargets(uploadRoots, _ => true).ToLookup(pair => pair.target, pair => pair.constraint);
        }

        private static VFConstraint[] GetConstraints(IEnumerable<VFGameObject> roots, Func<VFGameObject, bool> matches) {
            return FindConstraintTargets(roots, matches).Select(pair => pair.constraint).ToArray();
        }

        private static IEnumerable<(VFConstraint constraint, VFGameObject target)> FindConstraintTargets(
            IEnumerable<VFGameObject> roots,
            Func<VFGameObject, bool> matches
        ) {
            foreach (var constraint in VFConstraint.GetConstraintsInSelfAndChildren(roots)) {
                VFGameObject target;
                try {
                    target = constraint.GetAffectedObject();
                    if (target == null || !matches(target)) continue;
                } catch (Exception e) {
                    // The affected transform can have the same malformed reference as a dynamics root.
                    throw new ExceptionWithCause(
                        $"Failed to check affected object of {constraint.GetComponent().GetType().Name} on {constraint.owner().GetDebugPath()}",
                        e
                    );
                }
                yield return (constraint, target);
            }
        }

        public static Func<VFGameObject,VFGameObject[]> getUploadRoots;
        public VFGameObject[] uploadRoots {
            get {
                if (getUploadRoots == null) {
                    throw new Exception("getUploadRoots not implemented in this project type");
                }
                return getUploadRoots(this);
            }
        }

        public T[] GetComponentsInUploadRoot<T>() {
            return uploadRoots.SelectMany(r => r.GetComponentsInSelfAndChildren<T>()).ToArray();
        }

        public static T[] GetComponentsInAllOpenScenes<T>() {
            return GetRoots().SelectMany(r => r.GetComponentsInSelfAndChildren<T>()).ToArray();
        }

        public static UnityEngine.Component[] GetComponentsInAllOpenScenes(Type type) {
            return GetRoots().SelectMany(r => r.GetComponentsInSelfAndChildren(type)).ToArray();
        }

        public bool IsSelected() {
            return Selection.gameObjects.Contains(gameObject);
        }
    }
}
