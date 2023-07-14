using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VF.Component {
    /**
     * JsonComponent is a component that stores everything as JSON. Unfortunately, for UIElements to work,
     * we still require a unity-serialized copy of the information, so we deserialize the json into a "holder"
     * object that is used exclusively for the inspector. When the json changes, we update the holder,
     * and when the holder changes, we update the json.
     */
    [InitializeOnLoad]
    public static class JsonComponentExtensions {
        private static List<Loader> loaders = new List<Loader>();

        public static void RegisterType<T>(
            Type componentType,
            Type holderType,
            Func<T, SerializedProperty, VisualElement> createEditor,
            JsonSerializerSettings settings
        ) where T : class {
            if (!typeof(JsonComponent).IsAssignableFrom(componentType))
                throw new ArgumentException();
            if (!typeof(ParsedJsonHolder).IsAssignableFrom(holderType))
                throw new ArgumentException();
            loaders.Add(new Loader {
                componentType = componentType,
                parseType = typeof(T),
                jsonSettings = settings,
                holderType = holderType,
                createEditor = (m,p) => createEditor(m as T, p)
            });
        }

        static JsonComponentExtensions() {
            JsonComponent.onValidate = test => {
                test.Debug("Json changed " + test.json);
                var holder = test.GetHolder();
                if (!holder) {
                    test.Debug("Not deserializing (parsed holder not yet present)");
                    return;
                }
                if (holder.saving) {
                    test.Debug("Not deserializing (parsed holder is currently serializing)");
                    return;
                }
                if (holder.reconnecting) {
                    test.Debug("Not deserializing (parsed holder is currently reconnecting to the component, probably due to instantiating a copy)");
                    return;
                }

                test.Deserialize(holder);
                new SerializedObject(holder).Update();
            };

            JsonComponent.onDestroy = test => {
                test.Debug("Json destroyed");
                var holder = test.GetHolder();
                if (holder) {
                    Object.DestroyImmediate(holder);
                }
            };
            
            // Scripts just reloaded, so delete all holders so they get recreated fresh,
            // instead of the json components trying to reconnect to them
            foreach (var holder in Resources.FindObjectsOfTypeAll<ParsedJsonHolder>()) {
                Object.DestroyImmediate(holder);
            }
        }

        private static ParsedJsonHolder GetHolder(this JsonComponent component) {
            // If this has been hot-reloaded or Object.instantiated, the holder may already exist,
            // and we just don't have a reference to it.
            return component.gameObject.GetComponents<ParsedJsonHolder>()
                .FirstOrDefault(other => other.original == component);
        }

        private static ParsedJsonHolder GetOrCreateHolder(this JsonComponent component) {
            var holder = component.GetHolder();
            if (!holder) {
                component.Debug("Creating holder");
                var loader = component.GetLoader();
                holder = component.gameObject.AddComponent(loader.holderType) as ParsedJsonHolder;
                holder.hideFlags |= HideFlags.HideAndDontSave | HideFlags.HideInInspector;
                holder.original = component;
                component.Deserialize(holder);
            }
            holder.reconnecting = false;
            holder.error?.Throw();
            return holder;
        }

        public static SerializedObject GetSo(this JsonComponent component) {
            return new SerializedObject(component.GetOrCreateHolder());
        }
        
        public static object GetParsed(this JsonComponent component) {
            return component.GetOrCreateHolder().heldObject;
        }

        private static void Deserialize(this JsonComponent component, ParsedJsonHolder holder) {
            component.Debug("json -> parsed");
            try {
                var loader = component.GetLoader();
                holder.heldObject = JsonConvert.DeserializeObject(component.json, loader.parseType, loader.jsonSettings);
                if (holder.heldObject == null) throw new Exception("Json content was empty");
                holder.error = null; 
            } catch (Exception e) {
                holder.heldObject = null;
                holder.error = ExceptionDispatchInfo.Capture(e);
            }
        }
 
        private static void Serialize(this JsonComponent component, ParsedJsonHolder holder) {
            if (holder.error != null) {
                component.Debug("Serialization skipped because there's a deserialization error");
                return;
            }

            component.Debug("parsed -> json");
            holder.saving = true;
            try {
                var so = new SerializedObject(component);
                try {
                    var loader = component.GetLoader();
                    so.FindProperty("json").stringValue = JsonConvert.SerializeObject(holder.heldObject, loader.jsonSettings);
                } catch (Exception e) {
                    UnityEngine.Debug.LogException(new Exception("Failed to serialize JsonProperty", e));
                }
                so.ApplyModifiedProperties();
            } finally {
                holder.saving = false;
            }
        }
         
        public static void Debug(this JsonComponent component, string msg) {
            UnityEngine.Debug.Log("JsonComponent: " + component + " " + msg);
        }
        
        public static Loader GetLoader(this JsonComponent component) {
            var loader = loaders.FirstOrDefault(l => l.componentType.IsInstanceOfType(component));
            if (loader == null) {
                throw new Exception($"Loader not found for {component.GetType().Name} type");
            }

            return loader;
        }

        public class ParsedJsonHolder : MonoBehaviour {
            public JsonComponent original;
            [SerializeReference] public object heldObject;
            [NonSerialized] public bool saving; // The holder is currently saving json to the parent component
            [NonSerialized] public bool reconnecting = true; // The holder is currently reconnecting to the parent component (can happen if Object.Instantiate makes a copy)
            [NonSerialized] public ExceptionDispatchInfo error;

            public void OnValidate() {
                if (original != null) {
                    original.Debug("parsed changed");
                    original.Serialize(this);
                }
            }
        }

        public class Loader {
            public Type componentType;
            public Type parseType;
            public Type holderType;
            public JsonSerializerSettings jsonSettings;
            public Func<object, SerializedProperty, VisualElement> createEditor;
        }
    }
}