using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Model {
    [Serializable]
    public class GuidAnimationClip : GuidWrapper<AnimationClip> {
        public static implicit operator GuidAnimationClip(AnimationClip d) => new GuidAnimationClip {
            protectedObj = d
        };
    }
    
    [Serializable]
    public class GuidMaterial : GuidWrapper<Material> {
        public static implicit operator GuidMaterial(Material d) => new GuidMaterial {
            protectedObj = d
        };
    }
    
    [Serializable]
    public class GuidTexture2d : GuidWrapper<Texture2D> {
        public static implicit operator GuidTexture2d(Texture2D d) => new GuidTexture2d {
            protectedObj = d
        };
    }
    
    [Serializable]
    public class GuidController : GuidWrapper<RuntimeAnimatorController> {
        public static implicit operator GuidController(RuntimeAnimatorController d) => new GuidController {
            protectedObj = d
        };
    }
    
    [Serializable]
    public class GuidMenu : GuidWrapper<VRCExpressionsMenu> {
        public static implicit operator GuidMenu(VRCExpressionsMenu d) => new GuidMenu {
            protectedObj = d
        };
    }
    
    [Serializable]
    public class GuidParams : GuidWrapper<VRCExpressionParameters> {
        public static implicit operator GuidParams(VRCExpressionParameters d) => new GuidParams {
            protectedObj = d
        };
    }

    [Serializable]
    public class GuidWrapper<T> : ISerializationCallbackReceiver where T : Object {
        [SerializeField]
        private long fileID;
        [SerializeField]
        private string guid;

        [SerializeField]
        private T obj;

        protected T protectedObj {
            set { obj = value; Refresh(); }
            get { Refresh(); return obj; }
        }

        public static implicit operator GuidWrapper<T>(T d) {
            if (d == null) return null;
            var w = new GuidWrapper<T>();
            w.obj = d;
            w.Refresh();
            return w;
        }

        public static implicit operator T(GuidWrapper<T> d) {
            return d?.GetObject();
        }

        private T GetObject() {
            Refresh();
            return obj;
        }
        
        public static bool operator ==(GuidWrapper<T> a, GuidWrapper<T> b) {
            return a?.GetObject() == b?.GetObject();
        }
        public static bool operator !=(GuidWrapper<T> a, GuidWrapper<T> b) {
            return a?.GetObject() != b?.GetObject();
        }

        public void OnBeforeSerialize() {
            Refresh();
        }

        public void OnAfterDeserialize() {
#if UNITY_EDITOR
            EditorApplication.delayCall += Refresh;
#endif
        }

        [NonSerialized] private Object lastSelected;

        private void Refresh() {
#if UNITY_EDITOR
            if (obj != null) {
                // The selected object is valid, so update our stored guid
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out fileID);
                lastSelected = obj;
                return;
            }

            if (lastSelected != null) {
                // The last selection still exists, assume the user actually cleared this pointer on purpose
                // (probably by removing it in the inspector)
                guid = "";
                fileID = 0;
                return;
            }

            if (!string.IsNullOrEmpty(guid)) {
                // Try and find the Object using our stored guid info
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == null) return;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
                    if (!(asset is T t)) continue;
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid_, out long fileID_)) continue;
                    if (guid_ != guid) continue;
                    if (fileID_ != fileID) continue;
                    lastSelected = obj = t;
                    return;
                }
            
                // Sometimes the fileId of the main animator controller in a file changes randomly, so if the main asset
                // in the file is the right type, just assume it's the one we're looking for.
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main is T mainT) {
                    lastSelected = obj = mainT;
                    return;
                }
            }
#endif
        }
    }
}
