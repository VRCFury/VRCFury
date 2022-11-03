using System;
using System.Collections;
using System.Runtime.Serialization;
using UnityEngine;

namespace VF.Model {
    public abstract class VRCFuryComponent : MonoBehaviour, ISerializationCallbackReceiver {
        private static readonly int VRCFURY_SER_VERSION = 7; 

        public int vrcfSerVersion;
        [NonSerialized]
        public bool failedToLoad = false;

        public bool IsBroken() {
            return vrcfSerVersion > VRCFURY_SER_VERSION || ContainsNullsInList(this);
        }

        public void OnAfterDeserialize() {
            if (IsBroken()) {
                failedToLoad = true;
            } else {
                vrcfSerVersion = VRCFURY_SER_VERSION;
                failedToLoad = false;
            }
        }
        
        public void OnBeforeSerialize() {
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
                    foreach (var t in list) {
                        if (t == null) return true;
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
    }
}
