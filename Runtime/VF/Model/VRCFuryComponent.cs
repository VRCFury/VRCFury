using System;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

namespace VF.Model {
    public class VRCFuryComponent : MonoBehaviour, ISerializationCallbackReceiver {
        private static readonly int VRCFURY_SER_VERSION = 1;
        private static bool warningShown = false;
        
        public int vrcfSerVersion;
        [NonSerialized]
        private bool failedToLoad = false;

        public void OnAfterDeserialize() {
            if (vrcfSerVersion > VRCFURY_SER_VERSION) {
                if (!warningShown) {
                    warningShown = true;
                    EditorApplication.delayCall += () => {
                        EditorUtility.DisplayDialog("VRCFury Error",
                            "This project contains VRCFury assets newer than the installed VRCFury plugin. " +
                            "You need to upgrade VRCFury using Tools -> VRCFury -> Upgrade VRCFury.",
                            "Ok");
                    };
                }
                failedToLoad = true; 
                throw new SerializationException("Cannot load VRCFury component, VRCFury plugin is out of date");
            } else {
                vrcfSerVersion = VRCFURY_SER_VERSION;
            }
        }
        
        public void OnBeforeSerialize() {
            if (failedToLoad) {
                throw new SerializationException("Cannot save VRCFury component, VRCFury plugin is out of date");
            }
        }
    }
}
