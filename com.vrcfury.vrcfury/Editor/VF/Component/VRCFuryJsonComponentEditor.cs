using System;
using JsonSubTypes;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using Object = UnityEngine.Object;

namespace VF.Component {
    [InitializeOnLoad]
    public class VRCFuryJsonComponentEditor : JsonComponentEditor {
        static VRCFuryJsonComponentEditor() {
            var jsonSettings = new JsonSerializerSettings();
            jsonSettings.Converters.Add(JsonSubtypesConverterBuilder
                .Of(typeof(FeatureBuilder), "type")
                .SetFallbackSubtype(typeof(UnknownFeature))
                .RegisterSubtype(typeof(BlendshapeOptimizerBuilder), "blendshapeOptimizer")
                .Build());
            
            jsonSettings.Converters.Add(new GuidWrapperJsonConverter());
            jsonSettings.Converters.Add(new NoBadTypesConverter());

            Func<FeatureBuilder, SerializedProperty, VisualElement> CreateEditor = (builder, prop) => {
                var c = new VisualElement();
                c.styleSheets.Add(VRCFuryEditorUtils.GetResource<StyleSheet>("VRCFuryStyle.uss"));
                c.Add(builder.CreateEditor(prop));
                return c;
            };
            JsonComponentExtensions.RegisterType(
                typeof(VRCFuryComponentNew),
                typeof(VRCFuryJsonHolder),
                CreateEditor,
                jsonSettings
            );
        }

        public class VRCFuryJsonHolder : JsonComponentExtensions.ParsedJsonHolder, IVrcfEditorOnly {
        }

        private class NoBadTypesConverter : JsonConverter {
            public override bool CanConvert(Type objectType) {
                if (typeof(Object).IsAssignableFrom(objectType))
                    throw new NotImplementedException();
                return false;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
                throw new NotImplementedException();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
                throw new NotImplementedException();
            }
        }
        
        public class GuidWrapperJsonConverter : JsonConverter<GuidWrapper> {
            public override void WriteJson(JsonWriter writer, GuidWrapper value, JsonSerializer serializer) {
                writer.WriteValue(value.id);
            }

            public override GuidWrapper ReadJson(JsonReader reader, Type objectType, GuidWrapper existingValue, bool hasExistingValue,
                JsonSerializer serializer) {
                var s = reader.Value as string;
                var inst = Activator.CreateInstance(objectType) as GuidWrapper;
                inst.id = s;
                return inst;
            }
        }
        
        [Serializable] 
        public class UnknownFeature : FeatureBuilder {
            public override VisualElement CreateEditor(SerializedProperty prop) {
                var c = new VisualElement();
                c.Add(VRCFuryEditorUtils.WrappedLabel("Unknown builder"));
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("type"), "Type"));
                return c;
            }
        }
    }
}