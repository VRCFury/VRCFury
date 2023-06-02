using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace VF.Component {
    public abstract class VRCFuryComponent : MonoBehaviour, ISerializationCallbackReceiver
#if VRC_NEW_HOOK_API
        , IEditorOnly
#endif
    {
        [SerializeField]
        private int version = -1;

        public bool IsBroken() {
            return GetBrokenMessage() != null;
        }
        public string GetBrokenMessage() {
            if (version > GetLatestVersion()) {
                return $"This component was created with a newer version of VRCFury ({version} > {GetLatestVersion()}";
            } else if (ContainsNullsInList(this)) {
                return "Found a null list on a child object";
            }
            return null;
        }

        public void OnAfterDeserialize() {
            if (version < 0) {
                // Object was deserialized, but had no version. Default to version 0.
                version = 0;
            }

#if UNITY_EDITOR
            EditorApplication.delayCall += Upgrade;
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
                        if (t == null && isRef) {
                            return true;
                        }
                        if (ContainsNullsInList(t)) {
                            return true;
                        }
                    }
                } else {
                    var type = field.FieldType;
                    if (type.IsClass) {
                        if (ContainsNullsInList(value)) {
                            return true;
                        }
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
            if (!this) return;
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
