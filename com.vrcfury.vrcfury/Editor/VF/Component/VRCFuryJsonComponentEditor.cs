using System;
using JsonSubTypes;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature;
using VF.Feature.Base;
using VF.Inspector;
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
            
            jsonSettings.Converters.Add(new ObjectConverter());

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

        private class ObjectConverter : JsonConverter<Object> {
            public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer) {
                writer.WriteValue(JsonSerializerState.GetId(value));
            }

            public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, bool hasExistingValue,
                JsonSerializer serializer) {
                if (reader.Value is int i) {
                    return JsonSerializerState.GetObject(i);
                }
                return null;
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