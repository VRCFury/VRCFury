using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Upgradeable {
    public abstract class VrcfUpgradeableMonoBehaviour : MonoBehaviour, IUpgradeable {
        [SerializeField] private int version = -1;
        [JsonIgnore] public string unityVersion;
        [JsonIgnore] public string vrcfuryVersion;
        [JsonIgnore] public string backupData;
        [JsonIgnore] public List<Object> backupObj;

        public static string currentVrcfVersion { private get; set; }

        public int Version { get => version; set => version = value; }
        void ISerializationCallbackReceiver.OnAfterDeserialize() { this.IUpgradeableOnAfterDeserialize(); }

        void ISerializationCallbackReceiver.OnBeforeSerialize() {
            unityVersion = Application.unityVersion;
            vrcfuryVersion = currentVrcfVersion;
            this.IUpgradeableOnBeforeSerialize();
        }

        private void OnValidate() {
            var backupObjects = new List<Object>();
            var settings = new JsonSerializerSettings {
                ContractResolver = new MyContractResolver(),
                Context = new StreamingContext(StreamingContextStates.Other, new JsonContext {
                    objects = backupObjects
                }),
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            var json = JsonConvert.SerializeObject(this, settings);
            Debug.Log(json);
            Debug.Log(string.Join(",", backupObjects.Select(o => o.name)));
            JsonConvert.PopulateObject(json, this, settings);
        }

        public virtual bool Upgrade(int fromVersion) {
            return false;
        }

        public virtual int GetLatestVersion() {
            return 0;
        }

        public class JsonContext {
            public List<Object> objects;
        }
        
        public class MyContractResolver : DefaultContractResolver {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var props = UnitySerializationUtils.GetAllSerializableFields(type)
                    .Select(f => {
                        var p = base.CreateProperty(f, memberSerialization);
                        p.Writable = true;
                        p.Readable = true;
                        if (typeof(Object).IsAssignableFrom(f.FieldType)) {
                            p.Converter = new ObjectConverter();
                        }
                        return p;
                    })
                    .ToList();
                return props;
            }
        }

        public class ReferenceConverter : JsonConverter {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
                
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
                throw new NotImplementedException();
            }

            public override bool CanConvert(Type objectType) {
                return true;
            }
        }

        public class ObjectConverter : JsonConverter<Object> {
            public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer) {
                if (value == null) {
                    writer.WriteValue(-1);
                    return;
                }
                var context = serializer.Context.Context as JsonContext;
                var id = context.objects.IndexOf(value);
                if (id < 0) {
                    id = context.objects.Count;
                    context.objects.Add(value);
                }
                writer.WriteValue(id);
            }

            public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, bool hasExistingValue,
                JsonSerializer serializer) {
                var context = serializer.Context.Context as JsonContext;
                var id = reader.Value as int?;
                if (id == null || id < 0 || id >= context.objects.Count) return null;
                return context.objects[id.Value];
            }
        }
    }
}
