using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VF.Builder {
    /**
     * VFGameObject is a wrapper around Transform and GameObject, combining the two into one object we can use
     * everywhere, and providing helper methods that are commonly used.
     */
    public class VFGameObject {
        private GameObject _gameObject;
        public VFGameObject(GameObject gameObject) {
            _gameObject = gameObject;
        }

        public GameObject gameObject => _gameObject;
        public Transform transform => _gameObject == null ? null : _gameObject.transform;
        public static implicit operator VFGameObject(GameObject d) => new VFGameObject(d);
        public static implicit operator VFGameObject(Transform d) => new VFGameObject(d == null ? null : d.gameObject);
        public static implicit operator GameObject(VFGameObject d) => d?.gameObject;
        public static implicit operator UnityEngine.Object(VFGameObject d) => d?.gameObject;
        public static implicit operator Transform(VFGameObject d) => d?.transform;
        public static implicit operator bool(VFGameObject d) => d?.gameObject;
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

        public int childCount => transform.childCount;
        
        public UnityEngine.Component GetComponent(Type t) {
            return gameObject.GetComponent(t);
        }
        
        public T GetComponent<T>() where T : UnityEngine.Component {
            return gameObject.GetComponent<T>();
        }
        
        public T[] GetComponents<T>() where T : UnityEngine.Component {
            return gameObject.GetComponents<T>();
        }
        
        public T GetComponentInSelfOrParent<T>() where T : UnityEngine.Component {
            // GetComponentInParent<T> randomly returns null sometimes, even if the component actually exists :|
            // This is especially common in editors (where the reference is gotten through prop.serializedObject)
            // GetComponentsInParent works fine though? It's almost as if some hidden destroyed component is present.
            //return gameObject.GetComponentInParent<T>();
            return GetComponentsInSelfAndParents<T>().FirstOrDefault();
        }

        public T[] GetComponentsInSelfAndChildren<T>() {
            return gameObject.GetComponentsInChildren<T>(true);
        }
        
        public T[] GetComponentsInSelfAndParents<T>() {
            return gameObject.GetComponentsInParent<T>(true);
        }

        public T AddComponent<T>() where T : UnityEngine.Component {
            return gameObject.AddComponent<T>();
        }
        
        public VFGameObject Find(string search) {
            return transform.Find(search);
        }

        public bool IsChildOf(Transform other) {
            return transform.IsChildOf(other);
        }

        public string GetPath(VFGameObject root = null) {
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
            return AnimationUtility.CalculateTransformPath(this, root);
        }

        public void Destroy() {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        public VFGameObject Clone() {
            return UnityEngine.Object.Instantiate(gameObject);
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
    }
}
