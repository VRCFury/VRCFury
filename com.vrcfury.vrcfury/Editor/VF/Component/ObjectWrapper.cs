using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Component {
    // These are here because you can't use Serializable with generics until unity 2020
    // https://forum.unity.com/threads/generics-serialization.746300/
    [Serializable] public class AnimationClipWrapper : ObjectWrapperWrapper<AnimationClip, AnimationClipWrapper> {}
    [Serializable] public class MaterialWrapper : ObjectWrapperWrapper<Material, MaterialWrapper> {}
    [Serializable] public class Texture2DWrapper : ObjectWrapperWrapper<Texture2D, Texture2DWrapper> {}
    [Serializable] public class VrcControllerWrapper : ObjectWrapperWrapper<RuntimeAnimatorController, VrcControllerWrapper> {}
    [Serializable] public class VrcMenuWrapper : ObjectWrapperWrapper<VRCExpressionsMenu, VrcMenuWrapper> {}
    [Serializable] public class VrcParamsWrapper : ObjectWrapperWrapper<VRCExpressionParameters, VrcParamsWrapper> {}
    [Serializable] public class GameObjectWrapper : ObjectWrapperWrapper<GameObject, GameObjectWrapper> {}

    public abstract class ObjectWrapperWrapper<T, W> : ObjectWrapper<T>
        where T : Object
        where W : ObjectWrapper<T>, new()
    {
        public static implicit operator W(ObjectWrapperWrapper<T,W> d) => new W {
            protectedObj = d
        };
    }

    /**
     * This wrapper stores a backup string reference to objects so that we can recover the reference if it
     * goes away and later comes back.
     */
    [Serializable]
    [JsonConverter(typeof(ObjectWrapperJsonConverter))]
    public abstract class ObjectWrapper<T> : ISerializationCallbackReceiver where T : Object {
        [SerializeField]
        private string backupId;
        
        [SerializeField]
        private string backupName;
        
        [SerializeField]
        private string backupFile;

        [SerializeField]
        private T obj;
        
        public T protectedObj {
            set { obj = value; Refresh(); }
            get { Refresh(); return obj; }
        }

        /*
         // Re-add this and make this class not abstract once we can actually start using this class
         // directly in unity 2020
        public static implicit operator GuidWrapper2<T>(T d) {
            if (d == null) return null;
            var w = new GuidWrapper2<T>();
            w.obj = d;
            w.Refresh();
            return w;
        }
        */

        public static implicit operator T(ObjectWrapper<T> d) {
            return d?.GetObject();
        }

        private T GetObject() {
            Refresh();
            return obj;
        }
        
        public static bool operator ==(ObjectWrapper<T> a, ObjectWrapper<T> b) {
            return a?.GetObject() == b?.GetObject();
        }
        public static bool operator !=(ObjectWrapper<T> a, ObjectWrapper<T> b) {
            return a?.GetObject() != b?.GetObject();
        }

        public override bool Equals(object other) {
            if (other is ObjectWrapper<T> o) {
                return o.GetObject() == GetObject();
            }
            return false;
        }

        public override int GetHashCode() {
            return GetObject()?.GetHashCode() ?? 0;
        }

        public void OnBeforeSerialize() {
            Refresh();
        }

        public void OnAfterDeserialize() {
            EditorApplication.delayCall += Refresh;
        }

        [NonSerialized] private Object lastSelected;

        private void Refresh() {
            if (obj == null && lastSelected != null) {
                // The last selection still exists, assume the user actually cleared this pointer on purpose
                // (probably by removing it in the inspector)
                backupId = "";
                backupName = "";
                backupFile = "";
            }

            if (obj == null && !string.IsNullOrEmpty(backupId)) {
                // Try and find the Object using our stored guid info
                if (GlobalObjectId.TryParse(backupId, out var id)) {
                    var asset = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T;
                    if (asset) {
                        obj = asset;
                    }
                }
            }

            if (obj != null) {
                // The selected object is valid, so update our stored ID
                var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(obj);
                backupId = globalObjectId.ToString();
                backupName = obj.name;
                backupFile = AssetDatabase.GUIDToAssetPath(globalObjectId.assetGUID.ToString()) ?? "";
            }
        }

        public override string ToString() {
            Refresh();

            var output = "";
            if (string.IsNullOrWhiteSpace(backupId)) return output;
            output += backupId;
            if (string.IsNullOrWhiteSpace(backupName)) return output;
            output += ":" + backupName.Replace(":", "");
            if (string.IsNullOrWhiteSpace(backupFile)) return output;
            output += ":" + backupFile.Replace(":", "");
            return output;
        }

        public void LoadString(string input) {
            var split = input.Split(':');
            backupId = split[0];
            if (split.Length > 1) backupName = split[1];
            if (split.Length > 2) backupFile = split[2];
            Refresh();
        }
    }

    public class ObjectWrapperJsonConverter : JsonConverter {
        public override bool CanConvert(Type objectType) => throw new NotImplementedException();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            var s = reader.Value as string;
            var inst = Activator.CreateInstance(objectType);
            objectType
                .GetMethod("LoadString")
                .Invoke(inst, new object[] { s });
            return inst;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            writer.WriteValue(value.ToString());
        }
    }
}
