using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

namespace VF.Model {
    public abstract class VRCFuryComponent : MonoBehaviour, ISerializationCallbackReceiver {
        [SerializeField]
        private int version = -1;

        [NonSerialized]
        private int failedToLoad = 0;

        public bool IsBroken() {
            return failedToLoad > 0;
        }
        public string GetBrokenMessage() {
            if (failedToLoad == 1) return $"Version too new ({version} > {GetLatestVersion()}";
            if (failedToLoad == 2) return "Found a null list on a child object";
            if (failedToLoad > 0) return "Unknown error";
            return null;
        }

        public void OnAfterDeserialize() {
            if (version < 0) {
                // Object was deserialized, but had no version. Default to version 0.
                version = 0;
            }
            if (version > GetLatestVersion()) {
                failedToLoad = 1;
            } else if (ContainsNullsInList(this)) {
                failedToLoad = 2;
            }
            
#if UNITY_EDITOR
            EditorApplication.delayCall += () => {
                if (!this) return;
                //Debug.Log("Loaded " + this);
                if (failedToLoad > 0) {
                    var path = AssetDatabase.GetAssetPath(this);
                    if (!string.IsNullOrWhiteSpace(path)) {
                        //Debug.LogError("VRCFury is triggering manual reload of asset " + path + " (previous import corrupted)");
                        Debug.LogWarning(
                            $"VRCFury detected VRCFury component in asset at path {path} is corrupted. " +
                            "Hopefully it will be fixed during the prefab import auto-fix.");
                        //attemptedReload.Add(path);
                        //AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    }
                } else {
                    Upgrade();
                }
            };
#endif
        }
        
        public void OnBeforeSerialize() {
            if (version < 0) { 
                // Object was created fresh (not deserialized), so it's automatically the newest
                version = GetLatestVersion();
            }
        }

        private static bool ContainsNullsInList(object obj) {
            if (obj == null) return false;
            var objType = obj.GetType();
            if (!objType.FullName.StartsWith("VF")) return false;
            var fields = objType.GetFields();
            foreach (var field in fields) {
                var value = field.GetValue(obj);
                if (value is IList) {
                    var list = value as IList;
                    var isRef = field.GetCustomAttribute<SerializeReference>() != null;
                    foreach (var t in list) {
                        if (t == null && isRef) return true;
                        if (ContainsNullsInList(t)) return true;
                    }
                } else {
                    var type = field.FieldType;
                    if (type.IsClass) {
                        if (ContainsNullsInList(value)) return true;
                    }
                }
            }
            return false;
        }
        
        private int GetVersion() {
            return version < 0 ? GetLatestVersion() : version;
        }

        public void Upgrade() {
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabInstance(this)) return;
            if (Application.isPlaying) return;
            if (IsBroken()) return;
            UpgradeAlways();
            var fromVersion = GetVersion();
            var latestVersion = GetLatestVersion();
            if (fromVersion < latestVersion) {
                //Debug.LogWarning("UPGRADING " + this);
                Upgrade(fromVersion);
                if (!this) {
                    // The upgrade deleted this component!
                    return;
                }
                version = latestVersion;
                EditorUtility.SetDirty(this);
            }
#endif
        }

        /**
         * This gets called when the component version is lower than what you've set in GetLatestVersion.
         * Do most upgrades here!
         */
        protected virtual void Upgrade(int fromVersion) {
        }

        /**
         * This gets called every time an upgrade check is invoked. Use sparingly, and beware that you'll
         * have to call SetDirty yourself if you change something.
         */
        protected virtual void UpgradeAlways() {
        }

        protected virtual int GetLatestVersion() {
            return 1;
        }
    }
}
