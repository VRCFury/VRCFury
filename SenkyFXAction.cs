using System;
using UnityEngine;
using UnityEditor;

[Serializable]
public class SenkyFXAction {
    public const string TOGGLE = "toggle";
    public const string BLENDSHAPE = "blendShape";

    public string type;
    public GameObject obj;
    public string blendShape;
}

[CustomPropertyDrawer(typeof(SenkyFXAction))]
public class SenkyFXActionDrawer : BetterPropertyDrawer {
    protected override void render(SerializedProperty prop, GUIContent label) {
        var type = prop.FindPropertyRelative("type").stringValue;

        if (type == SenkyFXAction.TOGGLE) {
            var one = renderRect(line);
            renderLabel(new Rect(one.x, one.y, one.width/2, one.height), "Object Toggle");
            renderProp(new Rect(one.x+one.width/2, one.y, one.width/2, one.height), prop.FindPropertyRelative("obj"));
        } else if (type == SenkyFXAction.BLENDSHAPE) {
            var one = renderRect(line);
            renderLabel(new Rect(one.x, one.y, one.width/2, one.height), "BlendShape");
            renderProp(new Rect(one.x+one.width/2, one.y, one.width/2, one.height), prop.FindPropertyRelative("blendShape"));
        } else {
            renderLabel("Unknown action: " + type);
        }
    }
}
