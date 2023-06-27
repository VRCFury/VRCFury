using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VF.Component {
    [CustomEditor(typeof(JsonComponent), true)]
    public class JsonComponentEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            var component = (JsonComponent)target;

            JsonComponentExtensions.Loader loader;
            try {
                loader = component.GetLoader();
            } catch (Exception e) {
                var c2 = new VisualElement();
                c2.Add(new Label("Error while loading json (see console)"));
                UnityEngine.Debug.LogException(e);
                return c2;
            }
            
            object parsed;
            SerializedObject parsedSo;
            try {
                parsed = component.GetParsed();
                parsedSo = component.GetSo();
            } catch (Exception e) {
                var c2 = new VisualElement();
                c2.Add(new Label("Error while parsing json (see console)"));
                UnityEngine.Debug.LogException(e);
                return c2;
            }

            var c = new VisualElement();
            var body = loader.createEditor(parsed, parsedSo.FindProperty("heldObject"));
            body.Bind(parsedSo);
            c.Add(body);
            return c;
        }
    }
}
