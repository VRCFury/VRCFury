using System;
using UnityEditor;
using UnityEngine;

namespace VF.Upgradeable {
    public abstract class VrcfUpgradeableMonoBehaviour : MonoBehaviour, IUpgradeable {
        [SerializeField] private int version = -1;
        public string unityVersion;
        public string vrcfuryVersion;
        public string backup;
        [NonSerialized] private bool backupLoaded;

        public static string currentVrcfVersion { private get; set; }

        public int Version { get => version; set => version = value; }

        void ISerializationCallbackReceiver.OnAfterDeserialize() {
            this.IUpgradeableOnAfterDeserialize();
        }

        public void OnValidate() {
            //Debug.Log("VALIDATE");
            if (!backupLoaded) {
                backupLoaded = true;
                //Debug.Log("Loading backup " + backup);
                EditorJsonUtility.FromJsonOverwrite(backup, this);
            } else {
                unityVersion = Application.unityVersion;
                vrcfuryVersion = currentVrcfVersion;
                backup = null;
                backup = EditorJsonUtility.ToJson(this);
                //Debug.Log("Saved backup " + backup);
                EditorUtility.SetDirty(this);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() {
            this.IUpgradeableOnBeforeSerialize();
        }

        public virtual bool Upgrade(int fromVersion) {
            return false;
        }

        public virtual int GetLatestVersion() {
            return 0;
        }
    }
}
